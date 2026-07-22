# PvP Demo: 2-Player "Skirmish 2v2"

Two players fight the pinned symmetric 2v2 frigate skirmish
(`pvp-skirmish-2v2`, scenario v2): identical fleets mirrored across the
board under a live cross-breeze, modifier set `{ chainShot,
mutualRamming, ramming, windMovement }`. Each turn both captains author orders (turn delta,
speed delta, optional broadside target, round/chain ammo), the server
resolves exactly one deterministic engine turn once both sides are in,
and the resolved turn plays back through the spectator renderer. Ships
really move: heading and speed buy position, closing raises everyone's
hit chance and damage, sailing within 25 units rams — and colliding with a ship
that is itself under way hurts both hulls by each other's momentum, so
head-on charges are mutual destruction, not a first-strike prize; only
ramming a stationary target is one-sided (with recoil). The wind arcs
make some headings faster than others. Win by sinking both enemy frigates; mutual
annihilation or hitting the 20-turn limit is a draw.

Two ways to play:

- **Hot-seat** (one machine, slice 1): `Assets/Scenes/PvPHotseatDemo.unity`.
- **Netplay** (two clients, this doc's focus): `Assets/Scenes/PvPNetplayDemo.unity`
  against the server-authoritative `pvp_api` match routes.

## 1. Run the backend

```bash
docker compose up -d          # local Postgres (Docker Desktop must be running)
npm install
npm run migrate               # applies the Match/MatchParticipant migration
npm run seed                  # seeds feature flags, including pvp_api
npm run dev                   # Fastify server on http://localhost:4500
```

## 2. Two clients on one machine

Each running client signs in as its own guest player. The demo pairing is
the Editor plus a standalone build:

1. **Editor client**: open `unity/` in Unity `2022.3.62f3` (first open
   only: import TMP Essentials when prompted), open
   `Assets/Scenes/PvPNetplayDemo.unity`, press Play.
2. **Standalone client**: build once via
   `Assets → Armada → Build PvP Netplay Standalone (Win64)` (or batch:
   `Unity.exe -batchmode -quit -projectPath unity -executeMethod
   PvpNetplayBuildScript.Build`), then run
   `build/PvPNetplayDemo/PvPNetplayDemo.exe`.

Both clients default to `http://localhost:4500`
(`Assets/Scenes/PvPNetplayClientConfig.asset`).

## 3. Play a match

1. Client A clicks **Create Match** and reads the 8-character match code
   off the status line.
2. Client B types the code into the input field and clicks **Join Match**.
3. Each turn, both clients author orders for their own two frigates:
   - **Next Ship** switches between your frigates (`>` marks the active one).
   - **Turn < / Turn >** adjust heading ±15° per press (clamped ±90°).
   - **Speed − / Speed +** adjust speed per press (clamped ±2).
   - **Target** cycles your broadside target through living enemy ships
     and back to hold-fire; **Ammo** toggles round/chain shot.
   - **Confirm Orders** submits your side. The HUD shows
     "waiting for the enemy captain" until the opponent submits too.
4. When the server resolves the turn, both clients play it back: broadside
   flashes (cyan = chain shot, orange = round shot), HP/sail bars, and a
   HUD narration with applied damage totals. Spectator controls from the
   mission demo apply during playback (Space pause, Right Arrow step, 1–4
   and +/− speed).
5. Repeat until the HUD declares VICTORY / DEFEAT / DRAW.

## Rules of engagement (server-enforced)

- You can only order your own living ships, and only target living enemy
  ships — the server rejects anything else.
- Submissions bind to the current turn number; stale or duplicate
  submissions are rejected (409), so a double-click can never burn a turn.
- Opponent orders are hidden until the turn resolves; the poll view only
  says *whether* they have submitted. The match seed is withheld until the
  match completes (a live seed would let a client simulate outcomes
  locally).
- The server ignores any client-supplied state: it resolves from its own
  persisted match state with the modifier set pinned at creation.
- Abandoned matches expire: 30 minutes unjoined, or 15 minutes without a
  submission (polling does not keep a match alive). Expired matches show
  MATCH EXPIRED on the next poll and refuse joins/orders. Each player can
  hold at most 3 open matches; finished and expired ones never count.

## Playtest checklist

- [ ] Create shows a code; a second client can join with it (case-insensitive).
- [ ] A third join attempt is rejected (match full).
- [ ] Order entry only ever lists your own frigates; targets only enemies.
- [ ] Confirm → "waiting for the enemy captain" → playback starts on both
      clients within a poll interval (~2 s) of the second confirm.
- [ ] Chain shot flashes cyan and shreds sail; round shot flashes orange
      and bites hull; bars track across turns without resetting to full.
- [ ] Markers actually move each turn; two fleets that charge straight
      ahead collide near midfield around turn 6–7 and rams flash white
      with hull + recoil damage in the HUD narration.
- [ ] When ships sail beyond the opening frame the camera follows —
      zooming out and re-centering so every ship (and its bars) stays in
      view, and never zooming tighter than the opening framing.
- [ ] Sunk ships stop being orderable/targetable on later turns.
- [ ] Focus fire beats split fire comfortably inside 20 turns; two
      hold-fire fleets reach the turn-limit DRAW.
- [ ] Killing the backend mid-wait shows "Connection hiccup ... retrying"
      and recovers when the backend returns.
- [ ] A match left waiting/idle past its TTL shows MATCH EXPIRED on the
      next poll instead of waiting forever; a fourth simultaneous open
      match is refused (match_limit_reached).

## Notes

- Both PvP scenes are generated: rerun `Assets → Armada → Build PvP
  Hotseat Demo Scene` / `Build PvP Netplay Demo Scene` after changing a
  builder. The standalone build script passes its scene explicitly and
  never touches project build settings; output lands in the gitignored
  `build/` folder.
- Hot-seat keeps match state client-side between turns (single machine
  only); netplay is fully server-authoritative — the client's only local
  bookkeeping is the pre-turn fleet snapshot used to animate playback.
- The scenario, fleet, and modifier set are fingerprint-pinned in
  `tests/pvpScenario.test.ts` and the Unity EditMode suite; deviations are
  design changes, not tuning. The full knob inventory, applied values,
  and tuning constraints live in the reviewed spec at
  `docs/design/pvp-tuning.md`.
