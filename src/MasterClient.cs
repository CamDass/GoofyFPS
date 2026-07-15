using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LiteNetLib;

// ========================================================
// LE CLIENT DU SERVEUR MAÎTRE + LE HOLE PUNCHING (Phase 2 de la roadmap WAN)
// ========================================================
// Côté HÔTE : enregistre le salon (POST), le maintient vivant (heartbeat + ré-introduction
//   NAT toutes les 25 s pour garder le mapping ouvert), le ferme proprement (DELETE).
// Côté CLIENT : résout un code (GET), puis lance une machine à états de perçage NAT
//   (introduce -> succès -> connect), avec réessais et repli direct-IP en cas d'échec.
//
// RIEN de tout ça ne bloque la boucle de jeu : les appels HTTP tournent en tâche de fond
// et déposent leur résultat dans des champs volatils lus par la boucle principale.
partial class Program
{
    static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    // --- État côté hôte ---
    public enum EtatSalon { Aucun, Creation, Actif, Echec }
    public static volatile EtatSalon etatSalon = EtatSalon.Aucun;
    public static string roomCode = "";        // le code à afficher dans le lobby
    static string roomHostKey = "";            // secret : ne JAMAIS afficher/logguer
    static float heartbeatTimer = 0f;
    static float punchKeepaliveTimer = 0f;

    // --- État côté client (machine à états du perçage) ---
    public enum EtatJoin { Repos, Resolution, Percage, Connexion, Echec }
    public static volatile EtatJoin etatJoin = EtatJoin.Repos;
    public static string joinErreur = "";      // message affiché en cas d'échec
    public static string codeSaisi = "";       // code tapé par le joueur dans l'UI
    static string joinCode = "";
    static float punchTimer = 0f;
    static int punchEssais = 0;
    const int PunchEssaisMax = 3;
    static volatile bool punchConnectDemande = false;
    static IPEndPoint? punchEndpoint = null;    // rempli par OnNatIntroductionSuccess
    static double punchDebut = 0;

    // ==========================================
    // INIT DU MODULE NAT (appelé par AllumerMoteurReseau, après netManager.Start)
    // ==========================================
    public static void InitNatPunch()
    {
        netManager.NatPunchEnabled = true;
        var listener = new EventBasedNatPunchListener();
        listener.NatIntroductionSuccess += (targetEndPoint, type, token) =>
        {
            // Le maître nous a mariés et le perçage a ouvert les deux NAT.
            // SEUL le client appelle Connect ; l'hôte n'a qu'à laisser sa porte ouverte
            // (son ConnectionRequestEvent acceptera la connexion entrante du client).
            Console.WriteLine($"[NAT] Perçage réussi vers {targetEndPoint} (token {token}).");
            if (!isServer)
            {
                punchEndpoint = targetEndPoint;
                punchConnectDemande = true;
            }
        };
        netManager.NatPunchModule.Init(listener);
    }

    // Le serveur maître comme cible du rendez-vous UDP (résolu à la volée)
    static void EnvoyerIntroduction(string token)
    {
        try { netManager.NatPunchModule.SendNatIntroduceRequest(NetConfig.PunchHost, NetConfig.PunchPort, token); }
        catch (Exception ex) { Console.WriteLine($"[NAT] Introduction impossible : {ex.Message}"); }
    }

    // ==========================================
    // HÔTE : CRÉER / MAINTENIR / FERMER LE SALON
    // ==========================================
    public static void HoteCreerSalon()
    {
        etatSalon = EtatSalon.Creation;
        roomCode = "";
        _ = HoteCreerSalonAsync();
    }

    static async Task HoteCreerSalonAsync()
    {
        try
        {
            var corps = new
            {
                version = NetConfig.ProtocolVersion,
                name = NetConfig.NettoyerNom(hostMatchName, "Salon"),
                maxPlayers = NetConfig.MaxPlayers,
                mapIndex = mapChoisieIndex,
                hasPassword = !string.IsNullOrEmpty(hostRoomPassword) // Task 2.9
            };
            using var resp = await http.PostAsync(NetConfig.MasterUrl + "/v1/rooms", JsonContent(corps));
            if (!resp.IsSuccessStatusCode) { etatSalon = EtatSalon.Echec; return; }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            roomCode = doc.RootElement.GetProperty("code").GetString() ?? "";
            roomHostKey = doc.RootElement.GetProperty("hostKey").GetString() ?? "";
            etatSalon = EtatSalon.Actif;
            heartbeatTimer = NetConfig.HeartbeatIntervalSec;
            punchKeepaliveTimer = 0f; // ré-introduction immédiate
            Console.WriteLine($"[MAÎTRE] Salon enregistré, code = {roomCode}");
        }
        catch (Exception ex)
        {
            // Maître injoignable : on ne casse pas la partie -> repli LAN + IP directe
            etatSalon = EtatSalon.Echec;
            Console.WriteLine($"[MAÎTRE] Enregistrement échoué ({ex.GetType().Name}) : repli LAN/IP directe.");
        }
    }

    // Appelé chaque frame côté hôte quand un salon est actif
    public static void TickMasterHote(float dt)
    {
        if (!isServer || etatSalon != EtatSalon.Actif || string.IsNullOrEmpty(roomCode)) return;

        heartbeatTimer -= dt;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = NetConfig.HeartbeatIntervalSec;
            _ = HoteHeartbeatAsync();
        }

        punchKeepaliveTimer -= dt;
        if (punchKeepaliveTimer <= 0f)
        {
            punchKeepaliveTimer = NetConfig.PunchKeepaliveSec;
            // "H:<code>:<fragment de hostKey>" -> prouve qu'on est le vrai hôte (S14)
            string frag = roomHostKey.Length >= 8 ? roomHostKey.Substring(0, 8) : roomHostKey;
            EnvoyerIntroduction($"H:{roomCode}:{frag}");
        }
    }

    static async Task HoteHeartbeatAsync()
    {
        try
        {
            int joueurs = 1;
            lock (_lobbyLock) joueurs = Math.Max(1, currentLobbyPlayers.Count);
            string etat = currentState == GameState.Playing ? "playing" : "lobby";
            var corps = new { players = joueurs, state = etat };
            using var req = new HttpRequestMessage(HttpMethod.Put, NetConfig.MasterUrl + $"/v1/rooms/{roomCode}")
            { Content = JsonContent(corps) };
            req.Headers.Add("X-Host-Key", roomHostKey);
            using var resp = await http.SendAsync(req);
            // 404 = le maître a redémarré et a oublié notre salon -> on se réenregistre
            if (resp.StatusCode == HttpStatusCode.NotFound) HoteCreerSalon();
        }
        catch { /* transitoire : le prochain heartbeat réessaiera */ }
    }

    public static void HoteFermerSalon()
    {
        if (string.IsNullOrEmpty(roomCode)) return;
        string code = roomCode, key = roomHostKey;
        roomCode = ""; roomHostKey = ""; etatSalon = EtatSalon.Aucun;
        _ = HoteFermerSalonAsync(code, key);
    }

    static async Task HoteFermerSalonAsync(string code, string key)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, NetConfig.MasterUrl + $"/v1/rooms/{code}");
            req.Headers.Add("X-Host-Key", key);
            await http.SendAsync(req);
        }
        catch { /* peu importe : le TTL nettoiera le salon côté maître */ }
    }

    // ==========================================
    // CLIENT : REJOINDRE PAR CODE (résolution + perçage NAT)
    // ==========================================
    public static void ClientRejoindreParCode(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        joinErreur = "";
        etatJoin = EtatJoin.Resolution;
        joinCode = code;
        _ = ClientResoudreEtPercerAsync(code);
    }

    static async Task ClientResoudreEtPercerAsync(string code)
    {
        try
        {
            using var resp = await http.GetAsync(NetConfig.MasterUrl + $"/v1/rooms/{code}");
            if (resp.StatusCode == HttpStatusCode.NotFound) { EchecJoin("Code de salon inconnu."); return; }
            if (!resp.IsSuccessStatusCode) { EchecJoin("Serveur maître injoignable."); return; }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            int version = root.GetProperty("version").GetInt32();
            int players = root.GetProperty("players").GetInt32();
            int maxPlayers = root.GetProperty("maxPlayers").GetInt32();
            if (version != NetConfig.ProtocolVersion) { EchecJoin("Version du jeu différente de l'hôte."); return; }
            if (players >= maxPlayers) { EchecJoin("Le salon est plein."); return; }

            // Résolution OK -> on lance la machine à états de perçage
            etatJoin = EtatJoin.Percage;
            punchEssais = 0;
            punchTimer = 0f; // percera à la première frame de TickMasterClient
            punchDebut = Raylib_cs.Raylib.GetTime();
        }
        catch (Exception ex)
        {
            EchecJoin("Serveur maître injoignable.");
            Console.WriteLine($"[MAÎTRE] Résolution échouée : {ex.GetType().Name}");
        }
    }

    // Appelé chaque frame côté client tant qu'on tente de rejoindre par code
    public static void TickMasterClient(float dt)
    {
        // Le perçage a abouti : on se connecte (marshalé depuis le thread NAT)
        if (punchConnectDemande && punchEndpoint != null)
        {
            punchConnectDemande = false;
            etatJoin = EtatJoin.Connexion;
            Console.WriteLine($"[NAT] Connexion à {punchEndpoint} après perçage.");
            ConnecterAHote(punchEndpoint);
            _ = EnvoyerTelemetriePunch(true);
            currentState = GameState.Lobby;
            etatJoin = EtatJoin.Repos;
            return;
        }

        if (etatJoin != EtatJoin.Percage) return;

        punchTimer -= dt;
        if (punchTimer <= 0f)
        {
            if (punchEssais >= PunchEssaisMax)
            {
                _ = EnvoyerTelemetriePunch(false);
                EchecJoin("Perçage NAT impossible (NAT strict). Essaie l'IP directe.");
                return;
            }
            punchEssais++;
            punchTimer = 3f; // 3 s entre chaque tentative
            EnvoyerIntroduction($"C:{joinCode}");
            Console.WriteLine($"[NAT] Demande de perçage {joinCode} (tentative {punchEssais}/{PunchEssaisMax}).");
        }
    }

    static void EchecJoin(string message)
    {
        etatJoin = EtatJoin.Echec;
        joinErreur = message;
        derniereErreurReseau = message;
        Console.WriteLine($"[MAÎTRE] Échec join : {message}");
    }

    static async Task EnvoyerTelemetriePunch(bool ok)
    {
        try
        {
            int duree = (int)((Raylib_cs.Raylib.GetTime() - punchDebut) * 1000);
            var corps = new { ok, durationMs = Math.Max(0, duree) };
            await http.PostAsync(NetConfig.MasterUrl + "/v1/telemetry/punch", JsonContent(corps));
        }
        catch { /* télémétrie best-effort */ }
    }

    static StringContent JsonContent(object o) =>
        new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");
}
