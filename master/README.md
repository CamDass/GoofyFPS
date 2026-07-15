# GoofyMaster — annuaire de salons + rendez-vous NAT

Service **unique** (C# / ASP.NET Core + LiteNetLib) qui joue deux rôles pour le
multijoueur WAN de GoofyFPS, **sans jamais héberger de partie** :

1. **Annuaire REST (HTTPS)** — résout un *code de salon* → infos de connexion.
2. **Rendez-vous NAT (UDP)** — orchestre le *hole punching* entre l'hôte et le client
   (`NatPunchModule`), car un annuaire HTTP seul ne peut pas connaître le mapping
   de port UDP derrière un NAT résidentiel (principe STUN).

Tout est **en mémoire** (aucune base de données), les salons expirent au bout de
**90 s** sans heartbeat. Voir `docs/NETWORK_ROADMAP.md` §8 pour l'architecture.

## Lancer en local (dev)

```bash
cd master
dotnet run
# REST : http://localhost:5100   |   UDP NAT : 7790
curl http://localhost:5100/v1/health
```

## API REST (`/v1`)

| Méthode & route | Auth | Corps → Réponse |
|---|---|---|
| `POST /v1/rooms` | — | `{version,name,maxPlayers,mapIndex,hasPassword}` → `201 {code,hostKey}` |
| `PUT /v1/rooms/{code}` | `X-Host-Key` | `{players,state}` → `204` (heartbeat, 30 s) |
| `DELETE /v1/rooms/{code}` | `X-Host-Key` | `204` |
| `GET /v1/rooms/{code}` | — | infos du salon ou `404` |
| `GET /v1/rooms?pub=1` | — | liste des salons publics |
| `POST /v1/telemetry/punch` | — | `{ok,durationMs}` → `204` (stats hole punching) |
| `GET /v1/health` | — | `{status,rooms,uptimeSec}` |

- **Codes de salon** : 5 caractères de `23456789ABCDEFGHJKMNPQRSTUVWXYZ` (pas de glyphe
  ambigu), ~28,6 M combinaisons, vérifiés uniques.
- **hostKey** : secret 128 bits rendu à la création ; requis pour heartbeat/suppression,
  comparé à temps constant (S13). **Ne jamais logguer.**

## Sécurité (S9–S16 de la roadmap)

- **S10/S11** : Kestrel bind `localhost:5100` uniquement (TLS assuré par nginx) ; corps
  ≤ 2 Ko ; contrat JSON strict → tout champ inconnu = `400` (`UnmappedMemberHandling.Disallow`).
- **S12** : double limite de débit (nginx `limit_req` + seau à jetons applicatif par IP) ;
  ≤ 5 salons par IP.
- **S13** : hostKey cryptographique, comparaison à temps constant.
- **S14** : anti-hijack du rendez-vous — l'endpoint d'un salon ne peut être (re)lié que par
  un hôte présentant le bon fragment de hostKey ; jetons malformés ignorés en silence ;
  limite par IP sur les demandes d'introduction.
- **S9/S15** : `systemd` durci (non-root, `ProtectSystem=strict`, `MemoryDenyWriteExecute`,
  RAM only, pas de PII) ; logs sans adresses.

## Déploiement OVH

Voir `deploy/` :
- `goofy-master.service` — unité systemd durcie (utilisateur `goofymaster` dédié, isolé du portfolio).
- `nginx-goofy-master.conf` — reverse proxy TLS + HSTS + `limit_req`, sur un **sous-domaine dédié**.
- `deploy.sh` — publie un binaire self-contained linux-x64, rsync + restart.

Pare-feu : ouvrir **443/tcp** (proxy) et **7790/udp** (rendez-vous NAT). Rien d'autre.
