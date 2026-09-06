# Starter progression decisions

This is an explicit, provisional MVP content decision under the game-completion task, not a claim that the reference GDD contains finished balance tables. The GDD and exec spec require captain XP/levels, a small matching crew bonus, and cosmetic sail preview/equip; their illustrative values are not a complete implementation contract.

## Bounded first catalog

- One owned starter captain, Aurora Black, commands the player's campaign fleet. Rarity is Rare, matching the reference example; rarity has no independent stat multiplier in this slice.
- Captain levels 1–5 use cumulative XP thresholds 0, 100, 250, 450, 700. Each mission grants 25 XP once when replay-verified under the progression system. A dedicated `captainXpGranted` marker keeps this independent of legacy resource grants: a previously completed mission can grant XP on its first verified replay after this feature, without regranting resources. Repeated requests never duplicate XP; no XP is inferred from old completion metadata.
- Training spends one generic `captain_shard` for 25 XP. Generic shards train the single starter captain; they are not represented as character-specific promotion shards. Training stops at level 5 and never spends a shard at the cap. Each training request names the exact next training sequence number so retries cannot spend again.
- Two free starter crew members: Calico Jim (First Mate) and One-Eyed Ella (Gunnery Chief), using the reference names. Each is owned from profile creation but starts unassigned. Their matching starter-pirate tag forms the v0 crew chain when both are assigned in their correct slots.
- PvE gunfire effect with an explicit server-validated frozen loadout: +1% per captain level above 1 and +2% for the matching pair, additive within this progression multiplier. Maximum combined progression bonus is 6%; existing component upgrades remain the primary power source. Boarding, ram impacts and fire ticks are unaffected. See `../campaign-loadouts.md`; clients must send the frozen loadout before presenting its combat effect as active.
- Persist captain XP and assignments per authenticated player. Clients cannot submit earned XP, levels, rarity, or bonus values as authority. Progress reads must never identify another player by a supplied ID.
- Stable starter IDs are `aurora_black`, `calico_jim` (First Mate), and `one_eyed_ella` (Gunnery Chief). Training sequences start at 1; already consumed sequences return the current profile without charging again, future sequences conflict. The server stores XP only and derives levels. All mutations lock the owned profile before spending inventory, within one transaction.

## Presentation-only starter sails

- Default sail and one free test cosmetic, Harbor Blue, form the initial preview/equip catalog. This is local test content, not a real payment or purchased entitlement.
- Preview is temporary until Apply; Cancel restores saved equipment. Saved equipment survives restart.
- Cosmetic identity and color do not enter simulation inputs, damage, replay outcomes, or hashes. Player/enemy faction markers remain readable.
- No real payment integration, production catalog, or store publication is authorized by this local implementation.

## Deferred balance decisions

Repeat-clear diminishing returns, character recruitment/promotion beyond the starter, ultimates, more crew slots, and paid cosmetic pricing require their own explicit content decisions. Existing first-clear resource rewards stay unchanged in this slice.

Verification requires transactional first-clear XP, duplicate training protection, ownership isolation, persisted assignment readback, exact-loadout replay parity before power activation, and cosmetic preview/cancel/equip plus identical combat outcomes with either sail.
