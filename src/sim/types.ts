import { z } from 'zod';
import { MAX_UPGRADE_TIER } from '../economy/upgrades.js';

export const vectorSchema = z
  .object({
    x: z.number().int(),
    y: z.number().int()
  })
  .strict();

export const windSchema = z
  .object({
    direction: z.number().int().min(0).max(359),
    speed: z.number().int().min(0)
  })
  .strict();

export const shipStatusSchema = z
  .object({
    onFire: z.boolean().optional(),
    slowed: z.boolean().optional(),
    // Remaining-turn counters for modifiers.statusEffects; the booleans stay
    // the wire truth clients read (mirrors the cooldowns.boarding precedent).
    fireTurnsRemaining: z.number().int().min(0).max(10).optional(),
    slowTurnsRemaining: z.number().int().min(0).max(10).optional()
  })
  .strict();

export const shipSchema = z
  .object({
    id: z.string().min(1),
    side: z.enum(['player', 'enemy']),
    position: vectorSchema,
    heading: z.number().int().min(0).max(359),
    speed: z.number().int().min(0).max(10),
    hp: z.number().int().min(0).max(1000),
    sail: z.number().int().min(0).max(1000),
    crew: z.number().int().min(0).max(1000),
    status: shipStatusSchema.optional(),
    cooldowns: z
      .object({
        boarding: z.number().int().min(0).max(10).optional()
      })
      .partial()
      .optional()
  })
  .strict();

export const obstacleSchema = z
  .object({
    position: vectorSchema,
    radius: z.number().int().min(1)
  })
  .strict();

export const slowZoneSchema = z
  .object({
    position: vectorSchema,
    radius: z.number().int().min(1),
    speedPenalty: z.number().int().min(1).max(5)
  })
  .strict();

export const simStateSchema = z
  .object({
    turn: z.number().int().min(1),
    wind: windSchema,
    ships: z.array(shipSchema).min(1),
    // Impassable terrain (e.g. islands). Movement halts at the edge instead
    // of entering. Only meaningful with modifiers.windMovement.
    obstacles: z.array(obstacleSchema).max(8).optional(),
    // Hazard areas (e.g. debris fields) that slow ships moving inside them.
    // Only meaningful with modifiers.windMovement.
    slowZones: z.array(slowZoneSchema).max(8).optional()
  })
  .strict();

export const simOrderSchema = z
  .object({
    shipId: z.string().min(1),
    action: z.enum(['maneuver', 'broadside', 'boarding', 'pass']),
    targetShipId: z.string().min(1).optional(),
    turnDelta: z.number().int().min(-90).max(90).default(0),
    speedDelta: z.number().int().min(-2).max(2).default(0),
    side: z.enum(['port', 'starboard']).optional(),
    // Optional per-order ammo selection, only read for broadsides when
    // modifiers.chainShot is true and otherwise inert. Absent-by-default (no
    // default value) so legacy order payloads parse byte-identically.
    ammo: z.enum(['round', 'chain']).optional()
  })
  .strict()
  .superRefine((value, ctx) => {
    if ((value.action === 'broadside' || value.action === 'boarding') && !value.targetShipId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'targetShipId required for combat actions'
      });
    }
    if (value.action === 'broadside' && !value.side) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'side required for broadside'
      });
    }
  });

export const simModifiersSchema = z
  .object({
    damageScale: z.record(z.string(), z.number().min(0).max(2)).optional(),
    // Opt-in wind-aware resolution: wind impact curve on effective speed plus
    // per-turn movement. Absent or false keeps the legacy stationary rules.
    windMovement: z.boolean().optional(),
    // Opt-in raking fire: broadsides aligned with the target's keel line deal
    // multiplied damage. Absent or false keeps the legacy damage rules.
    rakingFire: z.boolean().optional(),
    // Per-ship boarding success-chance bonus as a fraction (0.1 = +10 points).
    boardingBonus: z.record(z.string(), z.number().min(-0.5).max(0.5)).optional(),
    // Per-ship broadside hit-chance bonus in percentage points (e.g. enrage).
    accuracyBonus: z.record(z.string(), z.number().int().min(-50).max(50)).optional(),
    // Opt-in status effects: fire deals per-turn hull damage plus an accuracy
    // penalty; slow reduces speed and turn rate. Absent or false keeps the
    // legacy rules and never mutates ship status.
    statusEffects: z.boolean().optional(),
    // Opt-in ship upgrades: the request-level upgrades block scales
    // player-side ship stats (cannon → broadside damage, sail → speed/turn,
    // hull → hp). Absent or false ignores the upgrades block entirely.
    shipUpgrades: z.boolean().optional(),
    // Opt-in wind-aware turn rates: maneuvers are clamped by point of sail
    // (hardest beating upwind, barely when running free). Absent or false
    // keeps the legacy unclamped turning rules.
    windTurnRate: z.boolean().optional(),
    // Opt-in ramming: movement-phase contact with an enemy hull deals
    // speed-scaled ram damage to both ships. Only meaningful with
    // modifiers.windMovement. Absent or false keeps the legacy contact-free
    // movement rules.
    ramming: z.boolean().optional(),
    // Opt-in mutual ramming (balance refinement over modifiers.ramming,
    // only meaningful with it): when the rammed target is itself under way,
    // the rammer takes counter-momentum damage scaled by the target's
    // effective speed instead of fractional recoil, so a head-on exchange
    // costs both sides equally regardless of resolution order. A stationary
    // target still yields the classic one-sided ram with recoil. Absent or
    // false keeps the legacy recoil rule.
    mutualRamming: z.boolean().optional(),
    // Opt-in chain shot: broadside orders may select ammo 'chain' to trade
    // hull damage for heavy sail/rigging damage. Absent or false keeps the
    // legacy round-shot damage split and ignores the ammo key entirely.
    chainShot: z.boolean().optional()
  })
  .strict();

// Owned upgrade tiers supplied by the client for preview purposes. Tier
// authenticity against actually-owned upgrades is enforced at mission
// win-proof time, not here.
export const shipUpgradeTiersSchema = z
  .object({
    cannon: z.number().int().min(0).max(MAX_UPGRADE_TIER).default(0),
    sail: z.number().int().min(0).max(MAX_UPGRADE_TIER).default(0),
    hull: z.number().int().min(0).max(MAX_UPGRADE_TIER).default(0)
  })
  .strict();

export const simPreviewSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turn: z.number().int().min(1).default(1),
    state: simStateSchema,
    orders: z.array(simOrderSchema).max(32),
    modifiers: simModifiersSchema.optional(),
    // Only read when modifiers.shipUpgrades is true.
    upgrades: shipUpgradeTiersSchema.optional()
  })
  .strict();

export type SimModifiers = z.infer<typeof simModifiersSchema>;
export type ShipUpgradeTiers = z.infer<typeof shipUpgradeTiersSchema>;
export type Obstacle = z.infer<typeof obstacleSchema>;
export type SlowZone = z.infer<typeof slowZoneSchema>;
export type Vector2 = z.infer<typeof vectorSchema>;
export type Wind = z.infer<typeof windSchema>;
export type ShipStatus = z.infer<typeof shipStatusSchema>;
export type ShipState = z.infer<typeof shipSchema>;
export type SimState = z.infer<typeof simStateSchema>;
export type SimOrder = z.infer<typeof simOrderSchema>;
export type SimPreviewRequest = z.infer<typeof simPreviewSchema>;

export type SimEvent =
  | {
      type: 'maneuver';
      shipId: string;
      heading: number;
      speed: number;
      turnDelta: number;
      speedDelta: number;
    }
  | {
      type: 'broadside';
      shipId: string;
      targetShipId: string;
      side: 'port' | 'starboard';
      hit: boolean;
      roll: number;
      hitChance: number;
      damage: {
        hull: number;
        sail: number;
        crew: number;
      };
      targetRemaining: {
        hp: number;
        sail: number;
        crew: number;
      };
      rake?: 'bow' | 'stern';
      // Present only when chain shot actually fired (modifiers.chainShot and
      // ammo 'chain'); round-shot events keep the legacy shape.
      ammo?: 'chain';
    }
  | {
      type: 'boarding';
      shipId: string;
      targetShipId: string;
      success: boolean;
      roll: number;
      crewLoss: number;
      targetCrewLoss: number;
      targetRemaining: {
        hp: number;
        sail: number;
        crew: number;
      };
    }
  | {
      type: 'movement';
      shipId: string;
      effectiveSpeed: number;
      position: Vector2;
      blocked?: boolean;
      slowedByHazard?: boolean;
    }
  | {
      type: 'status';
      shipId: string;
      status: ShipStatus;
    }
  | {
      type: 'ram';
      shipId: string;
      targetShipId: string;
      effectiveSpeed: number;
      hullDamage: number;
      selfHullDamage: number;
      targetRemaining: {
        hp: number;
        sail: number;
        crew: number;
      };
      rammerRemaining: {
        hp: number;
        sail: number;
        crew: number;
      };
    };

export interface SimSummary {
  playerRemaining: number;
  enemyRemaining: number;
  sunk: string[];
}

export interface SimPreviewResult {
  turn: number;
  nextState: SimState;
  events: SimEvent[];
  summary: SimSummary;
  hash: string;
}


