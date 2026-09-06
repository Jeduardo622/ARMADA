using System;
using System.Collections.Generic;

namespace Armada.Client.Core
{
    public sealed class CampaignMissionDefinition
    {
        public int Number { get; }
        public string Code { get; }
        public string Name { get; }
        public string Briefing { get; }
        public string Objectives { get; }
        public int Seed { get; }
        public int TurnLimit { get; }
        public bool BoardingAllowed => Number >= 3;
        public bool ChainShotAllowed => Number == 10;
        private readonly Func<int, SimState> _start;
        public SimState StartState() => _start(Seed);
        public CampaignMissionDefinition(int number, string code, string name, string briefing,
            string objectives, int seed, int turnLimit, Func<int, SimState> start)
        {
            Number = number; Code = code; Name = name; Briefing = briefing;
            Objectives = objectives; Seed = seed; TurnLimit = turnLimit; _start = start;
        }
    }
    public static class CampaignCatalog
    {
        // Seeds are pinned by the normal-control playability sweep, not selected for spectator-only fixtures.
        public static IReadOnlyList<CampaignMissionDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new CampaignMissionDefinition(1, Mission01Scenario.MissionCode, "Fair Wind",
                "A lone raider tests your first command. Read the wind, bring your guns to bear, and protect your hull.",
                "Defeat the raider. Bonus: under 20% hull damage; finish within 6 turns.",
                12, Mission01Scenario.TurnLimit, seed => Mission01Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(2, Mission02Scenario.MissionCode, "Weather Gage",
                "The wind is shifting. Coordinate your sloops and keep the weather gage against a larger formation.",
                "Defeat the enemy fleet. Hold the upwind advantage and finish swiftly.",
                3, Mission02Scenario.TurnLimit, seed => Mission02Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(3, Mission03Scenario.MissionCode, "Raking Shot",
                "A frigate and her escort close in. Aim along an enemy's bow or stern to rake her decks.",
                "Win the battle. Bonus: land 2 raking hits; finish within 9 turns. Boarding is available.",
                1, Mission03Scenario.TurnLimit, seed => Mission03Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(4, Mission04Scenario.MissionCode, "Boarding Party",
                "Weathered enemy crews expose an opportunity. Close the distance and send a boarding party, but watch the debris field.",
                "Defeat both frigates. Bonus: a successful boarding action; no friendly ship sunk.",
                6, Mission04Scenario.TurnLimit, seed => Mission04Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(5, Mission05Scenario.MissionCode, "Line Break",
                "The enemy sails in formation. Concentrate fire to break their line before they surround your fleet.",
                "Defeat the enemy line. Bonus: sink its lead ship first; finish within 9 turns.",
                2, Mission05Scenario.TurnLimit, seed => Mission05Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(6, Mission06Scenario.MissionCode, "Dreadnought Siege",
                "The dreadnought guards the passage. Reinforcements arrive on turn 5; the flagship grows dangerous as her hull fails.",
                "Sink the dreadnought and its reinforcement. Defeat the flagship swiftly and survive its rage.",
                2, Mission06Scenario.TurnLimit, seed => Mission06Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(7, Mission07Scenario.MissionCode, "Burning Seas",
                "Sustained broadsides ignite decks and tear rigging. Your shipyard upgrades can help weather the flames.",
                "Defeat both frigates. Bonus: ignite an enemy; win without a friendly ship catching fire.",
                21, Mission07Scenario.TurnLimit, seed => Mission07Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(8, Mission08Scenario.MissionCode, "Eye of the Wind",
                "A headwind limits how quickly the helm answers. Plan your turns with the wind instead of fighting it.",
                "Win the battle. Bonus: no maneuver clamped by the wind; victory within 8 turns.",
                9, Mission08Scenario.TurnLimit, seed => Mission08Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(9, Mission09Scenario.MissionCode, "Iron Bow",
                "Close at speed to ram with your bow. A collision damages both ships, so protect your own timbers.",
                "Defeat the brigs. Bonus: deliver at least 2 rams; avoid being rammed by an enemy.",
                87, Mission09Scenario.TurnLimit, seed => Mission09Scenario.BuildExpectedStart(seed).State),
            new CampaignMissionDefinition(10, Mission10Scenario.MissionCode, "Sail-Cutter",
                "Ball sinks ships; chain cuts their wings. Shred the clippers' sails, then switch to round shot to finish them.",
                "Defeat both clippers. Bonus: 60 chain-shot sail damage; land both chain and round shot.",
                872, Mission10Scenario.TurnLimit, seed => Mission10Scenario.BuildExpectedStart(seed).State)
        });
    }
}
