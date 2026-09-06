# Frozen PvE loadouts

All ten campaign resolve and completion requests accept optional independent blocks:

```json
{
  "upgrades": { "cannon": 1, "sail": 0, "hull": 1 },
  "loadout": { "schemaVersion": 1, "captainLevel": 3, "firstMate": "calico_jim", "gunneryChief": "one_eyed_ella" }
}
```

The client reads owned upgrades and `/players/me/progression` once when beginning a battle, then retains the chosen values for every resolve and completion replay. A level is an integer 1–5; each crew slot is its named starter ID or null. The server rejects additional loadout fields, unknown crew, levels above the authenticated owner's derived level, and tiers above owned tiers. Both starter crew are permanently owned, so later assignment changes do not invalidate an in-flight snapshot. Levels and tiers may be lower than currently owned.

This API is stateless. Each request is independently validated and simulated; completion does not attest that its snapshot matches a prior resolve request. The client must reuse its exact frozen snapshot and orders. No client-provided damage multiplier is accepted.

The PvE runner multiplies existing player-ship `damageScale` by `1 + 0.01 * (captainLevel - 1) + 0.02` when both matching crew are present (omit the final term otherwise). This affects gunfire through the existing damage-scaling path. Enemy scales, boarding, ram impacts, fire ticks, PvP, and generic simulation preview are unchanged. Existing cannon, sail, and hull upgrades apply through the established engine path for all ten missions. The maximum additional progression scale is 1.06.

Omitting both blocks preserves legacy hashes and scenario fingerprints. All-zero upgrades and level-one/no-crew loadout also preserve hashes. `/start` remains the unmodified scenario template. Use the first resolve record's `startState` for effective planning hull: it includes opening hull upgrades, scripted spawns and turn wind. That snapshot is display state, not raw engine input. The runner passes an unscaled opening state to the engine, avoiding double hull scaling. Damage profiles and hull bonus denominators use the effective opening hull; later turns carry engine hull forward.

Rollback: revert the loadout validation and mission-runner/scenario changes together, returning clients to requests without these optional blocks. Keep existing progression and upgrade ownership records. No schema or balance-table migration is part of this slice. Parent integration must compile and test the Unity client against this DTO before exposing the feature.
