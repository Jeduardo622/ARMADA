// Ship upgrade effects (modifiers.shipUpgrades): request-level owned tiers
// scale player-side ships only — cannon tiers raise broadside (and raked)
// damage, sail tiers raise effective speed and ease the slowed turn clamp,
// hull tiers raise hull hp at the battle-start (turn 1) state build only,
// so chained turns never compound the bonus. All values are design-tunable
// placeholders pending a balance pass (docs/content/balance-tables.md:
// cannon tier → damage, sail tier → speed/turn, hull tier → hp).
export const CANNON_DAMAGE_BONUS_PCT_PER_TIER = 10;
export const SAIL_SPEED_BONUS_PER_TIER = 1;
export const SAIL_SLOW_TURN_RECOVERY_PER_TIER = 15;
export const HULL_HP_BONUS_PCT_PER_TIER = 10;

// Bounds shared with the sim schema: shipSchema caps hp at 1000 and
// simOrderSchema caps turnDelta at ±90; scaled values must stay inside them.
const MAX_SHIP_HP = 1000;
const MAX_TURN_RATE = 90;

export function cannonDamageBonusPct(tier: number): number {
  return CANNON_DAMAGE_BONUS_PCT_PER_TIER * tier;
}

export function sailSpeedBonus(tier: number): number {
  return SAIL_SPEED_BONUS_PER_TIER * tier;
}

export function slowedTurnRateLimit(baseLimit: number, sailTier: number): number {
  return Math.min(MAX_TURN_RATE, baseLimit + SAIL_SLOW_TURN_RECOVERY_PER_TIER * sailTier);
}

// Integer scale-then-floor, mirroring the engine's damage scaling.
export function upgradedHullHp(hp: number, hullTier: number): number {
  return Math.min(
    MAX_SHIP_HP,
    Math.floor((hp * (100 + HULL_HP_BONUS_PCT_PER_TIER * hullTier)) / 100)
  );
}
