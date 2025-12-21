# Balance Tables (MVP Reference)

Keep these in shared config/JSON; mission specs should reference IDs, not hardcode numbers.

## Ships (example fields)
- ship_id, class, tier, hull_hp, sail_hp, crew_hp, speed, turn_rate, broadside_damage, rake_multiplier, boarding_bonus, abilities.

## Captains
- captain_id, rarity, ability_modifiers (accuracy, cooldown), passive bonuses, crew-chain bonus.

## Weapons/Components
- cannon tier → damage, reload; sail tier → speed/turn; hull tier → hp/armor.

## Combat Scalars
- Raking multiplier baseline; boarding success formula factors (crew_hp, captain bonus, status effects).
- Wind impact curve on speed/turn.

## Economy
- Upgrade costs per tier (gold, timber/ore), captain XP/shards curves.

## Status Effects
- Fire: DoT + accuracy penalty; Slow: turn/speed penalty; durations and stack rules.

## Storage
- Source of truth: config service JSON; versioned and signed; referenced by client and backend.

