# Mission 07: Burning Seas
- Narrative beat: Introduce status effects — fire spreads and rigging fails under sustained fire.
- Objectives: Win; bonus—set an enemy ablaze; bonus—win unscorched (no player ship ignited).
- Enemy setup: 2x frigates (AI: aggressive) in line abreast bearing down.
- Environment: Steady tailwind for the player's opening heading; no terrain — status effects are the hazard.
- Player constraints: Boarding enabled; turn limit 10.
- Rewards: Gold, timber, captain shard.
- Tuning knobs: Enemy sail_hp 0.85x (weathered rigging keeps slow in play); hull 1.0x; wind variance medium; fire/slow constants from the engine status-effects table (design-tunable).
- Telemetry: Ignitions inflicted/suffered; slows inflicted; fail reasons; turn count.
- QA notes: Burn/slow state readability from status events; verify bonuses are win-gated; draft spec authored with the slice, pending design review.
