export const UPGRADE_COMPONENTS = ['cannon', 'sail', 'hull'] as const;
export type UpgradeComponent = (typeof UPGRADE_COMPONENTS)[number];

export type UpgradeCost = {
  itemKey: string;
  quantity: number;
};

export const MAX_UPGRADE_TIER = 3;

// Design-tunable costs (docs/content/balance-tables.md, Economy): every tier
// costs gold plus a material — ore for cannon, timber for sail and hull.
export const UPGRADE_COST_TABLE: Record<UpgradeComponent, Record<number, UpgradeCost[]>> = {
  cannon: {
    1: [
      { itemKey: 'gold', quantity: 100 },
      { itemKey: 'ore', quantity: 20 }
    ],
    2: [
      { itemKey: 'gold', quantity: 250 },
      { itemKey: 'ore', quantity: 50 }
    ],
    3: [
      { itemKey: 'gold', quantity: 600 },
      { itemKey: 'ore', quantity: 120 }
    ]
  },
  sail: {
    1: [
      { itemKey: 'gold', quantity: 80 },
      { itemKey: 'timber', quantity: 25 }
    ],
    2: [
      { itemKey: 'gold', quantity: 200 },
      { itemKey: 'timber', quantity: 60 }
    ],
    3: [
      { itemKey: 'gold', quantity: 500 },
      { itemKey: 'timber', quantity: 140 }
    ]
  },
  hull: {
    1: [
      { itemKey: 'gold', quantity: 120 },
      { itemKey: 'timber', quantity: 40 }
    ],
    2: [
      { itemKey: 'gold', quantity: 300 },
      { itemKey: 'timber', quantity: 90 }
    ],
    3: [
      { itemKey: 'gold', quantity: 700 },
      { itemKey: 'timber', quantity: 200 }
    ]
  }
};

export function upgradeCostsFor(component: UpgradeComponent, tier: number): UpgradeCost[] {
  return UPGRADE_COST_TABLE[component]?.[tier] ?? [];
}

export function upgradeCatalog() {
  return UPGRADE_COMPONENTS.map((component) => ({
    component,
    tiers: Array.from({ length: MAX_UPGRADE_TIER }, (_, index) => ({
      tier: index + 1,
      costs: upgradeCostsFor(component, index + 1)
    }))
  }));
}
