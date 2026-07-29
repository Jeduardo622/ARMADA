# Mission 10 "Sail-Cutter": Spectator and Playable Demos

> Looking for the two-player battle? The PvP demo (hot-seat and
> two-client netplay) lives in [pvp.md](pvp.md).

Mission 10 ships as two scenes, both driven by `Mission10Bootstrap` and
differing only by its `mode`:

- **`Assets/Scenes/SpectatorDemo.unity`** (mode `Spectate`) plays back a
  resolved run of the pinned seed-5 mixed-battery orders (chain shot into
  the rigging for three turns, then round shot to sink), animated from the
  server's turn event stream. No gameplay input, but the playback itself
  can be paused, stepped, and speed-scaled (see [Controls](#controls)).
- **`Assets/Scenes/Mission10Play.unity`** (mode `Play`) is the playable
  mission: you write the orders. See
  [Playing the mission](#playing-the-mission).

## 1. Run the backend

```bash
docker compose up -d          # local Postgres (Docker Desktop must be running)
npm install
npm run migrate               # apply prisma migrations
npm run seed                  # seeds feature flags, including missions_api
npm run dev                   # Fastify server on http://localhost:4500
```

## 2. Open the scene

1. Open the `unity/` project in Unity `2022.3.62f3`.
2. First open only: import TMP Essentials when prompted
   (Window → TextMeshPro → Import TMP Essential Resources), otherwise the
   HUD text will not render.
3. Open `Assets/Scenes/SpectatorDemo.unity` (spectate) or
   `Assets/Scenes/Mission10Play.unity` (playable) and press Play.

The `Mission10Bootstrap` in the spectator scene authenticates a guest session, runs
the mission with seed 5 and the pinned mixed-battery orders, saves the
win, then hands the resolved outcome to the `SpectatorRenderer`, which
plays the turn stream: movement lerps, maneuver rotations, and broadside
flashes — **chain shot flashes cyan** (the mission's showcase mechanic),
round shot flashes orange, rams flash white. Player ships are green
cubes, enemies red capsules. The top HUD line narrates each step and
finishes with the outcome, bonus objectives, and damage totals derived
from applied (remaining-block) losses, never nominal rolls.

## Controls

On-screen **Pause / Step / Speed − / Speed +** buttons (top strip) drive
playback on every scene — they are the touch controls on device. The
keyboard bindings below remain as the Editor dev harness:

- **Space** — pause / resume playback.
- **Right Arrow** — while paused, play exactly one playback step
  (banner, maneuver, move, or attack flash), then freeze again.
- **1–4** — playback speed presets ×0.5, ×1, ×2, ×4.
- **+ / −** — cycle up/down through the speed presets.

The HUD line appends `PAUSED` and the current speed whenever they differ
from normal playback. Every ship marker carries a hull bar (top, green)
and sail bar (below it, yellow) scaled by remaining/initial values from
the same applied remaining blocks that drive the damage totals. Bindings,
presets, and bar geometry are design-tunable placeholders on
`SpectatorRenderer`.

## Playing the mission

Open `Assets/Scenes/Mission10Play.unity` and press Play. Each turn you
author orders for the surviving sloops with the button strip along the
bottom, then press **Confirm Turn** to resolve it and watch it play back
before the next order round.

- **Next Ship** — move the cursor between your sloops.
- **Turn < / Turn >** — swing the selected sloop's heading in 15° steps,
  up to ±90.
- **Speed − / Speed +** — trim sail, up to ±2.
- **Target** — cycle the broadside through the living clippers and back to
  "hold fire" (a sloop with no target manoeuvres only).
- **Ammo** — swap the selected sloop between round shot and chain shot.
  Chain shreds rigging (120% sail, 40% hull); round shot sinks hulls.
- **Confirm Turn** — resolve the turn server-side and play it back.
- **Undo Turn** — withdraw the last confirmed turn and write it again.

### How the turn loop works

The client never simulates. Each confirmed turn is appended to a
client-held order array, and the **whole array** is re-sent to
`/missions/mission-10-sail-cutter/resolve`, which accepts a partial turns
list. The mission loop feeds turn N only the previous turn's state and
`playerTurnOrders[N-1]`, so resolving the prefix `[t1..tN]` returns
byte-identical records for turns 1..N regardless of what follows — pinned
by the `mission 10 prefix stability` tests in `tests/mission10.test.ts`.
Only the newest record is rendered, and the tail the server resolves past
your authored turns (idle sloops) is ignored.

Two things fall out of that. Undo is free: the order array is client-side
and the server holds no run state, so withdrawing a turn just shortens the
next prefix. And the accumulated array is exactly the win's proof — the
winning resolve's snapshot is what `/complete` re-sends.

### Choosing the playable seed

The playable scene runs **seed 872**, not the spectator scene's seed 5.
Seed 2 is the *fixture* seed: it is pinned to win the one hardcoded order
script, which says nothing about a player writing their own orders. Seed
872 was picked from a sweep over the order families a captain would
plausibly try:

| Play | Result |
| --- | --- |
| Focused round shot, A then B | win, turn 9 — no bonuses |
| Open with 1–3 chain volleys, then ball | win, turn 9, same hull cost — **both bonuses** |
| A fourth chain volley | loss (timeout) |
| Crowd on sail (speed +2 while closing) | win a turn earlier, at ~40 more hull damage |
| Slow down to fight, or hold fire to close | loss |
| Pure chain, or split fire across both clippers | loss |

So a player who never touches the ammo toggle can still finish the
mission, committing to chain is rewarded with both bonus objectives rather
than being required, and the trade has a visible cliff at the fourth
volley. Dawdling and unfocused fire lose outright.

Worth knowing when reading those numbers: in the current balance chain
shot never wins *faster* than round shot, and across an 800-seed sweep it
beat round shot on remaining hull in only 8 of 659 mutual wins — its
payoff is the bonus objectives, not the fight. That is the existing
balance (re-derived under the broadside-arc accuracy curve), not a
property of this seed.

## Notes

- All colors, speeds, and the sim-to-world scale are design-tunable
  placeholders on `SpectatorRenderer` (`unity/Assets/Armada/Playback/`).
  The full knob inventory, applied values, and tuning constraints live in
  the reviewed spec at `docs/design/spectator-tuning.md`.
- Both scenes are generated: rerun `Assets → Armada → Build Spectator Demo
  Scene` or `Assets → Armada → Build Mission 10 Play Scene` (menu) after
  changing a builder to rebuild it deterministically. Never hand-edit the
  `.unity` files.
- The backend URL and toggles live in
  `Assets/Scenes/SpectatorDemoClientConfig.asset` and
  `Assets/Scenes/Mission10PlayClientConfig.asset`
  (both default to `http://localhost:4500`).
