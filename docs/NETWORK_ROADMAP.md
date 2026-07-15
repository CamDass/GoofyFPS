# GoofyFPS — WAN Networking Roadmap (v2 — 2026-07-11, **validated**)

Goal: evolve the working LAN listen-server into an internet-playable architecture
(room codes, NAT traversal, persistent lobby, hot-join) for up to 12 friends per match
(configurable to 20), at a native **64 Hz tick**, with internet-grade security hardening.

## Implementation status (2026-07-11)

- **Phase 0 — ✅ DONE, validated** (64 Hz tick, quantized snapshots, interpolation, S1–S7, HUD, NETSIM). Movement feel signed off; capsule-flash bug fixed.
- **Phase 1 — ✅ DONE, LAN-validated.** Session FSM + MatchConfig + PostMatch + return-to-lobby + hot-join baseline + toasts + pause rework. Files: `src/SessionOnline.cs` (+ edits across Packets/Network/Program/Menu/Jeu). Full match loop and mid-match hot-join tested, 0 exceptions.
- **Phase 2 — ✅ DONE, loopback-validated** (real strict-NAT test pending OVH deploy). Master server in `master/` (REST + LiteNetLib NAT punch, hardened deploy files). Game side: `src/MasterClient.cs`, room-code host/join UI. Loopback: joined by code purely via master rendezvous.
- **Phase 3 — ✅ DONE** (kick UI, damage/fire-rate clamps, lenient movement sanity, **reconnect-grace 3.4**; regression-validated). Remaining: relay decision (needs real telemetry, post-deploy).
- **Local polish pass (2026-07-11)** — all locally simulable, deployment frozen:
  - **3.4 Reconnect grace:** host reserves a dropped player's slot+score+skin 30 s (id+token match to re-bind); client auto-reconnects on network Timeout during a match with a "reconnexion" overlay. Grace-expiry purges cleanly.
  - **2.7/2.5 Resiliency:** master-unreachable → clean lobby banner + graceful LAN/direct-IP fallback (no crash); punch-fail → clear strict-NAT message + visible "REJOINDRE (LAN / IP)" path.
  - **2.9 Room passwords:** connect data carries key+password; host validates (reject reason `RejetMotDePasse`); host password box (CreateMatch) + client password box (NetworkHub). `hasPassword` flows to master register.
  - **UI polish:** bordered "RÈGLES DU MATCH" panel (kills/time toggles) in lobby; restructured PostMatch end-screen (winner banner, podium medals, own-row highlight, countdown bar).
  - Build: **0 warnings, 0 errors** (game csproj `NoWarn`s the pre-existing non-nullable/dead-field families — all benign, no logic bugs).
  - New test hooks: `GOOFY_ROOMPASS`, `GOOFY_JOINPASS`, `GOOFY_JOINCODE`, `GOOFY_BIND` (master LAN bind).

Test harness env vars: `GOOFY_AUTOHOST`, `GOOFY_AUTOJOIN`, `GOOFY_JOINIP`, `GOOFY_MATCHTIME`, `GOOFY_SCORELIMIT`, `GOOFY_NETSIM`, `GOOFY_MASTER_URL`, `GOOFY_PUNCH_HOST`, `GOOFY_JOINCODE`.

---

## 0. Validated decisions (owner sign-off 2026-07-11)

| # | Decision | Resolution |
|---|---|---|
| D1 | Master server language | **C#** (single ASP.NET Core + LiteNetLib service) — consolidate the stack |
| D2 | Player cap | **Default 12**, configurable up to 20 (host-uplink warning) |
| D3 | Relay fallback | **Deferred.** NAT hole punching is a high-priority, rock-solid feature; instrument punch success telemetry to decide on a relay later |
| D4 | Phase order | **Confirmed:** Foundation → Lobby/Hot-join (LAN) → Master server (WAN) → Hardening |
| D5 | Tick rate | **64 Hz native.** No low-tick compromise; packet size/serialization engineered to fit residential uplinks (see §4.2) |
| D6 | Trust & security model | Anti-cheat is *not* the focus (friends-only). **Internet-facing security IS**: input sanitization, robust deserialization, no path from network data to host/master compromise, hardened OVH endpoints (see §5) |

---

## 1. Current state analysis

**Stack:** .NET 10 · Raylib-cs 7.0.2 (render) · BepuPhysics 2.4 (physics) · LiteNetLib 2.1.3 (UDP transport + reliability + serialization via `NetPacketProcessor`).

**What already exists and is worth keeping:**

| Piece | Where | Verdict |
|---|---|---|
| Listen server (host = server + client, `isServer`) | `Program.cs` `AllumerMoteurReseau` | ✅ Keep — Task 1's "listen server" is already done |
| UDP transport w/ per-packet delivery semantics (Sequenced for state, ReliableOrdered for events) | `Network.cs` | ✅ Correct choices, keep |
| Server-assigned player IDs, join/lobby/ready/start flow | `Program.cs:601-737` | ✅ Keep, harden |
| LAN broadcast discovery (GOOFY_REQ/RES) | `Program.cs:400-451` | ✅ Keep as offline fallback |
| Direct-IP join | `Menu.cs:1321` | ✅ Keep as NAT-failure fallback |
| Headless-ish test harness (`GOOFY_AUTOHOST` / `GOOFY_AUTOJOIN`) | `Program.cs:1217-1297` | ✅ Gold — extend it |

**Gaps blocking WAN (found in code):**

1. **Send rate = render rate.** `MettreAJourReseauEnJeu` is called every frame (`Jeu.cs:1225`) and the host relays each packet individually to every peer → host outbound = `n(n-1)×60` pkt/s. Snapshot aggregation (§4.2) fixes the packet-count explosion while keeping a high tick.
2. **Cross-player Sequenced drop bug.** The host relays every player's `PlayerStatePacket` on the *same* Sequenced channel per connection (`Network.cs:166`). Sequenced discards late arrivals per channel, so player A's fresh state can be silently dropped because player B's packet bumped the sequence counter. Invisible on LAN, visible under WAN jitter. Fixed by aggregation.
3. **Naive interpolation.** Remote players lerp toward the last packet at fixed 15/s (`Network.cs:339`). Under 80–150 ms jitter + loss this rubber-bands. Needs a time-buffered snapshot interpolator.
4. **Physics tied to render loop.** `simulation.Timestep(1/60)` once per frame (`Jeu.cs:1211`) — correct only because `FPS=60` is hardcoded. Replaced by the unified fixed-tick loop (§4.1).
5. **Join gate = lobby only.** Connections rejected unless `currentState == Lobby` (`Program.cs:463`) → no hot-join. No world baseline exists (walls are only broadcast at placement).
6. **No match end online.** PvP runs forever; quitting goes through `CouperReseau()` → MainMenu (`Menu.cs:1016`). "Return to lobby" has no hook to attach to yet.
7. **Fragile wire details:** lobby list serialized as `"Name,Ready,Id;..."` string (`Program.cs:719-730`); `ShootPacket.WeaponName` is a string; skins ride in every state packet; max players `4` hardcoded twice (`Program.cs:415,463`); static connection key; no version handshake.
8. **No public identity.** Port 7777 hardcoded, host knows only its LAN IPv4; nothing knows the public `IP:port` behind NAT.
9. **Security-relevant (see §5):** host trusts the `PlayerId` field inside packets (impersonation), no NaN/bounds validation on received floats, unbounded wall list (memory exhaustion), unconnected-message handlers always enabled, a malformed packet in `ReadAllPackets` can throw and take the host down.

**Trust model:** client-authoritative movement + shooter-authoritative hits + victim-applied damage — kept by design (D6). Shooter-side hit testing against interpolated remotes *is* the lag compensation ("favor the shooter").

---

## 2. Key architecture rationale (from the review, retained in v2)

### 2.1 Task 1 was ~80 % done — the real work is identity + NAT traversal
The listen server exists. What WAN adds: (a) learning the host's public endpoint, (b) getting UDP through two NATs, (c) a room code instead of an IP.

### 2.2 A pure HTTP directory cannot do `Name → IP:Port` for NAT'd hosts
When the host registers over HTTPS, the API sees its public IP but **not the UDP port mapping** — NATs allocate the external port only when the game's UDP socket itself sends traffic. Directory and rendezvous must therefore cooperate over UDP (STUN principle). LiteNetLib ships this as `NatPunchModule`: host and client each send an introduce request with a token (= our room code); the master mates them, exchanges internal+external endpoints, both punch simultaneously, then the client connects normally.

**Consequence:** the master = one C# service with two faces — HTTPS REST (rooms) + one UDP socket (punch rendezvous) — sharing one in-memory room table. (D1: Go would have required re-implementing LiteNetLib's introduction protocol.)

### 2.3 No prediction/reconciliation rewrite
Server-authoritative movement + CSP + reconciliation would mean moving all player physics onto the host and rewriting the movement feel — wrong cost/benefit for a friends game (D6). Client-auth movement means the local player never waits on the network; 64 Hz snapshots keep *remote* players fresh.

---

## 3. Target architecture

```
                      OVH VPS (subdomain, isolated service)
                ┌────────────────────────────────────────────┐
                │  MASTER: rooms table (in-memory, TTL 90s)  │
                │  ├─ HTTPS REST  (nginx → localhost:5100)   │
                │  └─ UDP :7790   (LiteNetLib NatPunchModule)│
                └───────▲──────────────────▲─────────────────┘
        register/heartbeat│                │resolve code + punch req
              (HTTP+UDP)  │                │      (HTTP+UDP)
                ┌─────────┴───────┐   ┌────┴──────────┐
                │ HOST (listen    │◄──┤ CLIENT(S)     │  UDP after punch:
                │ server, :7777)  │──►│               │  direct P2P game traffic
                └─────────────────┘   └───────────────┘
   Game session (host authoritative for session/relay, clients authoritative for own movement)
   Host session FSM:  Lobby → Loading → Playing → PostMatch → Lobby (loop)
```

---

## 4. Timing & wire architecture (64 Hz native — D5)

### 4.1 Unified fixed tick
One fixed simulation/network tick at **64 Hz** (`dt = 1/64 s`), driven by an accumulator decoupled from render FPS ("fix your timestep"):

- **Physics:** `simulation.Timestep(1/64)` inside the accumulator (replaces the per-frame call at `Jeu.cs:1211`). Physics and net tick share the clock — no beat-frequency judder between simulation and snapshots.
- **Net out:** every tick, client samples its own state → sends to host; host assembles + broadcasts one `WorldSnapshot`.
- **Render:** independent (user FPS setting / vsync). Local view optionally alpha-blends between the last two physics states (polish task, *should*).
- **Events bypass the tick:** fire/hit/death/wall packets are sent **immediately** on the action (ReliableOrdered), never batched to the next tick — trigger-pull latency is 0 added ms regardless of tick phase.
- ⚠️ Retune check: moving fixed dt from 1/60 → 1/64 can subtly change jump/movement integration; a movement-feel regression pass is a Phase 0 exit criterion. Documented fallback if feel can't be matched: keep physics at 60 Hz, net at 64 Hz sampling latest state (accepted duplicate snapshots ~6 % of ticks).

### 4.2 Packet budget at 64 Hz (quantization is mandatory)
Per-player snapshot entry, quantized (**~14 B**): `PlayerId u8 · Pos 3×i16` (1 cm precision, ±327 m map bounds) `· Yaw u16 · Pitch i8 · Lean i8 · Health u8 · WeaponIndex u8 · Flags u8` (aiming/reloading/…).

`WorldSnapshot` = ~34 B fixed cost (UDP/IP 28 + LiteNetLib ~4 + `ServerTick u16`) + n×14. Host upload at 64 Hz:

| Players | Snapshot size | Host upload (state) | Verdict |
|---|---|---|---|
| 4 | 90 B | ~17 kB/s (0.14 Mbit/s) | trivial |
| 8 | 146 B | ~65 kB/s (0.5 Mbit/s) | fine everywhere |
| **12 (default)** | 202 B | **~142 kB/s (1.15 Mbit/s)** | fine on any modern uplink |
| 20 (max config) | 314 B | ~382 kB/s (3.1 Mbit/s) | needs solid fiber → uplink warning in UI |

Client upload: ~51 B × 64 ≈ **3.3 kB/s** — trivial. Host download at 12 players: ~36 kB/s.
The net-stats HUD (task 0.11) displays live in/out so saturation is visible, not guessed.

### 4.3 Interpolation
Per remote player: ring buffer of `(tick, snap)`; render at `newestTick − INTERP_TICKS` with `INTERP_TICKS = 3` → **~47 ms** display delay (vs ~100 ms at 20 Hz — the payoff of D5). Shortest-arc angle lerp; extrapolate ≤ 150 ms on starvation then freeze; hard-snap when displacement > 5 m (respawn/teleport). Tick comparison uses wraparound-safe `u16` math.

---

## 5. Security architecture (D6 — first-class requirement)

**Threat model.** Internet-exposed surfaces: ① host's game UDP port (LiteNetLib parsing + our packet handlers), ② master REST API, ③ master UDP rendezvous, ④ the client's outbound connections. Attacker = arbitrary internet stranger (port scans, malformed traffic, API abuse), *not* a cheating friend.

**Memory safety note:** all parsing paths (our code, LiteNetLib) are managed C# — classic buffer overflows are not reachable from network input. The csproj's `AllowUnsafeBlocks` exists for Raylib interop only; **rule: no network data may ever flow into an `unsafe` block** (audited in Phase 0). The realistic risks are: crash-DoS via malformed packets, resource exhaustion, impersonation, logic abuse, and API abuse — each addressed below and as numbered tasks in the phases.

### 5.1 Game host (protocol) hardening — tasks live in Phases 0–1
- **S1. Crash-proof deserialization:** wrap `netProcessor.ReadAllPackets` per-peer; any exception → log + disconnect that peer, never take the process down.
- **S2. Sender identity binding:** the host derives the acting `PlayerId` from the *sending peer* (reverse `peersParId` map), never from a field inside the packet → impersonation (state/shots/deaths as someone else) impossible. Packet `PlayerId` fields kept only for host→client fan-out.
- **S3. Field validation on every receive:** floats checked `IsFinite` (a NaN position propagates into Bepu/rendering = crash/hang vector) and clamped to map bounds; indices bounds-checked (weapon, map); strings length-capped (names ≤ 24) + charset-whitelisted (strip control chars & protocol separators).
- **S4. Resource caps:** max walls per player + global (bounds `listeMur` memory); max remote-shot visuals; max pending reliable queue per peer.
- **S5. Per-peer packet budget:** cap packets/s per channel (e.g. state ≤ 80/s, events ≤ 30/s); sustained excess → disconnect (flood protection).
- **S6. Attack-surface minimization:** `BroadcastReceiveEnabled` / `UnconnectedMessagesEnabled` on **only** in LAN-discovery states (currently always on); unconnected handlers validate magic + length strictly and answer nothing else.
- **S7. Handshake gate:** connection key = `"GoofyFPS/{PROTOCOL_VERSION}"`; wrong key/version rejected before any game logic; `NetManager` connection cap = room cap.
- **S8. Accepted risk (documented):** game UDP payloads are plaintext (positions/shots — not sensitive). All master-server traffic is TLS. Optional room password (task 2.9) gates *who* can join.

### 5.2 Master server hardening — tasks live in Phase 2
- **S9. Process isolation:** dedicated non-root user; systemd `NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`, `MemoryMax`, `Restart=on-failure`. Zero filesystem/port overlap with the portfolio site.
- **S10. TLS-only REST** behind nginx (existing certbot), HSTS; Kestrel bound to `localhost:5100` only.
- **S11. Strict input contract:** JSON body ≤ 1 KB; unknown fields rejected; name ≤ 24 chars whitelisted charset; room code validated against the exact alphabet/length before any lookup.
- **S12. Rate limiting, two layers:** nginx `limit_req` per IP + in-app token bucket; ≤ 5 rooms per IP; heartbeat window enforced.
- **S13. Auth:** `hostKey` = 128-bit from `RandomNumberGenerator`; constant-time comparison; never logged. Room mutation (heartbeat/delete) requires it.
- **S14. Rendezvous anti-hijack:** host punch registration token carries a `hostKey` fragment — only the legitimate host can (re)bind its endpoint to a room code (prevents an attacker registering as "host" of someone else's room = MITM). Client-side punch requests need only the code (joining is meant to be open; password gates the actual game connect). Malformed punch tokens ignored silently; per-IP rate limit on introduce requests.
- **S15. Data minimization:** RAM-only store, no PII, transient IPs only; minimal structured logs with rotation; `/health` exposes counts, never addresses.
- **S16. Supply chain:** pin package versions; check LiteNetLib advisories at implementation time; version handshake (S7) doubles as a force-upgrade lever if a protocol security fix ships.

### 5.3 Security verification — in the test plan (§10)
Malformed-packet fuzz harness against the host socket (must never crash), API abuse suite (oversized bodies, bad codes, missing auth, rate-limit floods), and a checklist review of S1–S16 before the Phase 2 exit gate.

---

## 6. Phase 0 — Netcode foundation (LAN, no visible features) — **must**

| # | Micro-task | Notes / files |
|---|---|---|
| 0.1 | `NetConfig` static class: `PORT`, `MAX_PLAYERS=12` (replaces hardcoded 4 at `Program.cs:415,463`; configurable ≤ 20), `PROTOCOL_VERSION`, `TICK_RATE=64`, `INTERP_TICKS=3`, `PUNCH_PORT`, master URL, resource caps (S4) | new `src/NetConfig.cs` |
| 0.2 | Unified 64 Hz fixed-tick accumulator driving physics **and** net send (§4.1); render decoupled | `Jeu.cs:1211`, `Jeu.cs:1225` |
| 0.3 | Movement-feel regression pass at dt = 1/64 (jump height, wall-run, air control vs current build); apply fallback of §4.1 only if feel can't be matched | exit criterion |
| 0.4 | Split identity from state: `PlayerInfoPacket {Id, Name, SkinColor, SkinHat, SkinFace}` on join/change; skins removed from per-tick state | `Packets.cs`, `Network.cs` |
| 0.5 | `ClientStatePacket` v2 as `INetSerializable` struct, quantized per §4.2 (~20 B on the wire incl. client tick) | |
| 0.6 | Host-side **`WorldSnapshotPacket`** (§4.2): one broadcast per tick, includes host's own state (removes echo-filter hacks; fixes the Sequenced cross-drop bug) | `Network.cs` |
| 0.7 | Snapshot interpolation per §4.3, replacing the fixed lerp | `Network.cs:339` |
| 0.8 | **S1 + S2 + S3**: crash-proof `ReadAllPackets`, sender-ID binding on the host, field validation (NaN/bounds/strings) on every packet handler | `Network.cs`, `Program.cs:550` |
| 0.9 | **S6 + S7**: unconnected messages only in LAN-browser states; versioned connection key with clean client-side rejection message | `Program.cs:390,466` |
| 0.10 | Structured `LobbyStatePacket` (proper array serialization; kill the `"Name,Ready,Id;…"` string) + `ShootPacket` uses `byte WeaponIndex` | `Program.cs:719`, `Packets.cs:69` |
| 0.11 | Net debug HUD (extend F3): ping, in/out kB/s + pkt/s (`EnableStatistics`), snapshot age, interp buffer depth, tick-time | `Menu.cs` HUD |
| 0.12 | `GOOFY_NETSIM=lat:jitter:loss` env var → LiteNetLib `SimulateLatency`/`SimulatePacketLoss` (DEBUG builds) | `Program.cs` |
| 0.13 | Audit: no network data reaches `unsafe` code (§5 memory-safety rule) | csproj / `Rendu.cs` interop |

**Exit criteria:** 2 LAN instances under simulated 150 ms ± 30 ms + 5 % loss: remote players smooth, hits register, HUD confirms ≤ budget (§4.2). Movement feel signed off at 1/64 dt. Fuzz harness (10.4) runs 10 min without host crash. Autohost/autojoin harness still passes.

---

## 7. Phase 1 — Session state machine, lobby persistence, hot-join (LAN) — **must**

| # | Micro-task | Notes |
|---|---|---|
| 1.1 | Host-authoritative session FSM `{Lobby, Loading, Playing, PostMatch}`; transitions broadcast reliable; client `GameState` follows | supersedes ad-hoc `currentState` flips |
| 1.2 | `MatchConfig {scoreLimit (déf. 20), timeLimitSec (déf. 600), 0=∞}` — host lobby UI + carried in `StartGamePacket` | the missing match-end prerequisite |
| 1.3 | Match clock: remaining seconds piggybacked in `WorldSnapshot` header (`u16`) | |
| 1.4 | End detection on host (kill tally + timer) → `MatchEndPacket {winnerId, scores[]}` | `Network.cs:239` |
| 1.5 | PostMatch scoreboard (10 s) → `ReturnToLobbyPacket` → everyone in Lobby, **sockets stay up** | |
| 1.6 | Return-to-lobby world cleanup: walls/shots/explosions cleared, ready flags reset, scores archived to "last match" panel, `remotePlayers` kept with `HasState=false` | reuse `ChargerMapReseau` cleanup |
| 1.7 | Pause menu online rework: **"RETOUR AU SALON"** (client: stays connected, lobby shows "match en cours — REJOINDRE" via hot-join; host: triggers MatchEnd for all) + **"QUITTER LA SESSION"** (disconnect → NetworkHub). Fixes `Menu.cs:1016` killing the session | |
| 1.8 | Hot-join gate: accept during `Playing` (relax `Program.cs:463`) if under cap + version OK; peer flagged `joining`, no damage routed until ready | |
| 1.9 | Baseline sync (ReliableOrdered): `MatchInfoPacket {mapIndex, config, clock}` → client loads map → `ClientReadyPacket` → host streams `WallBatchPacket` (≤ 20 walls/pkt, total bounded by S4), `ScoreSnapshotPacket`, `PlayerInfoPacket`×n → `BaselineEndPacket {spawnPos}` → client spawns | ordering guaranteed by channel |
| 1.10 | Host picks hot-join spawn farthest from enemies; ships it in `BaselineEnd` | |
| 1.11 | Join/leave toasts in-game (names from `PlayerInfoPacket`) | |
| 1.12 | Edge cases: disconnect mid-baseline (abort, free ID); join during `Loading` → reject `BUSY` + client auto-retry 2 s; duplicate pseudo → suffix `#2` | |
| 1.13 | **S4 + S5**: resource caps live (walls, queues) + per-peer packet budgets with disconnect on sustained excess | |
| 1.14 | Test harness: `GOOFY_AUTOJOIN` mid-match variant | |

**Exit criteria:** host + 2 clients play to score limit → scoreboard → lobby → new map → replay, no restart. 3rd instance hot-joins mid-match and sees every wall. A client leaves to lobby and rejoins the running match. Flooding instance gets disconnected, match unaffected.

---

## 8. Phase 2 — Master server + NAT traversal (WAN) — **must**

Hole punching is the flagship deliverable of this phase (D3): engineered with explicit state machines, retries, keepalives, and telemetry — not a best-effort bolt-on.

### 8.1 Master service (C# — D1)
- In-memory room table, TTL sweep (expire 90 s without heartbeat), no DB.
- Room code: 5 chars from `23456789ABCDEFGHJKMNPQRSTUVWXYZ` (no ambiguous glyphs), server-generated, collision-checked (~33 M combos).

**REST endpoints (HTTPS via nginx, isolated subdomain):**

| Endpoint | Auth | Body / returns |
|---|---|---|
| `POST /v1/rooms` | — | `{version, name, maxPlayers, mapIndex, hasPassword}` → `201 {code, hostKey}` |
| `PUT /v1/rooms/{code}` (heartbeat 30 s) | `X-Host-Key` | `{players, state: lobby\|playing}` → `204` |
| `DELETE /v1/rooms/{code}` | `X-Host-Key` | `204` |
| `GET /v1/rooms/{code}` | — | `{name, players, maxPlayers, state, version, hasPassword}` or `404` |
| `POST /v1/telemetry/punch` | — | `{ok, durationMs}` — anonymous punch outcome (feeds the D3 relay decision) |
| `GET /v1/rooms?public=1` | — | public listing (internet browser — optional) |
| `GET /v1/health` | — | uptime/room count |

**UDP rendezvous (same process, `NatPunchModule`, `:7790/udp`):**
- Host: `NatPunchEnabled=true`; on room creation **and every 25 s** (keeps the NAT mapping alive): `SendNatIntroduceRequest(master, "H:{code}:{hostKeyFragment}")` (S14).
- Client: after REST resolve: `SendNatIntroduceRequest(master, "C:{code}")`.
- Master `NatIntroductionRequested`: `H:` → verify fragment, bind/refresh host int+ext endpoints; `C:` → look up host → `NatIntroduce(hostInt, hostExt, clientInt, clientExt, token)`.
- Both punch; client `OnNatIntroductionSuccess` → `Connect(endpoint, key)`. Same-LAN pairs work via the internal endpoints.

### 8.2 Game integration & punch robustness

| # | Micro-task |
|---|---|
| 2.1 | Host: lobby creation → `POST /rooms`, **room code displayed big in the lobby UI**; heartbeat timer; `DELETE` on close; re-`POST` transparently if the master rebooted (404 on heartbeat) |
| 2.2 | Host punch keepalive loop (25 s re-introduce) + re-register after network change (send failure streak) |
| 2.3 | Client "REJOINDRE PAR CODE" UI → `GET /rooms/{code}` (exists? full? version? password?) → punch → connect; explicit error states: *code inconnu / salon plein / version différente / mot de passe / NAT strict* |
| 2.4 | Client punch state machine: introduce → await 3 s → retry ×3 (fresh socket on final retry) → try internal & external endpoints in parallel → on success `Connect`; every outcome reported via `POST /telemetry/punch` |
| 2.5 | Punch-failure UX: dialog explaining strict NAT, offering the kept direct-IP path + port-forward hint |
| 2.6 | Keep LAN broadcast discovery untouched (offline play; unconnected handlers only active there — S6) |
| 2.7 | Master unavailable ≠ game broken: hosting falls back to LAN + direct-IP with a warning banner |
| 2.8 | *(could)* UPnP port-map attempt on host start (Mono.Nat) → direct connectivity without punch when the router allows |
| 2.9 | *(should)* Room password: optional, sent in LiteNetLib connect data, host compares (S8) |
| 2.10 | *(could)* IPv6 dual-stack: if both peers have public IPv6, connect directly, no punch needed |

### 8.3 Deployment (OVH) — implements S9–S16

| # | Micro-task |
|---|---|
| 2.11 | Hardened systemd unit per S9; dedicated user; self-contained publish |
| 2.12 | nginx: `gfps-api.<domain>` → `localhost:5100`, TLS + HSTS (S10), `limit_req` (S12); firewall opens `7790/udp` only |
| 2.13 | In-app rate limiting + input contract (S11–S13); rendezvous anti-hijack + silent drop of malformed tokens (S14) |
| 2.14 | Logging/metrics per S15; deploy script (rsync + restart) documented in the repo |

**Exit criteria:** two machines on different home ISPs join by room code, no port forwarding. Phone-hotspot client (CGNAT) fails *gracefully* through the 2.4 state machine and reports telemetry. Master survives host crash (TTL expiry) and its own restart (2.1 re-register). API abuse suite (10.5) passes. S1–S16 checklist reviewed.

---

## 9. Phase 3 — Polish & optional integrity — **should/could**

| # | Micro-task |
|---|---|
| 3.1 | *(should)* Host kick UI (lobby list + scoreboard) → `Disconnect(reason)` |
| 3.2 | *(should, cheap integrity)* Damage clamp to weapon-table max + per-weapon fire-rate window on the host; drop + log violations. Not anti-cheat (D6) — it bounds the blast radius of a modified/buggy client |
| 3.3 | *(could)* Movement sanity counter (speed/teleport thresholds) → warn → kick |
| 3.4 | *(could)* Reconnect grace (rejoin with same ID ≤ 30 s keeps score) — hot-join already covers the coarse case |
| 3.5 | **Relay decision review:** after ~1 month of punch telemetry, decide relay build/no-build with real failure-rate data (D3) |

**Out of scope (deliberate):** host migration, dedicated cloud game servers, game-traffic encryption (S8), full server-authoritative physics / anti-cheat.

---

## 10. Test plan (transversal)

1. Every phase: 2–3 local instances via `GOOFY_AUTOHOST`/`GOOFY_AUTOJOIN` + `GOOFY_NETSIM` matrices (0 ms · 80 ms · 150 ms ± 30 ms · 5 % loss).
2. Phase 1: scripted full loop (lobby → match → limit → lobby → new map) + mid-match join.
3. Phase 2: staging on OVH; real-world matrix: same LAN, two ISPs, phone hotspot (CGNAT), VPN.
4. **Security — fuzz harness:** standalone tool spraying malformed/truncated/random UDP at the host socket and the rendezvous port; acceptance = zero crashes, zero handler exceptions escaping S1, memory stable (S4).
5. **Security — API abuse suite:** oversized bodies, invalid codes, missing/wrong `hostKey`, rate-limit floods, unknown fields → all rejected per S11–S13.
6. Regression guard: solo mode byte-for-byte unaffected (all changes gated behind `isOnline`).
7. Bandwidth verification: HUD-measured host uplink at 4/8/12 players vs the §4.2 table.
