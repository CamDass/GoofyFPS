using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace GoofyMaster;

// ========================================================
// LA TABLE DES SALONS (en mémoire, sans base de données — S15 : rien n'est persisté)
// Chaque salon expire après TTL secondes sans battement de cœur (heartbeat).
// Thread-safe : accédé à la fois par les requêtes HTTP et par le rendez-vous UDP.
// ========================================================
public sealed class Room
{
    public required string Code { get; init; }
    public required string HostKey { get; init; }   // secret 128 bits : preuve de propriété (S13)
    public string Name { get; set; } = "Salon";
    public int MaxPlayers { get; set; } = 12;
    public int Players { get; set; } = 1;
    public int MapIndex { get; set; }
    public int Version { get; set; }
    public bool HasPassword { get; set; }
    public string State { get; set; } = "lobby";     // "lobby" | "playing"
    public string OwnerIp { get; init; } = "";        // pour la limite de salons par IP
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    // Endpoints appris via le rendez-vous NAT (renseignés par les paquets UDP de l'hôte)
    public IPEndPoint? HostInternal { get; set; }
    public IPEndPoint? HostExternal { get; set; }
}

public sealed class RoomStore
{
    // Alphabet sans glyphes ambigus (pas de 0/O, 1/I/L) -> codes faciles à lire et dicter
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeLength = 5;                 // 31^5 ≈ 28,6 millions de combinaisons
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(90);
    public const int MaxRoomsPerIp = 5;               // S12 : anti-spam de création

    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _rooms.Count;

    public bool IsValidCode(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != CodeLength) return false;
        foreach (char c in code) if (Alphabet.IndexOf(char.ToUpperInvariant(c)) < 0) return false;
        return true;
    }

    public Room? Get(string code)
    {
        if (!IsValidCode(code)) return null;
        return _rooms.TryGetValue(code, out Room? r) ? r : null;
    }

    public IEnumerable<Room> PublicRooms() => _rooms.Values.Where(r => !r.HasPassword).ToArray();

    public int RoomsOwnedBy(string ip) => _rooms.Values.Count(r => r.OwnerIp == ip);

    public Room Create(string name, int maxPlayers, int mapIndex, int version, bool hasPassword, string ownerIp)
    {
        string code = GenererCodeUnique();
        string hostKey = GenererHostKey();
        Room room = new()
        {
            Code = code,
            HostKey = hostKey,
            Name = name,
            MaxPlayers = maxPlayers,
            MapIndex = mapIndex,
            Version = version,
            HasPassword = hasPassword,
            OwnerIp = ownerIp,
        };
        _rooms[code] = room;
        return room;
    }

    public bool Remove(string code) => _rooms.TryRemove(code, out _);

    // Purge des salons morts (appelée périodiquement par le service de fond)
    public int Sweep()
    {
        DateTime maintenant = DateTime.UtcNow;
        int supprimes = 0;
        foreach (var kv in _rooms)
        {
            if (maintenant - kv.Value.LastSeen > Ttl && _rooms.TryRemove(kv.Key, out _)) supprimes++;
        }
        return supprimes;
    }

    private string GenererCodeUnique()
    {
        Span<byte> buffer = stackalloc byte[CodeLength];
        Span<char> code = stackalloc char[CodeLength]; // hors boucle (CA2014 : pas de stackalloc répété)
        for (int tentative = 0; tentative < 16; tentative++)
        {
            RandomNumberGenerator.Fill(buffer);
            for (int i = 0; i < CodeLength; i++) code[i] = Alphabet[buffer[i] % Alphabet.Length];
            string s = new(code);
            if (!_rooms.ContainsKey(s)) return s;
        }
        // Collision improbable 16 fois de suite : on rallonge avec un suffixe aléatoire
        return new string(Enumerable.Range(0, CodeLength).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
    }

    private static string GenererHostKey()
    {
        Span<byte> key = stackalloc byte[16]; // 128 bits
        RandomNumberGenerator.Fill(key);
        return Convert.ToHexString(key);
    }

    // Comparaison à temps constant (S13 : pas de fuite temporelle sur la clé)
    public static bool CleValide(Room room, string? fournie)
    {
        if (string.IsNullOrEmpty(fournie)) return false;
        byte[] a = System.Text.Encoding.ASCII.GetBytes(room.HostKey);
        byte[] b = System.Text.Encoding.ASCII.GetBytes(fournie);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
