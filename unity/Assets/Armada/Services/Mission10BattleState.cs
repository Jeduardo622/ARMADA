using System.Collections.Generic;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// Running ship snapshot for a playable Mission 10 run. The event-stream
    /// reader itself is mission-agnostic and lives in
    /// <see cref="MissionBattleState"/>, shared with the campaign's
    /// mission-agnostic play loop; this name stays for the Mission 10 play
    /// scene and its pinned tests.
    /// </summary>
    public sealed class Mission10BattleState : MissionBattleState
    {
        public Mission10BattleState(IReadOnlyList<SimShip> initialShips)
            : base(initialShips)
        {
        }
    }
}
