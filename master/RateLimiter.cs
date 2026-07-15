using System.Collections.Concurrent;

namespace GoofyMaster;

// ========================================================
// LIMITEUR DE DÉBIT PAR IP (S12 : seau à jetons)
// Deuxième ligne de défense derrière le limit_req de nginx : borne les requêtes
// applicatives par IP pour qu'un flood ne noie ni la table des salons ni le CPU.
// ========================================================
public sealed class RateLimiter
{
    private sealed class Bucket
    {
        public double Tokens;
        public DateTime Last;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly double _capacity;
    private readonly double _refillPerSec;

    public RateLimiter(double capacity = 20, double refillPerSec = 5)
    {
        _capacity = capacity;
        _refillPerSec = refillPerSec;
    }

    // true = requête autorisée ; false = trop de requêtes (429)
    public bool Autoriser(string ip)
    {
        DateTime maintenant = DateTime.UtcNow;
        Bucket b = _buckets.GetOrAdd(ip, _ => new Bucket { Tokens = _capacity, Last = maintenant });
        lock (b)
        {
            double ecoule = (maintenant - b.Last).TotalSeconds;
            b.Tokens = Math.Min(_capacity, b.Tokens + ecoule * _refillPerSec);
            b.Last = maintenant;
            if (b.Tokens < 1) return false;
            b.Tokens -= 1;
            return true;
        }
    }

    // Purge des seaux inactifs (évite une croissance mémoire infinie)
    public void Nettoyer()
    {
        DateTime limite = DateTime.UtcNow.AddMinutes(-10);
        foreach (var kv in _buckets)
            if (kv.Value.Last < limite) _buckets.TryRemove(kv.Key, out _);
    }
}
