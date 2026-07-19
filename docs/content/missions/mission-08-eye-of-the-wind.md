# Mission 08: Eye of the Wind
- Narrative beat: Introduce wind turn-rate — the helm answers slowly beating into the wind's eye and freely running before it.
- Objectives: Win; bonus—clean tack (win without a single clamped maneuver); bonus—swift victory (win within 8 turns).
- Enemy setup: 2x corvettes (AI: aggressive) in line abreast running downwind at the player.
- Player constraints: Boarding enabled; turn limit 10; no upgrade tiers on this mission.
- Environment: Dead-ahead headwind for the player's opening heading (wind 180, speed 4, medium variance); no terrain — the wind itself is the hazard.
- Rewards: Gold, ore, cosmetic token.
- Tuning knobs: Turn limits per point of sail — upwind 30°, beam 60°, downwind 90° (engine placeholders pending balance pass); swift-victory target 8 turns (placeholder); clamp is wind-speed-independent and sail tiers do not ease it (both open knobs); enemies-win-by-timeout stays the loss condition (knob open).
- Telemetry: Clamped maneuvers; upwind/downwind maneuver counts; fail reasons; turn count.
- QA notes: Clamped helm readability from maneuver events (ordered vs applied turnDelta); verify bonuses are win-gated; flag-off runs must keep mission 01-07 hashes byte-identical; draft spec authored with the slice, pending design review.
