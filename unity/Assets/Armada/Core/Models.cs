using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Armada.Client.Core
{
    [Serializable]
    public sealed class ErrorResponse
    {
        [JsonProperty("error")] public string Error { get; set; }
    }

    [Serializable]
    public sealed class Player
    {
        public string Id { get; set; }
        public string ExternalId { get; set; }
        public string DisplayName { get; set; }
        public string Region { get; set; }
    }

    [Serializable]
    public sealed class GuestAuthRequest
    {
        public string ExternalId { get; set; }
        public string DisplayName { get; set; }
        public string Region { get; set; }
    }

    [Serializable]
    public sealed class GuestAuthResponse
    {
        public string Token { get; set; }
        public Player Player { get; set; }
    }

    [Serializable]
    public sealed class Mission
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> Rewards { get; set; }
        public Dictionary<string, object> Objectives { get; set; }
    }

    [Serializable]
    public sealed class MissionProgress
    {
        public string PlayerId { get; set; }
        public string MissionId { get; set; }
        public string Status { get; set; }
        public int? BestScore { get; set; }
        public Dictionary<string, object> LastResult { get; set; }
    }

    [Serializable]
    public sealed class RewardGrant
    {
        public string ItemKey { get; set; }
        public int Quantity { get; set; }
    }

    [Serializable]
    public sealed class MissionCompleteResponse
    {
        public MissionProgress Progress { get; set; }
        public List<RewardGrant> RewardsGranted { get; set; }
    }

    [Serializable]
    public sealed class MissionCompleteRequest
    {
        public string PlayerId { get; set; }
        public Dictionary<string, object> Result { get; set; }
        public int? BestScore { get; set; }
        // Win proof: the backend re-simulates seed + turns and rejects
        // completion of reward-bearing missions without a verified win.
        [JsonProperty("seed", NullValueHandling = NullValueHandling.Ignore)]
        public int? Seed { get; set; }
        [JsonProperty("turns", NullValueHandling = NullValueHandling.Ignore)]
        public List<List<SimOrder>> Turns { get; set; }
        // Tiers the winning run was played with; the backend validates them
        // against the player's owned upgrades before granting rewards.
        [JsonProperty("upgrades", NullValueHandling = NullValueHandling.Ignore)]
        public SimShipUpgrades Upgrades { get; set; }
    }

    [Serializable]
    public sealed class InventoryItem
    {
        public string PlayerId { get; set; }
        public string ItemKey { get; set; }
        public int Quantity { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    [Serializable]
    public sealed class InventoryGrantRequest
    {
        public string ItemKey { get; set; }
        public int Quantity { get; set; } = 1;
        public Dictionary<string, object> Metadata { get; set; }
    }

    [Serializable]
    public sealed class ShipUpgrade
    {
        public string PlayerId { get; set; }
        public string Component { get; set; }
        public int Tier { get; set; }
    }

    [Serializable]
    public sealed class OwnedUpgrade
    {
        public string Component { get; set; }
        public int Tier { get; set; }
    }

    [Serializable]
    public sealed class UpgradeCost
    {
        public string ItemKey { get; set; }
        public int Quantity { get; set; }
    }

    [Serializable]
    public sealed class UpgradeCatalogTier
    {
        public int Tier { get; set; }
        public List<UpgradeCost> Costs { get; set; }
    }

    [Serializable]
    public sealed class UpgradeCatalogEntry
    {
        public string Component { get; set; }
        public List<UpgradeCatalogTier> Tiers { get; set; }
    }

    [Serializable]
    public sealed class UpgradesResponse
    {
        public List<UpgradeCatalogEntry> Catalog { get; set; }
        public List<OwnedUpgrade> Owned { get; set; }
    }

    [Serializable]
    public sealed class UpgradePurchaseRequest
    {
        public string PlayerId { get; set; }
        public string Component { get; set; }
        // The tier being purchased; the backend rejects any value that is not
        // the player's current tier + 1, so replays cannot double-charge.
        public int Tier { get; set; }
    }

    [Serializable]
    public sealed class UpgradePurchaseResponse
    {
        public ShipUpgrade Upgrade { get; set; }
        public List<UpgradeCost> Spent { get; set; }
    }

    [Serializable]
    public sealed class TelemetryIngestRequest
    {
        public int SchemaVersion { get; set; } = 1;
        public string PlayerId { get; set; }
        public string MissionCode { get; set; }
        public Dictionary<string, object> Payload { get; set; }
    }

    [Serializable]
    public sealed class SimVector2
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [Serializable]
    public sealed class SimWind
    {
        public int Direction { get; set; }
        public int Speed { get; set; }
    }

    [Serializable]
    public sealed class SimShipStatus
    {
        [JsonProperty("onFire")] public bool? OnFire { get; set; }
        [JsonProperty("slowed")] public bool? Slowed { get; set; }
        [JsonProperty("fireTurnsRemaining")] public int? FireTurnsRemaining { get; set; }
        [JsonProperty("slowTurnsRemaining")] public int? SlowTurnsRemaining { get; set; }
    }

    [Serializable]
    public sealed class SimShipCooldowns
    {
        public int? Boarding { get; set; }
    }

    [Serializable]
    public sealed class SimShip
    {
        public string Id { get; set; }
        public string Side { get; set; }
        public SimVector2 Position { get; set; }
        public int Heading { get; set; }
        public int Speed { get; set; }
        public int Hp { get; set; }
        public int Sail { get; set; }
        public int Crew { get; set; }
        public SimShipStatus Status { get; set; }
        public SimShipCooldowns Cooldowns { get; set; }
    }

    [Serializable]
    public sealed class SimObstacle
    {
        public SimVector2 Position { get; set; }
        public int Radius { get; set; }
    }

    [Serializable]
    public sealed class SimSlowZone
    {
        public SimVector2 Position { get; set; }
        public int Radius { get; set; }
        public int SpeedPenalty { get; set; }
    }

    [Serializable]
    public sealed class SimState
    {
        public int Turn { get; set; }
        public SimWind Wind { get; set; }
        public List<SimShip> Ships { get; set; }
        [JsonProperty("obstacles", NullValueHandling = NullValueHandling.Ignore)]
        public List<SimObstacle> Obstacles { get; set; }
        [JsonProperty("slowZones", NullValueHandling = NullValueHandling.Ignore)]
        public List<SimSlowZone> SlowZones { get; set; }
    }

    [Serializable]
    public sealed class SimOrder
    {
        public string ShipId { get; set; }
        public string Action { get; set; }
        public string TargetShipId { get; set; }
        public int TurnDelta { get; set; }
        public int SpeedDelta { get; set; }
        public string Side { get; set; }
        // Optional per-order ammo selection ("round"/"chain"), only read by
        // the server when modifiers.chainShot is on. Null is omitted so
        // legacy order payloads stay byte-identical.
        [JsonProperty("ammo", NullValueHandling = NullValueHandling.Ignore)]
        public string Ammo { get; set; }
    }

    [Serializable]
    public sealed class SimDamage
    {
        public int Hull { get; set; }
        public int Sail { get; set; }
        public int Crew { get; set; }
    }

    [Serializable]
    public sealed class SimRemaining
    {
        public int Hp { get; set; }
        public int Sail { get; set; }
        public int Crew { get; set; }
    }

    [Serializable]
    public sealed class SimEvent
    {
        public string Type { get; set; }
        public string ShipId { get; set; }
        public string TargetShipId { get; set; }
        public string Side { get; set; }
        public bool? Hit { get; set; }
        public int? Roll { get; set; }
        public int? HitChance { get; set; }
        public SimDamage Damage { get; set; }
        public SimRemaining TargetRemaining { get; set; }
        public int? TurnDelta { get; set; }
        public int? SpeedDelta { get; set; }
        public bool? Success { get; set; }
        public int? CrewLoss { get; set; }
        public int? TargetCrewLoss { get; set; }
        public SimShipStatus Status { get; set; }
        // Maneuver/movement variant fields (docs/api/openapi.yaml SimEvent):
        // the post-maneuver heading and the post-move position the spectator
        // renderer animates markers toward.
        public int? Heading { get; set; }
        public SimVector2 Position { get; set; }
        // Ram event fields (docs/api/openapi.yaml SimEvent "ram" variant):
        // hull damage dealt to the target, recoil taken on the rammer's own
        // bow, and both remaining blocks for damage readability.
        public int? EffectiveSpeed { get; set; }
        public int? HullDamage { get; set; }
        public int? SelfHullDamage { get; set; }
        public SimRemaining RammerRemaining { get; set; }
        // Broadside chain-shot marker (docs/api/openapi.yaml SimEvent
        // "broadside" variant): present only when chain shot actually fired.
        public string Ammo { get; set; }
    }

    [Serializable]
    public sealed class SimSummary
    {
        public int PlayerRemaining { get; set; }
        public int EnemyRemaining { get; set; }
        public List<string> Sunk { get; set; }
    }

    [Serializable]
    public sealed class SimModifiers
    {
        // Opt-in flags mirror docs/api/openapi.yaml SimModifiers; other
        // server-side modifier knobs are added as client features need them.
        [JsonProperty("shipUpgrades", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShipUpgrades { get; set; }
    }

    [Serializable]
    public sealed class SimShipUpgrades
    {
        public int Cannon { get; set; }
        public int Sail { get; set; }
        public int Hull { get; set; }
    }

    [Serializable]
    public sealed class SimPreviewRequest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        public int Seed { get; set; }
        public int Turn { get; set; } = 1;
        public SimState State { get; set; }
        public List<SimOrder> Orders { get; set; }
        [JsonProperty("modifiers", NullValueHandling = NullValueHandling.Ignore)]
        public SimModifiers Modifiers { get; set; }
        // Only read by the server when Modifiers.ShipUpgrades is true.
        [JsonProperty("upgrades", NullValueHandling = NullValueHandling.Ignore)]
        public SimShipUpgrades Upgrades { get; set; }
    }

    [Serializable]
    public sealed class SimPreviewResult
    {
        public int Turn { get; set; }
        public SimState NextState { get; set; }
        public List<SimEvent> Events { get; set; }
        public SimSummary Summary { get; set; }
        public string Hash { get; set; }
    }

    [Serializable]
    public sealed class Mission01Objectives
    {
        public int TurnLimit { get; set; }
        public int BonusTurnTarget { get; set; }
        public double BonusHullDamageFraction { get; set; }
        public double EnemyDamageScale { get; set; }
    }

    [Serializable]
    public sealed class Mission01StartRequest
    {
        public int Seed { get; set; }
    }

    [Serializable]
    public sealed class Mission01StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission01Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission01ResolveRequest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        public int Seed { get; set; }
        public List<List<SimOrder>> Turns { get; set; }
        // Only accepted by missions that support upgrades (07 onward); leave
        // null elsewhere so the payload matches the strict backend schemas.
        [JsonProperty("upgrades", NullValueHandling = NullValueHandling.Ignore)]
        public SimShipUpgrades Upgrades { get; set; }
    }

    [Serializable]
    public sealed class Mission01BonusObjectives
    {
        public bool UnderHullDamageThreshold { get; set; }
        public bool WithinTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission01DamageProfile
    {
        public int PlayerHullDamage { get; set; }
        public double PlayerHullDamageFraction { get; set; }
        public int PlayerRemainingHp { get; set; }
        public int EnemyHullDamage { get; set; }
        public int EnemyRemainingHp { get; set; }
    }

    [Serializable]
    public sealed class Mission01TurnRecord
    {
        public int Turn { get; set; }
        public string Hash { get; set; }
        public SimSummary Summary { get; set; }
        public List<SimEvent> Events { get; set; }
    }

    [Serializable]
    public sealed class Mission01Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission01BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission01ResolveEnvelope
    {
        public Mission01Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission02Objectives
    {
        public int TurnLimit { get; set; }
        public int BonusTurnTarget { get; set; }
        public int UpwindBonusTurns { get; set; }
        public double EnemyDamageScale { get; set; }
    }

    [Serializable]
    public sealed class Mission02StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission02Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission02BonusObjectives
    {
        public bool HeldWeatherGage { get; set; }
        public bool WithinTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission02Telemetry
    {
        public int RakeAttempts { get; set; }
        public int RakeHits { get; set; }
        public int UpwindTurns { get; set; }
        public List<bool> UpwindByTurn { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission02Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission02BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission02Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission02ResolveEnvelope
    {
        public Mission02Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission03Objectives
    {
        public int TurnLimit { get; set; }
        public int BonusTurnTarget { get; set; }
        public int RakeHitTarget { get; set; }
        public double EnemyDamageScale { get; set; }
    }

    [Serializable]
    public sealed class Mission03StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission03Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission03BonusObjectives
    {
        public bool LandedRakingHits { get; set; }
        public bool WithinTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission03Telemetry
    {
        public int RakeAttempts { get; set; }
        public int RakeHits { get; set; }
        public int BoardingAttempts { get; set; }
        public int BoardingSuccesses { get; set; }
    }

    [Serializable]
    public sealed class Mission03ShipDamage
    {
        public string ShipId { get; set; }
        public int HullDamage { get; set; }
        public int RemainingHp { get; set; }
    }

    [Serializable]
    public sealed class Mission03DamageProfile
    {
        public int PlayerHullDamage { get; set; }
        public double PlayerHullDamageFraction { get; set; }
        public int PlayerRemainingHp { get; set; }
        public int EnemyHullDamage { get; set; }
        public int EnemyRemainingHp { get; set; }
        public List<Mission03ShipDamage> PerShip { get; set; }
    }

    // Turn records and the resolve request shape are shared with mission 01
    // (Mission01TurnRecord / Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission03Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission03BonusObjectives BonusObjectives { get; set; }
        public Mission03DamageProfile DamageProfile { get; set; }
        public Mission03Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission03ResolveEnvelope
    {
        public Mission03Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission04Objectives
    {
        public int TurnLimit { get; set; }
        public double EnemyCrewScale { get; set; }
        public double PlayerBoardingBonus { get; set; }
    }

    [Serializable]
    public sealed class Mission04StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission04Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission04BonusObjectives
    {
        public bool SuccessfulBoarding { get; set; }
        public bool NoShipLost { get; set; }
    }

    [Serializable]
    public sealed class Mission04Telemetry
    {
        public int BoardingAttempts { get; set; }
        public int BoardingSuccesses { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission04Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission04BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission04Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission04ResolveEnvelope
    {
        public Mission04Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission05Objectives
    {
        public int TurnLimit { get; set; }
        public int BonusTurnTarget { get; set; }
        public double FlagshipHpScale { get; set; }
    }

    [Serializable]
    public sealed class Mission05StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission05Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission05BonusObjectives
    {
        public bool SankFlagshipFirst { get; set; }
        public bool WithinTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission05Telemetry
    {
        public string FirstSinkTarget { get; set; }
        public int ChokeBlockedMoves { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission05Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission05BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission05Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission05ResolveEnvelope
    {
        public Mission05Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission06Objectives
    {
        public int TurnLimit { get; set; }
        public int BonusTurnTarget { get; set; }
        public double BossHpScale { get; set; }
        public double BossDamageScale { get; set; }
        public double EnrageHullFraction { get; set; }
        public int ReinforcementTurn { get; set; }
        public double ReinforcementHpScale { get; set; }
    }

    [Serializable]
    public sealed class Mission06StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission06Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission06BonusObjectives
    {
        public bool NoShipLost { get; set; }
        public bool WithinTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission06PhaseTransition
    {
        public int Turn { get; set; }
        public int Phase { get; set; }
    }

    [Serializable]
    public sealed class Mission06Telemetry
    {
        public List<Mission06PhaseTransition> PhaseTransitions { get; set; }
        public int? EnragedOnTurn { get; set; }
        public int? ReinforcementTurn { get; set; }
        public int ReinforcementDamageDealt { get; set; }
    }

    [Serializable]
    public sealed class Mission06DamageProfile
    {
        public int PlayerHullDamage { get; set; }
        public double PlayerHullDamageFraction { get; set; }
        public int PlayerRemainingHp { get; set; }
        public int EnemyHullDamage { get; set; }
        public int EnemyRemainingHp { get; set; }
        public int BossHullDamage { get; set; }
        public int BossRemainingHp { get; set; }
    }

    // Turn records and the resolve request shape are shared with mission 01
    // (Mission01TurnRecord / Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission06Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission06BonusObjectives BonusObjectives { get; set; }
        public Mission06DamageProfile DamageProfile { get; set; }
        public Mission06Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission06ResolveEnvelope
    {
        public Mission06Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission07Objectives
    {
        public int TurnLimit { get; set; }
        public double EnemySailScale { get; set; }
        public int IgnitionTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission07StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission07Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission07BonusObjectives
    {
        public bool EnemyIgnited { get; set; }
        public bool Unscorched { get; set; }
    }

    [Serializable]
    public sealed class Mission07Telemetry
    {
        public int IgnitionsInflicted { get; set; }
        public int IgnitionsSuffered { get; set; }
        public int SlowsInflicted { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission07Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission07BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission07Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission07ResolveEnvelope
    {
        public Mission07Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission08Objectives
    {
        public int TurnLimit { get; set; }
        public int UpwindTurnLimit { get; set; }
        public int DownwindTurnLimit { get; set; }
        public int SwiftTurnTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission08StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission08Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission08BonusObjectives
    {
        public bool CleanTack { get; set; }
        public bool SwiftVictory { get; set; }
    }

    [Serializable]
    public sealed class Mission08Telemetry
    {
        public int ClampedManeuvers { get; set; }
        public int UpwindManeuvers { get; set; }
        public int DownwindManeuvers { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission08Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission08BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission08Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission08ResolveEnvelope
    {
        public Mission08Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission09Objectives
    {
        public int TurnLimit { get; set; }
        public int RamContactRange { get; set; }
        public int RamTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission09StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission09Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission09BonusObjectives
    {
        public bool HullBreaker { get; set; }
        public bool Unrammed { get; set; }
    }

    [Serializable]
    public sealed class Mission09Telemetry
    {
        public int RamsInflicted { get; set; }
        public int RamsSuffered { get; set; }
        public int RamHullDamageDealt { get; set; }
        public int RamHullDamageTaken { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission09Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission09BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission09Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission09ResolveEnvelope
    {
        public Mission09Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class Mission10Objectives
    {
        public int TurnLimit { get; set; }
        public int ChainHullPercent { get; set; }
        public int ChainSailPercent { get; set; }
        public int ChainCrewPercent { get; set; }
        public int ChainSailTarget { get; set; }
    }

    [Serializable]
    public sealed class Mission10StartResponse
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public int TurnLimit { get; set; }
        public Mission10Objectives Objectives { get; set; }
        public SimState State { get; set; }
    }

    [Serializable]
    public sealed class Mission10BonusObjectives
    {
        public bool SailShredder { get; set; }
        public bool MixedBattery { get; set; }
    }

    [Serializable]
    public sealed class Mission10Telemetry
    {
        public int ChainShotOrders { get; set; }
        public int ChainShotHits { get; set; }
        public int RoundShotHits { get; set; }
        public int ChainSailDamageDealt { get; set; }
    }

    // Damage profile, turn records, and the resolve request shape are shared
    // with mission 01 (Mission01DamageProfile / Mission01TurnRecord /
    // Mission01ResolveRequest).
    [Serializable]
    public sealed class Mission10Outcome
    {
        public string MissionCode { get; set; }
        public int Seed { get; set; }
        public string Result { get; set; }
        public string FailReason { get; set; }
        public int TurnCount { get; set; }
        public int TurnLimit { get; set; }
        public Mission10BonusObjectives BonusObjectives { get; set; }
        public Mission01DamageProfile DamageProfile { get; set; }
        public Mission10Telemetry Telemetry { get; set; }
        public List<Mission01TurnRecord> Turns { get; set; }
    }

    [Serializable]
    public sealed class Mission10ResolveEnvelope
    {
        public Mission10Outcome Outcome { get; set; }
    }

    [Serializable]
    public sealed class ConfigSnapshot
    {
        public string Namespace { get; set; }
        public int Version { get; set; }
        public string Checksum { get; set; }
        public Dictionary<string, object> Content { get; set; }
    }

    [Serializable]
    public sealed class ConfigResponse
    {
        public ConfigSnapshot Config { get; set; }
        public string Signature { get; set; }
        public string Algorithm { get; set; }
    }

    // Responses
    [Serializable]
    public sealed class MissionsResponse
    {
        public List<Mission> Missions { get; set; }
    }

    [Serializable]
    public sealed class InventoryResponse
    {
        public List<InventoryItem> Items { get; set; }
    }

    [Serializable]
    public sealed class SimPreviewEnvelope
    {
        public SimPreviewResult Result { get; set; }
    }
}

