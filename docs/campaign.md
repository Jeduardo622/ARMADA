# Campaign: All Ten Missions, Player-Authored

`Assets/Scenes/Campaign.unity` is the campaign front end. A mission-select
menu lists missions 01–10; picking one shows its briefing, **Set Sail**
starts it, and you write every order yourself, one turn at a time, exactly
as in the Mission 10 play scene ([demo.md](demo.md)). When a run ends you
can **Retry** it or go back to the **Missions** list, which shows the best
star rating you earned this session.

## Run it

1. Start the backend as in [demo.md](demo.md#1-run-the-backend)
   (`docker compose up -d`, `npm run migrate`, `npm run seed`, `npm run dev`).
2. Open `unity/` in Unity `2022.3.62f3`.
3. **First time only:** generate the scene with `Assets → Armada → Build
   Campaign Scene` (or `Rebuild All Generated Scenes`) and commit
   `Assets/Scenes/Campaign.unity`, `CampaignClientConfig.asset`, and their
   `.meta` files. The builder is deterministic; never hand-edit the scene.
4. Open `Assets/Scenes/Campaign.unity` and press Play.

The scene is not yet in the player build's scene list
(`ProjectSettings/EditorBuildSettings.asset` is empty for every scene).
Adding it is a build-settings change, which is protected (Class C) and
needs its own approved slice.

## Playing

Order controls are the Mission 10 surface, shared by every mission:
**Next Ship**, **Turn < / >** (15° steps, ±90), **Speed − / +** (±2),
**Target** (cycles living enemies, then "hold fire"), **Confirm Turn**, and
**Undo Turn**. Two controls appear only where the mission's rules accept
them:

| Control | Missions | Effect |
| --- | --- | --- |
| **Board** | 03–10 | Grapple the selected target instead of firing (crew vs crew; a failed boarding still costs men). Missions 01–02 lock boarding, matching the server's win-proof config. |
| **Ammo** | 10 | Swap round shot / chain shot. Inert everywhere else, so it is hidden. |

Broadsides hit hardest from the beam (D1-A accuracy curve), raking fire
from a target's bow or stern does half again as much, and the wind changes
how far each heading carries you.

**Stars**: one for the win, one per bonus objective (every mission has two),
following the GDD's 3-star rating. The bonus names come from each mission's
outcome (for example Mission 04's "Successful boarding" and "No ship lost").

## How it works

- **No client simulation.** Every mission runs through the server's shared
  `runMissionLoop`, whose turn N depends only on the previous state and
  `playerTurnOrders[N-1]`. Re-sending the growing order array to the
  mission's `/resolve` therefore returns the same records for turns already
  played, so the client renders only the newest one. This is the same
  contract the Mission 10 play scene relies on, generalized.
- **One adapter per mission.** `PlayableMissionCatalog`
  (`unity/Assets/Armada/Services/`) describes each mission (code, title,
  briefing, turn limit, seed, allowed controls) and wraps its existing client
  flow in an `IPlayableMissionRunner`. The flows' scenario-fingerprint, seed,
  and outcome checks stay in force.
- **The win is the proof.** Each runner snapshots the turns it resolved and
  re-sends that snapshot on completion. Missions 07–10 complete through
  their flow, and Mission 07's flow also carries the owned upgrade tiers it
  resolved with.
- **Scripted arrivals.** Mission 06's turn-5 reinforcement is spliced in by
  the server with no event. The client mirrors it
  (`Mission06Scenario.BuildReinforcement`, pinned on both sides) and adds it
  before the arrival turn is authored, so it can be targeted that turn, and
  removes it again on undo.

## Seeds

The campaign plays each mission's server default seed (101, 202, … 909),
except Mission 10, which keeps the free-play seed 872 chosen in
[demo.md](demo.md#choosing-the-playable-seed). Only Mission 10's seed was
swept against human-style order families. Sweeping 01–09 the same way is a
follow-up.

## Known limits (follow-ups)

- **Progress is session-local.** `/missions/:code/complete` records
  completions, but there is no route that reads a player's progress back,
  so stars reset when the scene restarts. A read-progress route is an API
  change (Class C).
- **Wind readout is the opening wind.** Turn records do not carry per-turn
  wind, so the HUD shows the start-of-mission wind even where the server's
  per-turn wind varies. Same limitation as the Mission 10 play scene.
- **Upgrades only matter in Mission 07**, as before. Missions 03–06 reject
  upgrade proofs (`docs/design/mission-balance.md`, design target 5).
- No missions are locked: all ten are selectable from the start.
