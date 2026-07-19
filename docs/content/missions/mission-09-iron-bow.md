# Mission 09: Iron Bow
- Narrative beat: Introduce ramming — a hull driven home is a weapon, and the bow that strikes pays in its own timbers.
- Objectives: Win; bonus—hull breaker (win with at least 2 rams delivered); bonus—unrammed (win without an enemy bow striking home).
- Enemy setup: 2x heavy-hulled brigs (AI: aggressive) in line abreast beating upwind at the player.
- Player constraints: Boarding enabled; turn limit 10; no upgrade tiers on this mission.
- Environment: Dead-astern tailwind for the player's opening heading (wind 0, speed 4, medium variance); no terrain — closing speed is the weapon and the hazard.
- Rewards: Gold, ore, captain shard.
- Tuning knobs: Ram contact range 25 (wide enough that a full-speed head-on pass cannot jump through the contact window between turns); ram hull damage 10 + 4x effective speed with 0.5 recoil fraction on the rammer's bow (engine placeholders pending balance pass); one ram per ship pair per turn, first mover strikes; ram target 2 (placeholder); stationary ships never initiate but can be struck; contact is rng-free so the roll stream is untouched; enemies-win-by-timeout stays the loss condition (knob open).
- Telemetry: Rams inflicted/suffered; ram hull damage dealt/taken as applied loss (a ram that finishes an already-battered hull counts only the hull actually removed, not the nominal blow); fail reasons; turn count.
- QA notes: Ram readability from ram events (hullDamage vs selfHullDamage and both remaining blocks); verify bonuses are win-gated; recoil can sink the rammer — sunk ships act no further; flag-off runs must keep mission 01-08 hashes byte-identical; draft spec authored with the slice, pending design review.
