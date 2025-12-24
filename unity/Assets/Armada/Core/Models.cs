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
    public sealed class SimState
    {
        public int Turn { get; set; }
        public SimWind Wind { get; set; }
        public List<SimShip> Ships { get; set; }
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

