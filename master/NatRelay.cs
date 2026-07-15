using System.Collections.Concurrent;
using System.Net;
using LiteNetLib;

namespace GoofyMaster;

// ========================================================
// LE RENDEZ-VOUS NAT (le cœur du hole punching — D3)
// ========================================================
// Un annuaire HTTP seul NE PEUT PAS résoudre "code -> IP:port de jeu" : le NAT
// n'alloue le port UDP externe de l'hôte QUE lorsque sa socket de jeu émet du
// trafic. Ce module observe donc les endpoints réels via les paquets UDP des
// pairs (principe STUN) et marie hôte + client (NatPunchModule de LiteNetLib).
//
// Jeton d'introduction :
//   "H:<code>:<fragmentHostKey>"  (hôte : prouve qu'il possède le salon -> S14)
//   "C:<code>"                     (client : le mot de passe éventuel garde l'accès jeu)
public sealed class NatRelay : INatPunchListener
{
    private readonly NetManager _net;
    private readonly RoomStore _rooms;
    private readonly RateLimiter _introLimiter = new(capacity: 30, refillPerSec: 10);
    private volatile bool _running;

    // Anti-hijack : on ne lie l'endpoint d'un salon qu'à un hôte présentant le bon
    // fragment de hostKey. On garde le fragment attendu par code (rempli au 1er H: valide
    // dont le fragment matche la hostKey réelle du salon).
    private readonly ConcurrentDictionary<string, byte> _codesVerrouilles = new(StringComparer.OrdinalIgnoreCase);

    public NatRelay(RoomStore rooms, int udpPort)
    {
        _rooms = rooms;
        _net = new NetManager(new EventBasedNetListener())
        {
            NatPunchEnabled = true,
            UnconnectedMessagesEnabled = false,
            BroadcastReceiveEnabled = false,
        };
        _net.NatPunchModule.Init(this);
        UdpPort = udpPort;
    }

    public int UdpPort { get; }

    public void Start()
    {
        if (!_net.Start(UdpPort))
            throw new InvalidOperationException($"Impossible d'ouvrir le port UDP {UdpPort} pour le rendez-vous NAT.");
        _running = true;
        Task.Run(BoucleEvenements);
        Console.WriteLine($"[NAT] Rendez-vous UDP actif sur le port {UdpPort}.");
    }

    public void Stop()
    {
        _running = false;
        _net.Stop();
    }

    private async Task BoucleEvenements()
    {
        while (_running)
        {
            _net.NatPunchModule.PollEvents();
            await Task.Delay(15); // ~64 Hz, largement suffisant pour un rendez-vous
        }
    }

    // Appelé quand un pair (hôte OU client) demande une introduction.
    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        // S14 : anti-flood par IP sur les demandes d'introduction
        if (!_introLimiter.Autoriser(remoteEndPoint.Address.ToString())) return;
        if (string.IsNullOrEmpty(token) || token.Length > 64) return;

        try
        {
            if (token.StartsWith("H:", StringComparison.Ordinal))
            {
                // Format "H:<code>:<fragmentHostKey>"
                string[] parts = token.Split(':');
                if (parts.Length != 3) return;
                string code = parts[1];
                string fragment = parts[2];

                Room? room = _rooms.Get(code);
                if (room is null) return;

                // Anti-hijack (S14) : seul un hôte connaissant le début de la hostKey
                // peut (re)lier l'endpoint du salon. Un attaquant ne peut pas se faire
                // passer pour l'hôte d'un salon qu'il ne possède pas.
                if (!room.HostKey.StartsWith(fragment, StringComparison.Ordinal) || fragment.Length < 8) return;

                room.HostInternal = localEndPoint;
                room.HostExternal = remoteEndPoint;
                room.LastSeen = DateTime.UtcNow;
                _codesVerrouilles[code] = 1;
            }
            else if (token.StartsWith("C:", StringComparison.Ordinal))
            {
                // Format "C:<code>"
                string code = token.Substring(2);
                Room? room = _rooms.Get(code);
                if (room?.HostInternal is null || room.HostExternal is null) return;

                // On marie l'hôte et le client : chacun reçoit les endpoints interne+externe
                // de l'autre et perce le NAT simultanément. Le jeton final = le code
                // (le jeu s'en sert pour corréler l'événement de succès).
                _net.NatPunchModule.NatIntroduce(
                    room.HostInternal, room.HostExternal,
                    localEndPoint, remoteEndPoint,
                    code);
                Console.WriteLine($"[NAT] Introduction {code} : hôte {room.HostExternal} <-> client {remoteEndPoint}");
            }
        }
        catch (Exception ex)
        {
            // Jeton forgé / endpoints incohérents : on ignore silencieusement (S14)
            Console.WriteLine($"[NAT] Demande ignorée ({ex.GetType().Name}).");
        }
    }

    // Côté serveur maître on n'est jamais l'aboutissement d'un punch : rien à faire.
    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token) { }
}
