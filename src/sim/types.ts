import { z } from 'zod';

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
    slowed: z.boolean().optional()
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
    side: z.enum(['port', 'starboard']).optional()
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
    boardingBonus: z.record(z.string(), z.number().min(-0.5).max(0.5)).optional()
  })
  .strict();

export const simPreviewSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turn: z.number().int().min(1).default(1),
    state: simStateSchema,
    orders: z.array(simOrderSchema).max(32),
    modifiers: simModifiersSchema.optional()
  })
  .strict();

export type SimModifiers = z.infer<typeof simModifiersSchema>;
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


