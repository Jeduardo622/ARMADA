# Spectator Demo: Mission 10 "Sail-Cutter"

Watch the repo's first Unity scene play back a resolved Mission 10 run —
the pinned seed-2 mixed-battery orders (chain shot into the rigging for
three turns, then round shot to sink) — animated from the server's turn
event stream. Spectate only; there is no player input yet.

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
3. Open `Assets/Scenes/SpectatorDemo.unity` and press Play.

The `Mission10Bootstrap` in the scene authenticates a guest session, runs
the mission with seed 2 and the pinned mixed-battery orders, saves the
win, then hands the resolved outcome to the `SpectatorRenderer`, which
plays the turn stream: movement lerps, maneuver rotations, and broadside
flashes — **chain shot flashes cyan** (the mission's showcase mechanic),
round shot flashes orange, rams flash white. Player ships are green
cubes, enemies red capsules. The top HUD line narrates each step and
finishes with the outcome, bonus objectives, and damage totals derived
from applied (remaining-block) losses, never nominal rolls.

## Notes

- All colors, speeds, and the sim-to-world scale are design-tunable
  placeholders on `SpectatorRenderer` (`unity/Assets/Armada/Playback/`).
- The scene is generated: rerun `Assets → Armada → Build Spectator Demo
  Scene` (menu) after changing the builder to rebuild it deterministically.
- The backend URL and toggles live in
  `Assets/Scenes/SpectatorDemoClientConfig.asset`
  (defaults to `http://localhost:4500`).
