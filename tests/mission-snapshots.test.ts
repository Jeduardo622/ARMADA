import { describe, expect, it } from 'vitest';
import { runMission01 } from '../src/sim/mission01.js';
import { runMission02 } from '../src/sim/mission02.js';
import { runMission03 } from '../src/sim/mission03.js';
import { runMission04 } from '../src/sim/mission04.js';
import { runMission05 } from '../src/sim/mission05.js';
import { runMission06 } from '../src/sim/mission06.js';
import { runMission07 } from '../src/sim/mission07.js';
import { runMission08 } from '../src/sim/mission08.js';
import { runMission09 } from '../src/sim/mission09.js';
import { runMission10 } from '../src/sim/mission10.js';
import { runMissionLoop } from '../src/sim/missionRunner.js';
import type { SimOrder, SimState } from '../src/sim/types.js';

describe('authoritative campaign turn snapshots', () => {
  it.each([
    runMission01, runMission02, runMission03, runMission04, runMission05,
    runMission06, runMission07, runMission08, runMission09, runMission10
  ].map((run) => ({ name: run.name, run })))('$name keeps authored prefixes and the next planning state stable', ({ run }) => {
    const initial = run(1, []);
    const playerShipId = initial.turns[0].startState.ships.find((ship) => ship.side === 'player')!.id;
    // Maneuvers make the authored suffix differ from the unsubmitted tail.
    const orders: SimOrder[][] = Array.from({ length: initial.turnLimit }, (_, index) => [
      {
        shipId: playerShipId, action: 'maneuver',
        turnDelta: index % 2 === 0 ? 15 : -15, speedDelta: 0
      }
    ]);
    const full = run(1, orders);
    expect(full.turns[0].nextState).not.toEqual(initial.turns[0].nextState);
    for (let length = 0; length <= full.turnCount; length++) {
      const prefix = run(1, orders.slice(0, length));
      expect(prefix.turns.slice(0, length)).toEqual(full.turns.slice(0, length));
      if (length < full.turnCount) {
        expect(prefix.turns[length].startState).toBeDefined();
        expect(prefix.turns[length].startState).toEqual(full.turns[length].startState);
      }
    }
    for (const record of full.turns) {
      expect(record.startState.turn).toBe(record.turn);
      expect(record.nextState.turn).toBe(record.turn + 1);
      expect(record.nextState.ships.filter((ship) => ship.side === 'player' && ship.hp > 0))
        .toHaveLength(record.summary.playerRemaining);
      expect(record.nextState.ships.filter((ship) => ship.side === 'enemy' && ship.hp > 0))
        .toHaveLength(record.summary.enemyRemaining);
    }
  });

  it('reveals the mission 06 reinforcement in turn 5 planning, not turn 4', () => {
    const run = runMission06(1, []);
    const fourth = run.turns[3];
    const fifth = run.turns[4];
    expect(fifth.startState).toBeDefined();
    expect(fourth.nextState.ships.some((ship) => ship.id === 'enemy-reinforcement')).toBe(false);
    expect(fifth.startState.ships.find((ship) => ship.id === 'enemy-reinforcement')).toMatchObject({
      side: 'enemy', position: { x: 300, y: 80 }, heading: 200, speed: 3,
      hp: 108, sail: 70, crew: 40
    });
    expect(fifth.nextState.ships.some((ship) => ship.id === 'enemy-reinforcement')).toBe(true);
  });

  it('isolates nested snapshots from later hooks, caller state and final state mutations', () => {
    const state: SimState = {
      turn: 1, wind: { direction: 0, speed: 2 },
      ships: ['player', 'enemy'].map((side, index) => ({
        id: side, side: side as 'player' | 'enemy', position: { x: index * 500, y: 0 },
        heading: 0, speed: 0, hp: 100, sail: 80, crew: 50,
        status: { onFire: false }, cooldowns: { boarding: 0 }
      })),
      obstacles: [{ position: { x: 40, y: 50 }, radius: 10 }],
      slowZones: [{ position: { x: 70, y: 80 }, radius: 10, speedPenalty: 1 }]
    };
    const result = runMissionLoop(1, [], {
      turnLimit: 2, createState: () => state,
      windForTurn: (_seed, turn) => ({ direction: turn * 10, speed: turn }),
      enemyOrders: () => [], modifiers: {},
      onTurnStart: (current, turn) => {
        current.ships[0].cooldowns!.boarding = turn;
        current.obstacles![0].position.x = turn;
        return current;
      }
    });
    expect(result.turns[0].startState).toBeDefined();
    expect(result.turns[0].nextState).toBeDefined();
    const first = structuredClone(result.turns[0]);
    const second = structuredClone(result.turns[1]);
    expect(first.startState.wind).toEqual({ direction: 10, speed: 1 });
    expect(first.startState.obstacles![0].position.x).toBe(1);
    expect(first.nextState.obstacles![0].position.x).toBe(1);
    state.ships[0].position.x = 900;
    state.ships[0].status!.onFire = true;
    state.slowZones![0].position.x = 900;
    result.finalState.ships[0].cooldowns!.boarding = 9;
    result.finalState.obstacles![0].position.x = 900;
    expect(result.turns[0]).toEqual(first);
    expect(result.turns[1]).toEqual(second);
    result.turns[1].startState.ships[0].position.x = 800;
    result.turns[0].nextState.ships[0].position.x = 700;
    expect(result.turns[0].startState).toEqual(first.startState);
    expect(result.turns[1].nextState).toEqual(second.nextState);
  });
});
