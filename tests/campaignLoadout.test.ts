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
import type { SimOrder, ShipUpgradeTiers } from '../src/sim/types.js';
import { campaignWinFixtures } from './fixtures/campaignWins.js';
import { runMissionLoop } from '../src/sim/missionRunner.js';
import { createMission01State } from '../src/sim/mission01.js';

const captainless = { schemaVersion: 1 as const, captainLevel: 1, firstMate: null, gunneryChief: null };
const paired = { schemaVersion: 1 as const, captainLevel: 5, firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' };
type Run = (seed: number, orders: SimOrder[][], upgrades?: ShipUpgradeTiers, loadout?: typeof captainless | typeof paired) => ReturnType<typeof runMission01>;
const runs = [runMission01, runMission02, runMission03, runMission04, runMission05, runMission06, runMission07, runMission08, runMission09, runMission10] as unknown as Run[];

describe('frozen campaign loadout mechanics', () => {
  it.each(runs.map((run, index) => ({ run, index, name: `mission${index + 1}` })))('$name preserves neutral hashes and exposes effective hull exactly once', ({ run, index }) => {
    const fixture = campaignWinFixtures[index];
    const baseline = run(fixture.seed, fixture.turns);
    const neutral = run(fixture.seed, fixture.turns, { cannon: 0, sail: 0, hull: 0 }, captainless);
    expect(neutral.turns.map(turn => turn.hash)).toEqual(baseline.turns.map(turn => turn.hash));
    const upgraded = run(fixture.seed, fixture.turns, { cannon: 0, sail: 0, hull: 1 }, captainless);
    for (const ship of upgraded.turns[0].startState.ships) {
      const original = baseline.turns[0].startState.ships.find(other => other.id === ship.id)!;
      expect(ship.hp).toBe(ship.side === 'player' ? Math.floor(original.hp * 110 / 100) : original.hp);
    }
    const openingHp = upgraded.turns[0].startState.ships.filter(ship => ship.side === 'player').reduce((sum, ship) => sum + ship.hp, 0);
    expect(upgraded.damageProfile.playerHullDamage).toBe(openingHp - upgraded.damageProfile.playerRemainingHp);
    expect(upgraded.damageProfile.playerHullDamage).toBeGreaterThanOrEqual(0);
    const players = upgraded.turns[0].nextState.ships.filter(ship => ship.side === 'player');
    for (const ship of players) expect(ship.hp).toBeLessThanOrEqual(upgraded.turns[0].startState.ships.find(other => other.id === ship.id)!.hp);
    if (upgraded.turns.length > 1) for (const ship of players) expect(upgraded.turns[1].startState.ships.find(other => other.id === ship.id)?.hp).toBe(ship.hp);
  });

  it('increases player gun damage for captain plus pair while preserving enemy scaling and frozen replay', () => {
    const fixture = { ...campaignWinFixtures[0], seed: 12 };
    const run = runs[0];
    const baseline = run(fixture.seed, fixture.turns);
    const improved = run(fixture.seed, fixture.turns, undefined, paired);
    const broadsides = (outcome: ReturnType<Run>, side: 'player' | 'enemy') => outcome.turns[0].events.filter(event =>
      event.type === 'broadside' && outcome.turns[0].startState.ships.find(ship => ship.id === event.shipId)?.side === side);
    const hullDamage = (outcome: ReturnType<Run>) => broadsides(outcome, 'player').reduce((sum, event) => sum + (event.type === 'broadside' ? event.damage.hull : 0), 0);
    expect(hullDamage(improved)).toBeGreaterThan(hullDamage(baseline));
    expect(broadsides(improved, 'enemy')).toEqual(broadsides(baseline, 'enemy'));
    expect(run(fixture.seed, fixture.turns, undefined, paired)).toEqual(improved);
  });

  it('does not double-scale or compound opening hull when no ship fires', () => {
    const run = runMissionLoop(12, [], {
      turnLimit: 2, createState: createMission01State,
      windForTurn: () => ({ direction: 0, speed: 0 }), enemyOrders: () => [], modifiers: {},
      upgrades: { cannon: 0, sail: 0, hull: 1 }
    });
    for (const turn of run.turns) {
      expect(turn.startState.ships.find(ship => ship.side === 'player')?.hp).toBe(132);
      expect(turn.nextState.ships.find(ship => ship.side === 'player')?.hp).toBe(132);
    }
  });
});
