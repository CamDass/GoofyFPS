using System.Text.Json;
using System.Text.Json.Serialization;
using GoofyMaster;

// ========================================================
// GOOFY MASTER — annuaire de salons + rendez-vous NAT (Phase 2 de la roadmap WAN)
// UN seul service, deux faces :
//   - REST HTTPS (via nginx) : créer / heartbeat / résoudre / lister les salons
//   - UDP (LiteNetLib NatPunchModule) : hole punching entre hôte et client
// Aucune base de données : tout est en mémoire, avec expiration par TTL (S15).
// ========================================================

const int HttpPort = 5100;   // Kestrel n'écoute QUE en local ; nginx fait le TLS (S10)
const int UdpPort = 7790;    // le seul port ouvert au monde côté pare-feu pour l'UDP

var builder = WebApplication.CreateBuilder(args);

// S10 : on n'écoute que sur la boucle locale. C'est nginx (reverse proxy TLS) qui
// expose le service au monde sur le sous-domaine dédié.
builder.WebHost.ConfigureKestrel(o =>
{
    // Par défaut (production) : localhost uniquement, nginx fait le TLS (S10).
    // GOOFY_BIND=0.0.0.0 (ou une IP LAN) permet un test à 2 machines sans nginx.
    string? bind = Environment.GetEnvironmentVariable("GOOFY_BIND");
    if (string.IsNullOrEmpty(bind)) o.ListenLocalhost(HttpPort);
    else if (bind is "0.0.0.0" or "any") o.ListenAnyIP(HttpPort);
    else o.Listen(System.Net.IPAddress.Parse(bind), HttpPort);

    o.Limits.MaxRequestBodySize = 2 * 1024; // S11 : corps borné (les payloads sont minuscules)
    o.AddServerHeader = false;
});

// S11 : contrat JSON strict — tout champ inconnu fait échouer la désérialisation.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    o.SerializerOptions.PropertyNameCaseInsensitive = false;
});

builder.Services.AddSingleton<RoomStore>();
builder.Services.AddSingleton<RateLimiter>();

var app = builder.Build();

var rooms = app.Services.GetRequiredService<RoomStore>();
var limiter = app.Services.GetRequiredService<RateLimiter>();

// Le protocole du jeu accepté (doit suivre NetConfig.ProtocolVersion côté jeu)
const int ProtocoleAttendu = 2;

// --- Utilitaires ---
static string IpClient(HttpContext ctx)
{
    // Derrière nginx : la vraie IP est dans X-Forwarded-For (première entrée)
    if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xff) && xff.Count > 0)
    {
        string first = xff.ToString().Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(first)) return first;
    }
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
}

static string NettoyerNom(string? brut)
{
    if (string.IsNullOrWhiteSpace(brut)) return "Salon";
    var sb = new System.Text.StringBuilder(24);
    foreach (char c in brut)
    {
        if (sb.Length >= 24) break;
        if (char.IsControl(c) || c == ',' || c == ';') continue;
        sb.Append(c);
    }
    return sb.Length == 0 ? "Salon" : sb.ToString();
}

// --- Middleware : limite de débit par IP (S12) ---
app.Use(async (ctx, next) =>
{
    // /health n'est pas limité (monitoring)
    if (!ctx.Request.Path.StartsWithSegments("/v1/health") && !limiter.Autoriser(IpClient(ctx)))
    {
        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.Response.WriteAsync("Trop de requetes.");
        return;
    }
    await next();
});

DateTime demarrage = DateTime.UtcNow;

// ========================================================
// ENDPOINTS REST
// ========================================================

// Créer un salon -> renvoie le code + la hostKey secrète
app.MapPost("/v1/rooms", (CreateRoomDto dto, HttpContext ctx) =>
{
    if (dto.Version != ProtocoleAttendu) return Results.BadRequest(new { error = "version" });
    if (dto.MaxPlayers < 2 || dto.MaxPlayers > 20) return Results.BadRequest(new { error = "maxPlayers" });
    if (dto.MapIndex < 0 || dto.MapIndex > 32) return Results.BadRequest(new { error = "mapIndex" });

    string ip = IpClient(ctx);
    if (rooms.RoomsOwnedBy(ip) >= RoomStore.MaxRoomsPerIp) return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    Room room = rooms.Create(NettoyerNom(dto.Name), dto.MaxPlayers, dto.MapIndex, dto.Version, dto.HasPassword, ip);
    Console.WriteLine($"[API] Salon créé {room.Code} '{room.Name}' ({ip}).");
    return Results.Created($"/v1/rooms/{room.Code}", new { code = room.Code, hostKey = room.HostKey });
});

// Heartbeat (toutes les 30 s) : garde le salon vivant + met à jour joueurs/état
app.MapPut("/v1/rooms/{code}", (string code, HeartbeatDto dto, HttpContext ctx) =>
{
    Room? room = rooms.Get(code);
    if (room is null) return Results.NotFound();
    if (!RoomStore.CleValide(room, ctx.Request.Headers["X-Host-Key"])) return Results.Unauthorized(); // S13

    room.Players = Math.Clamp(dto.Players, 0, room.MaxPlayers);
    room.State = dto.State == "playing" ? "playing" : "lobby";
    room.LastSeen = DateTime.UtcNow;
    return Results.NoContent();
});

// Fermer un salon proprement
app.MapDelete("/v1/rooms/{code}", (string code, HttpContext ctx) =>
{
    Room? room = rooms.Get(code);
    if (room is null) return Results.NoContent();
    if (!RoomStore.CleValide(room, ctx.Request.Headers["X-Host-Key"])) return Results.Unauthorized();
    rooms.Remove(code);
    return Results.NoContent();
});

// Résoudre un code -> infos publiques (le client s'en sert avant de percer le NAT)
app.MapGet("/v1/rooms/{code}", (string code) =>
{
    Room? room = rooms.Get(code);
    if (room is null) return Results.NotFound();
    return Results.Ok(new RoomInfoDto(room.Name, room.Players, room.MaxPlayers, room.State, room.Version, room.HasPassword));
});

// Liste des salons publics (navigateur internet — optionnel)
app.MapGet("/v1/rooms", (bool? pub) =>
{
    var liste = rooms.PublicRooms()
        .Select(r => new RoomListItemDto(r.Code, r.Name, r.Players, r.MaxPlayers, r.State))
        .ToArray();
    return Results.Ok(liste);
});

// Télémétrie de hole punching (anonyme) : alimente la décision "relais or not" (D3)
app.MapPost("/v1/telemetry/punch", (PunchTelemetryDto dto) =>
{
    Console.WriteLine($"[TELEMETRY] punch ok={dto.Ok} durée={dto.DurationMs}ms");
    return Results.NoContent();
});

app.MapGet("/v1/health", () => Results.Ok(new
{
    status = "ok",
    rooms = rooms.Count,
    uptimeSec = (int)(DateTime.UtcNow - demarrage).TotalSeconds
}));

// ========================================================
// SERVICES DE FOND : purge TTL + rendez-vous NAT
// ========================================================
var relay = new NatRelay(rooms, UdpPort);
relay.Start();

var minuterie = new Timer(_ =>
{
    int morts = rooms.Sweep();
    limiter.Nettoyer();
    if (morts > 0) Console.WriteLine($"[TTL] {morts} salon(s) expiré(s).");
}, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

app.Lifetime.ApplicationStopping.Register(() => { relay.Stop(); minuterie.Dispose(); });

Console.WriteLine($"[MASTER] REST sur http://localhost:{HttpPort} — UDP NAT sur {UdpPort}.");
app.Run();

// ========================================================
// DTOs (contrats stricts — S11)
// ========================================================
record CreateRoomDto(int Version, string? Name, int MaxPlayers, int MapIndex, bool HasPassword);
record HeartbeatDto(int Players, string State);
record RoomInfoDto(string Name, int Players, int MaxPlayers, string State, int Version, bool HasPassword);
record RoomListItemDto(string Code, string Name, int Players, int MaxPlayers, string State);
record PunchTelemetryDto(bool Ok, int DurationMs);
