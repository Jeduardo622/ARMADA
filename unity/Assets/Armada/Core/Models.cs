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
    public sealed class MissionCompleteRequest
    {
        public string PlayerId { get; set; }
        public Dictionary<string, object> Result { get; set; }
        public int? BestScore { get; set; }
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
    public sealed class SimState
    {
        public int Turn { get; set; }
        public SimWind Wind { get; set; }
        public List<SimShip> Ships { get; set; }
        [JsonProperty("obstacles", NullValueHandling = NullValueHandling.Ignore)]
        public List<SimObstacle> Obstacles { get; set; }
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
    }

    [Serializable]
    public sealed class SimSummary
    {
        public int PlayerRemaining { get; set; }
        public int EnemyRemaining { get; set; }
        public List<string> Sunk { get; set; }
    }

    [Serializable]
    public sealed class SimPreviewRequest
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        public int Seed { get; set; }
        public int Turn { get; set; } = 1;
        public SimState State { get; set; }
        public List<SimOrder> Orders { get; set; }
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

