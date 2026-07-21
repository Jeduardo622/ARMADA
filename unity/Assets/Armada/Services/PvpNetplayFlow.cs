using System.Collections.Generic;
using System.Threading.Tasks;
using Armada.Client.Core;

namespace Armada.Client.Services
{
    /// <summary>
    /// One resolved match turn ready for spectator playback: the server's
    /// turn record plus the client's snapshot of the fleet as it stood when
    /// the turn began (display bookkeeping only — the server state is the
    /// authority).
    /// </summary>
    public sealed class PvpPlaybackTurn
    {
        public Mission01TurnRecord Record { get; init; }
        public List<SimShip> ShipsAtTurnStart { get; init; }
    }

    /// <summary>
    /// Drives the networked PvP match flow against the server-authoritative
    /// pvp_api routes: create or join by code, submit ONLY this client's
    /// side each turn (bound to the match's current turn number), and poll
    /// until the server resolves the turn. The client never holds
    /// authoritative match state — every view comes from the server, and
    /// the only client-kept snapshot exists so playback can animate a turn
    /// from its starting positions.
    /// </summary>
    public sealed class PvpNetplayFlow
    {
        private readonly IPvpMatchClient _client;

        // Fleet as of the start of the next unplayed turn; seeds playback.
        private SimState _turnStartState;
        private int _playedTurns;

        public string MatchId { get; private set; }
        public string YourSide { get; private set; }
        public PvpMatchView View { get; private set; }

        public PvpNetplayFlow(IPvpMatchClient client)
        {
            _client = client;
        }

        /// <summary>Engine side ('player'/'enemy') for this client's seat.</summary>
        public string EngineSide =>
            YourSide == "side_a" ? "player" : YourSide == "side_b" ? "enemy" : null;

        public List<SimShip> OwnLivingShips() => LivingShips(EngineSide);

        public List<SimShip> EnemyLivingShips() =>
            LivingShips(EngineSide == "player" ? "enemy" : "player");

        public async Task<ServiceResult<PvpMatchResponse>> CreateAsync()
        {
            var result = await _client.CreateMatchAsync();
            AdoptView(result.Data?.Match);
            return result;
        }

        public async Task<ServiceResult<PvpMatchResponse>> JoinAsync(string code)
        {
            var result = await _client.JoinMatchAsync(code?.Trim().ToUpperInvariant());
            AdoptView(result.Data?.Match);
            return result;
        }

        /// <summary>
        /// Submits this side's orders bound to the server's current turn
        /// number. The response view replaces the local one; if the turn
        /// resolved immediately (we were the second submitter), the playback
        /// turn is queued exactly as if a poll had discovered it.
        /// </summary>
        public async Task<ServiceResult<PvpSubmitOrdersResponse>> SubmitOrdersAsync(List<SimOrder> orders)
        {
            var result = await _client.SubmitOrdersAsync(MatchId, new PvpSubmitOrdersRequest
            {
                TurnNumber = View?.TurnNumber ?? 1,
                Orders = orders
            });
            if (result.Success && result.Data?.Match != null)
            {
                RefreshView(result.Data.Match);
            }

            return result;
        }

        /// <summary>Polls the server view (used while waiting for the
        /// opponent to join or to submit).</summary>
        public async Task<ServiceResult<PvpMatchResponse>> PollAsync()
        {
            var result = await _client.GetMatchAsync(MatchId);
            if (result.Success && result.Data?.Match != null)
            {
                RefreshView(result.Data.Match);
            }

            return result;
        }

        /// <summary>
        /// Dequeues the next resolved-but-unplayed turn, if any. The client
        /// can be at most one turn behind: a turn cannot resolve without
        /// this side's own submission.
        /// </summary>
        public bool TryDequeuePlaybackTurn(out PvpPlaybackTurn playback)
        {
            playback = null;
            var turns = View?.Turns;
            if (turns == null || _playedTurns >= turns.Count)
            {
                return false;
            }

            playback = new PvpPlaybackTurn
            {
                Record = turns[_playedTurns],
                ShipsAtTurnStart = _turnStartState?.Ships
            };
            _playedTurns++;
            // The server view's state is the fleet after every resolved
            // turn, which is exactly the start of the next one.
            _turnStartState = View.State;
            return true;
        }

        private void AdoptView(PvpMatchView match)
        {
            if (match == null)
            {
                return;
            }

            MatchId = match.Id;
            YourSide = match.YourSide;
            View = match;
            _turnStartState = match.State;
            _playedTurns = match.Turns?.Count ?? 0;
        }

        private void RefreshView(PvpMatchView match)
        {
            // Monotonic guard: a poll started before a submission can land
            // after it; its pre-resolution view must never overwrite a newer
            // one (the server's turnNumber only ever advances).
            if (View != null && match.TurnNumber < View.TurnNumber)
            {
                return;
            }

            // _turnStartState deliberately lags behind: it advances only
            // when TryDequeuePlaybackTurn consumes a resolved turn.
            View = match;
            YourSide = match.YourSide ?? YourSide;
        }

        private List<SimShip> LivingShips(string engineSide)
        {
            var ships = new List<SimShip>();
            var current = View?.State?.Ships;
            if (current == null || engineSide == null)
            {
                return ships;
            }

            foreach (var ship in current)
            {
                if (ship != null && ship.Side == engineSide && ship.Hp > 0)
                {
                    ships.Add(ship);
                }
            }

            return ships;
        }
    }
}
