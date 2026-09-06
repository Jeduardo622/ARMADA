# Connected campaign

Open `unity/Assets/Scenes/Campaign.unity` in Unity 2022.3.62f3 and enter Play mode with the local backend listening at `http://localhost:4500`. Rebuild the scene from **Assets > Armada > Build Campaign Scene**. The builder preserves the existing demo scenes and requires the checked-in ship, board, and harbor assets.

The harbor restores a server-issued guest credential from protected platform storage, reads the signed-in player's saved mission progress and inventory, and opens the campaign chart or shipyard. Windows uses DPAPI; Android uses Keystore. Unsupported storage platforms fail closed. The Android Keystore implementation still needs device runtime proof; Editor verification is not a substitute.

Complete missions in sequence. Select each surviving ship, set its target/action, adjust helm and speed, then confirm the fleet's orders. Boarding unlocks with mission 3; chain shot is available in mission 10. The server resolves orders and supplies defensive turn snapshots. The client plays only the submitted turn, and uses the following turn's starting snapshot for new orders, including wind and reinforcements.

Undo rewinds the previous turn while entering orders. It is unavailable during requests/playback. A victory unlocks the next mission only after completion succeeds. Failed saves retain the frozen winning proof for retry and allow an explicit abandon confirmation. Defeat can be retried. First-clear rewards remain server-controlled; replays do not duplicate them.

The shipyard displays authoritative costs and balances and purchases exactly the tier shown. Component upgrades apply across all ten campaign missions. Captain & Crew shows Aurora Black's XP, training cost and owned crew assignments. Training submits the displayed sequence once, then refreshes the authoritative profile and supplies.

Each battle reads owned component tiers and captain/crew once. That defensive snapshot accompanies every resolution and completion request, including retries and undo. An opening fetch failure blocks sailing and can be retried; successful snapshots remain frozen for the battle. Hull readouts use the server's upgraded opening state as their maximum. See `campaign-loadouts.md` for the server validation and stateless replay limits.

## Evidence and verification

The first actual Unity campaign pass on 2026-09-06 completed all ten missions with three stars shown, through normal controller order handlers and actual API playback/completion. Winning turn counts: **5, 5, 9, 5, 8, 10, 7, 8, 6, 8**. This is local runtime proof, not device performance or release evidence.

Focused tests cover maneuver serialization, order bounds, proof snapshots, forecast-win rejection, retry/undo, planning snapshots, chart locking, UI callbacks, save-failure navigation, and affordable tier selection. Run `npm run verify:local` with `UNITY_EDITOR_PATH` configured for final integrated verification, including licensed EditMode and PlayMode gates.

## Risk and rollback

Class C applies to authentication, API integration, state serialization and purchase boundaries. Backend/Security and Unity review plus human merge are required. Revert the campaign client commit to return to the existing demo entry points; retain protected guest credentials and additive server progress columns. No production or store deployment is part of this local slice.
