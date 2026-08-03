using System.Collections.Generic;
using Armada.Client.Core;
using Armada.Client.Services;
using TMPro;
using UnityEngine;

namespace Armada.Client.Playback
{
    /// <summary>
    /// Spectate-only renderer for a resolved Mission 10 run: spawns a
    /// placeholder primitive marker per ship, then animates the TurnPlayback
    /// step stream — movement lerps, maneuver rotations, broadside/ram
    /// flashes (chain shot flashes a distinct color: it is the mission's
    /// showcase mechanic), and a HUD line per step. No player input. Tests
    /// drive Tick directly and assert component state, never rendered output.
    /// </summary>
    public sealed class SpectatorRenderer : MonoBehaviour
    {
        // All constants below are design-tunable placeholders pending real
        // art/UX direction; primitives and flat colors only.
        [Header("Board scale (design-tunable placeholder)")]
        [Tooltip("Sim coords are ints, x roughly 0-250 and y roughly ±60; this scales them to world units.")]
        [SerializeField] private float worldUnitsPerSimUnit = 0.1f;
        [SerializeField] private float markerHeight = 0.5f;

        [Header("Step timing seconds (design-tunable placeholders)")]
        [SerializeField] private float turnBannerSeconds = 0.5f;
        [SerializeField] private float maneuverSeconds = 0.2f;
        [SerializeField] private float moveSeconds = 0.35f;
        [SerializeField] private float flashSeconds = 0.45f;

        [Header("Colors (design-tunable placeholders)")]
        [Tooltip("Aurorian Empire hull navy (art-direction §1 faction identity); must stay legible against the sea mid band and the deep-sea sunk tint.")]
        [SerializeField] private Color playerColor = new Color(0.13f, 0.25f, 0.60f);
        [Tooltip("Aurorian brass: authored sail/flag accent over the navy hull — the faction read from the top-down camera, where sails dominate the silhouette.")]
        [SerializeField] private Color playerAccentColor = new Color(0.80f, 0.64f, 0.28f);
        [Tooltip("Crimson Republic hull crimson; deeper than the old placeholder red for separation from the round-shot amber flash.")]
        [SerializeField] private Color enemyColor = new Color(0.72f, 0.11f, 0.18f);
        [Tooltip("Crimson sun-bleached canvas: authored sail/flag accent (matches the previous derived lightening, now explicit so both factions use the same seam).")]
        [SerializeField] private Color enemyAccentColor = new Color(0.83f, 0.47f, 0.51f);
        [SerializeField] private Color roundShotFlashColor = new Color(1.00f, 0.72f, 0.05f);
        [Tooltip("Chain shot is the Mission 10 showcase; its flash must read distinct from round shot.")]
        [SerializeField] private Color chainShotFlashColor = new Color(0.20f, 0.90f, 1.00f);
        [SerializeField] private Color ramFlashColor = Color.white;
        [Tooltip("Boarding actions were visually identical to rams before W2 slice 2; violet is distinct from every shot and ram color.")]
        [SerializeField] private Color boardingFlashColor = new Color(0.75f, 0.40f, 1.00f);

        [Header("Playback controls (design-tunable placeholder bindings)")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Space;
        [SerializeField] private KeyCode stepKey = KeyCode.RightArrow;
        [SerializeField] private KeyCode speedUpKey = KeyCode.Equals;
        [SerializeField] private KeyCode speedDownKey = KeyCode.Minus;
        [Tooltip("Speed presets bound to keys 1-4; +/- cycle through them.")]
        [SerializeField] private float[] speedPresets = { 0.5f, 1f, 2f, 4f };

        [Header("Readout bars (design-tunable placeholders)")]
        [SerializeField] private float barWidth = 1.2f;
        [Tooltip("Gap between the view's reported top (ShipView.TopClearance) and the bars; the lift is derived per view, never hardcoded to a shape.")]
        [SerializeField] private float barClearance = 0.4f;
        [SerializeField] private Color hullBarColor = new Color(0.40f, 0.95f, 0.40f);
        [SerializeField] private Color sailBarColor = new Color(0.95f, 0.90f, 0.55f);

        [Header("UI Wiring")]
        [SerializeField] private TMP_Text hudLabel;
        [Tooltip("W4 conditions zone (art-direction.md §7 item 3): compact numeric wind/turn readout; optional — scenes without the label just skip it.")]
        [SerializeField] private TMP_Text conditionsLabel;

        [Header("Camera follow (optional; wired by the PvP scenes)")]
        [Tooltip("When set, the orthographic camera re-frames every tick to keep all markers in view; null keeps the scene's fixed authored framing (mission scenes).")]
        [SerializeField] private Camera followCamera;
        [Tooltip("World units of margin kept around the outermost markers.")]
        [SerializeField] private float followPadding = 2f;
        [Tooltip("Zoom floor (W3): the camera may tighten well below the authored opening as the fight concentrates, but never past this. Was 8.5 (the opening size), which kept the endgame tiny in an empty frame.")]
        [SerializeField] private float followMinSize = 5f;
        [Tooltip("Seconds for the follow camera to close ~63% of the gap to its target framing; 0 snaps instantly (W3).")]
        [SerializeField] private float followSmoothingSeconds = 0.25f;

        [Header("Board features & wind (design-tunable placeholders)")]
        [Tooltip("Impassable terrain (rocks/islands): dark cylinders scaled by sim radius.")]
        [SerializeField] private Color obstacleColor = new Color(0.25f, 0.22f, 0.18f);

        [Header("Board feature prefabs (optional; primitives when empty)")]
        [Tooltip("Authored rock variants; an obstacle picks one deterministically from its sim position. Empty falls back to the pre-art cylinder.")]
        [SerializeField] private GameObject[] rockPrefabs;
        [Tooltip("Authored debris patch for slow zones. Null falls back to the pre-art disc.")]
        [SerializeField] private GameObject debrisPrefab;
        [Tooltip("Hazard slow zones (debris): pale translucent discs scaled by sim radius.")]
        [SerializeField] private Color slowZoneColor = new Color(0.55f, 0.62f, 0.60f, 0.5f);
        [Tooltip("World offset of the wind arrow from the fleet centroid; re-anchored every tick so the follow camera never loses it.")]
        [SerializeField] private Vector3 windIndicatorOffset = new Vector3(0f, 0.5f, -4.5f);
        [SerializeField] private Color windIndicatorColor = new Color(0.85f, 0.9f, 1.0f, 0.9f);

        [Header("View provider (optional; primitives when null)")]
        [Tooltip("Factory for ship visuals. Null adds the primitive provider at first spawn; an art-backed provider replaces it without a renderer change (W2).")]
        [SerializeField] private ShipViewProvider shipViewProvider;

        private sealed class Marker
        {
            public ShipView View;
            public Transform Transform;
            public Renderer Renderer;
            public Color BaseColor;
            public Vector3 MoveFrom;
            public Vector3 MoveTo;
            public Transform HullBar;
            public Transform SailBar;
            public int MaxHull;
            public int MaxSail;
        }

        private readonly Dictionary<string, Marker> _markers = new();
        private readonly List<GameObject> _boardFeatures = new();
        private Transform _windArrow;
        private SimWind _wind;
        private TurnPlayback _playback;
        private Mission10Outcome _outcome;
        private int _turnLimit;
        private int _currentTurn;
        private string _completionLine;
        private PlaybackStep _currentStep;
        private float _stepElapsed;
        private float _stepDuration;
        private bool _stepArmed;
        private string _lastMessage;

        /// <summary>Step currently being animated; test hook, may be null.</summary>
        public PlaybackStep CurrentStep => _currentStep;

        /// <summary>Last HUD line written (narration plus control status); test hook.</summary>
        public string HudText { get; private set; }

        /// <summary>Last conditions readout written (wind/turn numerics); test hook.</summary>
        public string ConditionsText { get; private set; }

        public bool IsFinished { get; private set; }

        /// <summary>While paused, ticks do nothing unless a single step is armed.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Scales elapsed time while a step animates; 1 is real time.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public void Pause()
        {
            IsPaused = true;
            _stepArmed = false;
            RefreshHudLabel();
        }

        public void Resume()
        {
            IsPaused = false;
            _stepArmed = false;
            RefreshHudLabel();
        }

        /// <summary>
        /// While paused, arms exactly one playback step: subsequent ticks run
        /// normally until the step ends, then playback freezes again.
        /// </summary>
        public void StepOnce()
        {
            if (IsPaused)
            {
                _stepArmed = true;
            }
        }

        public void SetSpeed(float multiplier)
        {
            // Clamp bounds are design-tunable placeholders.
            SpeedMultiplier = Mathf.Clamp(multiplier, 0.25f, 8f);
            RefreshHudLabel();
        }

        /// <summary>
        /// Entry point used by Mission10Bootstrap after the flow resolves.
        /// Failed runs surface the failure on the HUD instead of animating.
        /// </summary>
        public void Begin(Mission10FlowResult run)
        {
            if (run == null || !run.Success || run.Outcome == null)
            {
                IsFinished = true;
                SetHud($"Run failed: {run?.FailureReason ?? "no result"}");
                return;
            }

            BeginOutcome(run.Outcome);
        }

        /// <summary>
        /// Starts playback for a resolved outcome. Markers spawn at the pinned
        /// scenario start positions for the outcome's seed, then the event
        /// stream moves them.
        /// </summary>
        public void BeginOutcome(Mission10Outcome outcome)
        {
            var start = Mission10Scenario.BuildExpectedStart(outcome.Seed).State;
            BeginTurns(
                start.Ships,
                outcome.Turns,
                outcome.TurnLimit,
                $"Spectating {outcome.MissionCode} (seed {outcome.Seed})...",
                completionLine: null,
                wind: start.Wind);
            _outcome = outcome;
        }

        /// <summary>
        /// Generic playback entry (PvP hot-seat and other non-mission
        /// consumers): markers spawn at the supplied ship snapshot, then the
        /// turn records animate them. The completion line replaces the
        /// mission outcome summary on finish; applied per-side loss totals
        /// are appended either way. Callers replaying mid-battle snapshots
        /// (per-turn hot-seat playback) pass the battle-start ships as
        /// baselineShips so the HP/sail readout bars keep the true maxima
        /// instead of re-baselining to the snapshot values.
        /// </summary>
        public void BeginTurns(
            IReadOnlyList<SimShip> shipsAtStart,
            IReadOnlyList<Mission01TurnRecord> turns,
            int turnLimit,
            string introLine,
            string completionLine,
            IReadOnlyList<SimShip> baselineShips = null,
            IReadOnlyList<SimObstacle> obstacles = null,
            IReadOnlyList<SimSlowZone> slowZones = null,
            SimWind wind = null)
        {
            ClearMarkers();
            SpawnBoardFeatures(obstacles, slowZones);
            _outcome = null;
            _turnLimit = turnLimit;
            _currentTurn = 0;
            _completionLine = completionLine;
            _currentStep = null;
            _stepArmed = false;
            IsFinished = false;

            SpawnMarkers(shipsAtStart, baselineShips);
            // After the markers: the arrow anchors to the fleet centroid,
            // which is empty until they exist (Codex P2 on #89).
            SetWind(wind);
            RefreshConditionsLabel();

            _playback = new TurnPlayback(shipsAtStart, turns);
            SetHud(introLine);
        }

        /// <summary>
        /// Re-spawns the markers at a supplied ship snapshot and queues no
        /// playback: the board simply *is* that state.
        ///
        /// Used when the board must jump to a state the renderer never
        /// animated into — the playable Mission 10 loop's opening position,
        /// and its undo, which rewinds to the turn being re-authored. Without
        /// this the renderer would keep showing the withdrawn turn's end
        /// positions and damage while the player writes replacement orders.
        /// </summary>
        public void ShowBoard(
            IReadOnlyList<SimShip> ships,
            string hudLine,
            IReadOnlyList<SimShip> baselineShips = null,
            IReadOnlyList<SimObstacle> obstacles = null,
            IReadOnlyList<SimSlowZone> slowZones = null,
            SimWind wind = null)
        {
            ClearMarkers();
            SpawnBoardFeatures(obstacles, slowZones);
            _outcome = null;
            _playback = null;
            _currentStep = null;
            _stepArmed = false;
            // Nothing is queued, so playback is trivially done; consumers
            // polling IsFinished must not wait on a run that will never step.
            IsFinished = true;

            SpawnMarkers(ships, baselineShips);
            // After the markers, for the same anchoring reason as BeginTurns;
            // ShowBoard has no tick loop to correct a stale position later.
            SetWind(wind);
            // No playback is queued, so no turn number is authoritative here;
            // the conditions readout drops to wind-only until the next run.
            _currentTurn = 0;
            RefreshConditionsLabel();
            SetHud(hudLine);
        }

        // Spawns one marker per ship, taking readout-bar maxima from the
        // matching baseline ship when one is supplied (mid-battle snapshots
        // must keep the battle-start maxima, not re-baseline to themselves).
        private void SpawnMarkers(IReadOnlyList<SimShip> ships, IReadOnlyList<SimShip> baselineShips)
        {
            var baselineById = new Dictionary<string, SimShip>();
            if (baselineShips != null)
            {
                foreach (var ship in baselineShips)
                {
                    if (ship?.Id != null)
                    {
                        baselineById[ship.Id] = ship;
                    }
                }
            }

            if (ships == null)
            {
                return;
            }

            foreach (var ship in ships)
            {
                SpawnMarker(ship, baselineById.TryGetValue(ship.Id ?? string.Empty, out var baseline) ? baseline : ship);
            }
        }

        public bool TryGetMarkerPosition(string shipId, out Vector3 position)
        {
            if (shipId != null && _markers.TryGetValue(shipId, out var marker))
            {
                position = marker.Transform.position;
                return true;
            }

            position = default;
            return false;
        }

        private void Update()
        {
            PollControls();
            Tick(Time.deltaTime);
        }

        // Legacy Input manager polling (no Input System package in-project);
        // bindings and presets are design-tunable placeholders. Kept out of
        // Tick so tests drive controls through the public methods instead.
        private void PollControls()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                if (IsPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }

            if (IsPaused && Input.GetKeyDown(stepKey))
            {
                StepOnce();
            }

            for (var i = 0; speedPresets != null && i < speedPresets.Length && i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SetSpeed(speedPresets[i]);
                }
            }

            if (Input.GetKeyDown(speedUpKey) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                CycleSpeedPreset(1);
            }
            if (Input.GetKeyDown(speedDownKey) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                CycleSpeedPreset(-1);
            }
        }

        /// <summary>
        /// Steps to the neighbouring speed preset (+1 faster, −1 slower),
        /// clamped at the ends. Shared by the keyboard bindings above and the
        /// on-screen playback buttons (D2-B touch controls).
        /// </summary>
        public void CycleSpeedPreset(int direction)
        {
            if (speedPresets == null || speedPresets.Length == 0)
            {
                return;
            }

            var nearest = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < speedPresets.Length; i++)
            {
                var distance = Mathf.Abs(speedPresets[i] - SpeedMultiplier);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = i;
                }
            }

            SetSpeed(speedPresets[Mathf.Clamp(nearest + direction, 0, speedPresets.Length - 1)]);
        }

        /// <summary>
        /// Advances playback by dt seconds. A tick either begins the next
        /// step or advances the active one, never both, so tests stepping
        /// with fixed dt values see every step deterministically. Paused
        /// playback ignores ticks unless a single step is armed; the speed
        /// multiplier scales dt on the advance path only.
        /// </summary>
        public void Tick(float dt)
        {
            // Playback advances first, camera second: the frame must fit the
            // positions the step animation just produced (Codex P1 on #90),
            // and the camera keeps settling after the battle ends or while
            // paused because this tail runs on every tick.
            TickPlayback(dt);
            UpdateFollowCamera(dt * SpeedMultiplier);
        }

        private void TickPlayback(float dt)
        {
            if (_playback == null || IsFinished)
            {
                return;
            }

            if (IsPaused && !_stepArmed)
            {
                return;
            }

            if (_currentStep == null)
            {
                if (!_playback.TryStep(out var step))
                {
                    _stepArmed = false;
                    FinishRun();
                    UpdateReadouts();
                    return;
                }

                BeginStep(step);
                UpdateReadouts();
                return;
            }

            _stepElapsed += dt * SpeedMultiplier;
            var progress = PlaybackEase.Progress(_stepElapsed, _stepDuration);
            AnimateStep(progress);
            if (_stepElapsed >= _stepDuration)
            {
                EndStep();
                _stepArmed = false;
            }

            UpdateReadouts();
        }


        // Keeps every LIVING marker inside the (orthographic, top-down) view
        // once windMovement lets ships sail beyond the authored opening
        // framing, tightening toward the action as the fight concentrates
        // (W3): wrecks stop pinning the frame open, the zoom floor is
        // followMinSize rather than the authored opening size, and the
        // camera eases toward its target framing instead of snapping.
        // Null camera (mission spectator scene) keeps the fixed framing.
        private void UpdateFollowCamera(float dt)
        {
            if (followCamera == null || _markers.Count == 0)
            {
                return;
            }

            var first = true;
            var min = Vector3.zero;
            var max = Vector3.zero;
            var anyAfloat = false;
            foreach (var marker in _markers.Values)
            {
                if (marker.Transform == null)
                {
                    continue;
                }

                // Sunk wrecks stay on the sea but no longer drive framing;
                // if everything is sunk, frame the wreckage instead.
                if (marker.View != null && marker.View.IsSunk)
                {
                    continue;
                }

                anyAfloat = true;
                Accumulate(marker.Transform.position, ref first, ref min, ref max);
            }

            if (!anyAfloat)
            {
                foreach (var marker in _markers.Values)
                {
                    if (marker.Transform != null)
                    {
                        Accumulate(marker.Transform.position, ref first, ref min, ref max);
                    }
                }
            }

            if (first)
            {
                return;
            }

            var center = (min + max) * 0.5f;

            // Screen-up for the top-down camera is world +z; world x maps to
            // screen x, scaled by the aspect ratio.
            var halfZ = (max.z - min.z) * 0.5f + followPadding;
            var aspect = followCamera.aspect > 0f ? followCamera.aspect : 1f;
            var halfXAsSize = ((max.x - min.x) * 0.5f + followPadding) / aspect;
            var targetSize = Mathf.Max(followMinSize, halfZ, halfXAsSize);

            // Exponential ease: framerate-independent for any fixed tick and
            // deterministic for the capture harness.
            var blend = followSmoothingSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-dt / followSmoothingSeconds);

            var cameraTransform = followCamera.transform;
            var current = cameraTransform.position;
            var target = new Vector3(center.x, current.y, center.z);
            var easedPosition = Vector3.Lerp(current, target, blend);
            var easedSize = Mathf.Lerp(followCamera.orthographicSize, targetSize, blend);

            // Containment is a hard guarantee, easing is not (Codex P1 on
            // #90): whatever the eased center is this tick, the size expands
            // instantly to keep every framed position inside the view, so a
            // fast ship can never outrun the lagging frame. Inward
            // tightening keeps the smooth ease.
            var halfZNeeded = Mathf.Max(
                max.z + followPadding - easedPosition.z,
                easedPosition.z - (min.z - followPadding));
            var halfXNeeded = Mathf.Max(
                max.x + followPadding - easedPosition.x,
                easedPosition.x - (min.x - followPadding)) / aspect;

            cameraTransform.position = easedPosition;
            followCamera.orthographicSize = Mathf.Max(easedSize, halfZNeeded, halfXNeeded);
        }

        private static void Accumulate(Vector3 position, ref bool first, ref Vector3 min, ref Vector3 max)
        {
            if (first)
            {
                min = position;
                max = position;
                first = false;
            }
            else
            {
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }
        }

        private void BeginStep(PlaybackStep step)
        {
            _currentStep = step;
            _stepElapsed = 0f;

            switch (step.Kind)
            {
                case PlaybackStepKind.TurnStart:
                    _stepDuration = turnBannerSeconds;
                    _currentTurn = step.Turn;
                    RefreshConditionsLabel();
                    SetHud($"Turn {step.Turn}/{_turnLimit}");
                    break;
                case PlaybackStepKind.Maneuver:
                    _stepDuration = maneuverSeconds;
                    if (step.Heading.HasValue && TryGetMarker(step.ShipId, out var maneuvering))
                    {
                        maneuvering.Transform.rotation = HeadingToRotation(step.Heading.Value);
                    }
                    break;
                case PlaybackStepKind.Move:
                    _stepDuration = moveSeconds;
                    if (TryGetMarker(step.ShipId, out var moving))
                    {
                        moving.MoveFrom = moving.Transform.position;
                        moving.MoveTo = ToWorld(step.X.Value, step.Y.Value);
                    }
                    break;
                case PlaybackStepKind.Broadside:
                    _stepDuration = flashSeconds;
                    Flash(step.ShipId, BroadsideFlashColor(step));
                    if (step.Rake != null && step.Hit)
                    {
                        // Rake flourish: the victim shudders in the shot color
                        // too — the showcase tactic reads on the board, not
                        // just in the narration.
                        Flash(step.TargetShipId, BroadsideFlashColor(step));
                    }
                    SetHud(step.Hit
                        ? $"T{step.Turn} {step.ShipId} => {step.TargetShipId}: {(step.ChainShot ? "CHAIN SHOT" : "round shot")}{RakeSuffix(step)} hit (hull -{step.AppliedHull}, sail -{step.AppliedSail}, crew -{step.AppliedCrew}){(step.TargetSunk ? " — SUNK!" : string.Empty)}"
                        : $"T{step.Turn} {step.ShipId} => {step.TargetShipId}: {(step.ChainShot ? "CHAIN SHOT" : "round shot")} miss");
                    break;
                case PlaybackStepKind.Ram:
                    _stepDuration = flashSeconds;
                    Flash(step.ShipId, ramFlashColor);
                    Flash(step.TargetShipId, ramFlashColor);
                    SetHud($"T{step.Turn} {step.ShipId} rams {step.TargetShipId} (hull -{step.AppliedHull}, recoil -{step.SelfAppliedHull}){(step.TargetSunk ? " — SUNK!" : string.Empty)}");
                    break;
                case PlaybackStepKind.Boarding:
                    _stepDuration = flashSeconds;
                    Flash(step.ShipId, boardingFlashColor);
                    SetHud($"T{step.Turn} {step.ShipId} boards {step.TargetShipId}: {(step.Hit ? "success" : "repelled")} (crew -{step.AppliedCrew})");
                    break;
                case PlaybackStepKind.Status:
                    _stepDuration = maneuverSeconds;
                    if (TryGetMarker(step.ShipId, out var statused) && statused.View != null)
                    {
                        statused.View.SetStatus(step.OnFire, step.Slowed);
                        SetHud(step.OnFire
                            ? $"T{step.Turn} {step.ShipId} is ABLAZE{(step.AppliedHull > 0 ? $" (burns hull -{step.AppliedHull})" : string.Empty)}"
                            : step.Slowed
                                ? $"T{step.Turn} {step.ShipId} is slowed, rigging fouled"
                                : $"T{step.Turn} {step.ShipId} recovers");
                    }
                    break;
                case PlaybackStepKind.RunComplete:
                    _stepDuration = 0f;
                    break;
            }
        }

        private void AnimateStep(float progress)
        {
            var step = _currentStep;
            switch (step.Kind)
            {
                case PlaybackStepKind.Move:
                    if (TryGetMarker(step.ShipId, out var moving))
                    {
                        moving.Transform.position = Vector3.Lerp(moving.MoveFrom, moving.MoveTo, progress);
                    }
                    break;
                case PlaybackStepKind.Broadside:
                    FadeFlash(step.ShipId, BroadsideFlashColor(step), progress);
                    if (step.Rake != null && step.Hit)
                    {
                        FadeFlash(step.TargetShipId, BroadsideFlashColor(step), progress);
                    }
                    break;
                case PlaybackStepKind.Ram:
                    FadeFlash(step.ShipId, ramFlashColor, progress);
                    FadeFlash(step.TargetShipId, ramFlashColor, progress);
                    break;
                case PlaybackStepKind.Boarding:
                    FadeFlash(step.ShipId, boardingFlashColor, progress);
                    break;
            }
        }

        private void EndStep()
        {
            var step = _currentStep;
            _currentStep = null;
            switch (step.Kind)
            {
                case PlaybackStepKind.Move:
                    if (TryGetMarker(step.ShipId, out var moved))
                    {
                        moved.Transform.position = moved.MoveTo;
                    }
                    break;
                case PlaybackStepKind.Broadside:
                case PlaybackStepKind.Ram:
                case PlaybackStepKind.Boarding:
                    RestoreColor(step.ShipId);
                    RestoreColor(step.TargetShipId);
                    break;
                case PlaybackStepKind.RunComplete:
                    FinishRun();
                    break;
            }
        }

        private void FinishRun()
        {
            IsFinished = true;
            if (_outcome == null)
            {
                // Generic (non-mission) playback: report the caller's line
                // plus both sides' applied (remaining-delta) loss totals.
                var sideA = _playback.PlayerInflicted;
                var sideB = _playback.EnemyInflicted;
                SetHud(
                    $"{_completionLine ?? "Playback complete"}"
                    + $" | side A applied: hull {sideA.Hull}, sail {sideA.Sail}, crew {sideA.Crew}"
                    + $" | side B applied: hull {sideB.Hull}, sail {sideB.Sail}, crew {sideB.Crew}");
                return;
            }

            var bonuses = _outcome?.BonusObjectives;
            var inflicted = _playback.PlayerInflicted;
            SetHud(
                $"Result: {_outcome?.Result} at turn {_outcome?.TurnCount}/{_outcome?.TurnLimit}"
                + $" | bonuses: sailShredder={(bonuses?.SailShredder == true ? "yes" : "no")}, mixedBattery={(bonuses?.MixedBattery == true ? "yes" : "no")}"
                + $" | applied to enemy: hull {inflicted.Hull}, sail {inflicted.Sail}, crew {inflicted.Crew}");
        }

        private ShipViewProvider Provider
        {
            get
            {
                if (shipViewProvider == null)
                {
                    // A provider living on this GameObject wins before the
                    // primitive default is created — the documented no-wiring
                    // extension path (Codex P2 on PR #87).
                    shipViewProvider = GetComponent<ShipViewProvider>();
                }

                if (shipViewProvider == null)
                {
                    // Pre-art default: the primitive views every scene shipped
                    // with, now behind the same seam a prefab provider uses.
                    shipViewProvider = gameObject.AddComponent<PrimitiveShipViewProvider>();
                }

                return shipViewProvider;
            }
        }

        /// <summary>
        /// Sim heading to view yaw. The engine moves ships along
        /// (cos h, sin h) in sim x/y — world x/z at this renderer's scale —
        /// and a view's bow is its local +z, so yaw must be 90 − h for the
        /// bow to track the motion. The legacy yaw = h mapping was wrong for
        /// every heading off the diagonal, invisible only because symmetric
        /// primitives had no bow to contradict it (W2 bow-cue finding).
        /// </summary>
        private static Quaternion HeadingToRotation(float heading)
        {
            return Quaternion.Euler(0f, 90f - heading, 0f);
        }

        // Misses read as a muted half-flash instead of the same triumphant
        // color as a hit (W2 slice 2: the GDD wants splash-on-miss feedback;
        // the dim flash is its pre-art stand-in).
        private Color BroadsideFlashColor(PlaybackStep step)
        {
            var color = step.ChainShot ? chainShotFlashColor : roundShotFlashColor;
            return step.Hit ? color : Color.Lerp(color, Color.gray, 0.6f);
        }

        private static string RakeSuffix(PlaybackStep step)
        {
            return step.Rake == null ? string.Empty : $" {step.Rake.ToUpperInvariant()} RAKE";
        }

        private void SpawnMarker(SimShip ship)
        {
            SpawnMarker(ship, ship);
        }

        // baselineShip supplies the readout-bar maxima; it differs from the
        // spawned snapshot only for mid-battle playback (hot-seat turns).
        private void SpawnMarker(SimShip ship, SimShip baselineShip)
        {
            var view = Provider.CreateShipView(ship, transform);
            view.transform.position = ToWorld(ship.Position.X, ship.Position.Y);
            view.transform.rotation = HeadingToRotation(ship.Heading);

            var baseColor = ship.Side == "player" ? playerColor : enemyColor;
            var accentColor = ship.Side == "player" ? playerAccentColor : enemyAccentColor;
            view.SetBaseTint(baseColor, accentColor);

            var marker = new Marker
            {
                View = view,
                Transform = view.transform,
                Renderer = view.TintRenderer,
                BaseColor = baseColor,
                MaxHull = Mathf.Max(1, baselineShip.Hp),
                MaxSail = Mathf.Max(1, baselineShip.Sail),
                HullBar = SpawnBar($"hull-bar-{ship.Id}", hullBarColor),
                SailBar = SpawnBar($"sail-bar-{ship.Id}", sailBarColor)
            };
            _markers[ship.Id] = marker;

            // Snapshot-only boards (mission 10 undo) can contain ships that
            // are already sunk; there is no playback to sink them later
            // (Codex P2 on #89).
            if (ship.Hp <= 0)
            {
                SinkMarker(marker);
                return;
            }

            PositionBars(
                marker,
                Mathf.Clamp01(ship.Hp / (float)marker.MaxHull),
                Mathf.Clamp01(ship.Sail / (float)marker.MaxSail));
        }

        private static void SinkMarker(Marker marker)
        {
            if (marker.View != null)
            {
                marker.View.SetSunk();
            }

            if (marker.HullBar != null)
            {
                marker.HullBar.gameObject.SetActive(false);
            }

            if (marker.SailBar != null)
            {
                marker.SailBar.gameObject.SetActive(false);
            }
        }

        // Bars parent to the renderer, not the marker, so heading rotations
        // never spin them; UpdateReadouts re-anchors them above the marker.
        private Transform SpawnBar(string name, Color color)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(transform, worldPositionStays: false);
            bar.transform.localScale = new Vector3(barWidth, 0.08f, 0.12f);

            var barRenderer = bar.GetComponent<Renderer>();
            if (barRenderer != null)
            {
                barRenderer.material.color = color;
            }

            return bar.transform;
        }

        private void UpdateReadouts()
        {
            foreach (var pair in _markers)
            {
                var marker = pair.Value;
                if (marker.Transform == null || _playback == null
                    || !_playback.TryGetRemaining(pair.Key, out var remaining))
                {
                    continue;
                }

                // One sink mechanism for every kill path (broadside, ram
                // victim, ram recoil, fire DoT): the authoritative remaining
                // block reaching zero hull sinks the view and hides its bars.
                if (remaining.Hp <= 0 && marker.View != null && !marker.View.IsSunk)
                {
                    SinkMarker(marker);
                }

                if (marker.View != null && marker.View.IsSunk)
                {
                    continue;
                }

                PositionBars(
                    marker,
                    Mathf.Clamp01(remaining.Hp / (float)marker.MaxHull),
                    Mathf.Clamp01(remaining.Sail / (float)marker.MaxSail));
            }

            AnchorWindIndicator();
        }

        private void PositionBars(Marker marker, float hullFraction, float sailFraction)
        {
            // Screen-up for the top-down camera is world +z, so the bars sit
            // just "above" the marker; offsets are design-tunable placeholders.
            // The lift derives from the view's reported top, never a
            // hardcoded shape height (spectator-tuning bar-clearance rule).
            var lift = (marker.View != null ? marker.View.TopClearance : 1f) + barClearance;
            PositionBar(marker.HullBar, marker.Transform.position, lift, 0.45f, hullFraction);
            PositionBar(marker.SailBar, marker.Transform.position, lift, 0.30f, sailFraction);
        }

        private void PositionBar(Transform bar, Vector3 markerPosition, float lift, float zOffset, float fraction)
        {
            if (bar == null)
            {
                return;
            }

            bar.position = markerPosition + new Vector3(0f, lift, zOffset);
            var scale = bar.localScale;
            scale.x = barWidth * fraction;
            bar.localScale = scale;
        }

        /// <summary>
        /// Readout bar fractions (remaining / initial) for a ship, read back
        /// from the bar transforms; test hook.
        /// </summary>
        public bool TryGetReadoutFractions(string shipId, out float hullFraction, out float sailFraction)
        {
            hullFraction = 0f;
            sailFraction = 0f;
            if (shipId == null || !_markers.TryGetValue(shipId, out var marker)
                || marker.HullBar == null || marker.SailBar == null || barWidth <= 0f)
            {
                return false;
            }

            hullFraction = marker.HullBar.localScale.x / barWidth;
            sailFraction = marker.SailBar.localScale.x / barWidth;
            return true;
        }

        private void ClearMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                if (marker.Transform != null)
                {
                    Destroy(marker.Transform.gameObject);
                }
                if (marker.HullBar != null)
                {
                    Destroy(marker.HullBar.gameObject);
                }
                if (marker.SailBar != null)
                {
                    Destroy(marker.SailBar.gameObject);
                }
            }

            _markers.Clear();
        }

        // Board features (W2 slice 3): impassable rocks and debris slow zones
        // were mechanically real but invisible — ships routed around nothing.
        // Pre-art defaults: dark cylinder per obstacle, pale flat disc per
        // slow zone, both scaled by the sim radius.
        private void SpawnBoardFeatures(IReadOnlyList<SimObstacle> obstacles, IReadOnlyList<SimSlowZone> slowZones)
        {
            foreach (var feature in _boardFeatures)
            {
                if (feature != null)
                {
                    Destroy(feature);
                }
            }

            _boardFeatures.Clear();

            if (obstacles != null)
            {
                foreach (var obstacle in obstacles)
                {
                    if (obstacle?.Position == null)
                    {
                        continue;
                    }

                    // Authored rocks (art-needs.md §3 P2) replace the pre-art
                    // cylinder when wired; the variant derives from the sim
                    // position so a given rock never changes between spawns.
                    var radius = obstacle.Radius * worldUnitsPerSimUnit;
                    GameObject rock;
                    if (rockPrefabs != null && rockPrefabs.Length > 0)
                    {
                        var variant = Mathf.Abs(obstacle.Position.X * 31 + obstacle.Position.Y) % rockPrefabs.Length;
                        rock = Instantiate(rockPrefabs[variant], transform);
                        // Authored meshes span a unit footprint at authored
                        // height; only the footprint scales with the radius.
                        rock.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
                        rock.transform.position = new Vector3(
                            obstacle.Position.X * worldUnitsPerSimUnit, 0f, obstacle.Position.Y * worldUnitsPerSimUnit);
                    }
                    else
                    {
                        rock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        rock.transform.SetParent(transform, worldPositionStays: false);
                        rock.transform.position = new Vector3(
                            obstacle.Position.X * worldUnitsPerSimUnit, 0.2f, obstacle.Position.Y * worldUnitsPerSimUnit);
                        rock.transform.localScale = new Vector3(radius * 2f, 0.4f, radius * 2f);
                    }

                    rock.name = $"obstacle-{obstacle.Position.X}-{obstacle.Position.Y}";
                    TintFeature(rock, obstacleColor);
                    _boardFeatures.Add(rock);
                }
            }

            if (slowZones != null)
            {
                foreach (var zone in slowZones)
                {
                    if (zone?.Position == null)
                    {
                        continue;
                    }

                    var radius = zone.Radius * worldUnitsPerSimUnit;
                    GameObject disc;
                    if (debrisPrefab != null)
                    {
                        // Authored debris patch: unit footprint, authored
                        // thinness; its transparent material honors the slow
                        // zone color's alpha (the primitive never could).
                        disc = Instantiate(debrisPrefab, transform);
                        disc.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
                        disc.transform.position = new Vector3(
                            zone.Position.X * worldUnitsPerSimUnit, 0.02f, zone.Position.Y * worldUnitsPerSimUnit);
                    }
                    else
                    {
                        disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        disc.transform.SetParent(transform, worldPositionStays: false);
                        disc.transform.position = new Vector3(
                            zone.Position.X * worldUnitsPerSimUnit, 0.02f, zone.Position.Y * worldUnitsPerSimUnit);
                        disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                    }

                    disc.name = $"slow-zone-{zone.Position.X}-{zone.Position.Y}";
                    TintFeature(disc, slowZoneColor);
                    _boardFeatures.Add(disc);
                }
            }
        }

        private static void TintFeature(GameObject feature, Color color)
        {
            var featureRenderer = feature.GetComponent<Renderer>();
            if (featureRenderer != null)
            {
                featureRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Shows (or hides, when null) the wind indicator: a flat arrow that
        /// re-anchors to the fleet centroid each tick and points DOWNWIND —
        /// where the wind pushes — using the same heading→yaw mapping as the
        /// ships. Wind was mechanically live and never rendered (W1 audit).
        /// </summary>
        public void SetWind(SimWind wind)
        {
            _wind = wind;
            if (wind == null)
            {
                if (_windArrow != null)
                {
                    Destroy(_windArrow.gameObject);
                    _windArrow = null;
                }

                return;
            }

            if (_windArrow == null)
            {
                var root = new GameObject("wind-indicator");
                root.transform.SetParent(transform, worldPositionStays: false);

                var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shaft.name = "shaft";
                shaft.transform.SetParent(root.transform, worldPositionStays: false);
                shaft.transform.localPosition = new Vector3(0f, 0f, 0f);
                shaft.transform.localScale = new Vector3(0.12f, 0.05f, 1.2f);
                TintFeature(shaft, windIndicatorColor);

                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "head";
                head.transform.SetParent(root.transform, worldPositionStays: false);
                head.transform.localPosition = new Vector3(0f, 0f, 0.7f);
                head.transform.localScale = new Vector3(0.35f, 0.05f, 0.35f);
                TintFeature(head, windIndicatorColor);

                _windArrow = root.transform;
            }

            _windArrow.rotation = HeadingToRotation(wind.Direction);
            // Speed reads as shaft length: ±2 effective-speed winds are the
            // mission convention, so scale gently around 1.
            _windArrow.localScale = new Vector3(1f, 1f, 0.6f + wind.Speed * 0.15f);
            AnchorWindIndicator();
        }

        private void AnchorWindIndicator()
        {
            if (_windArrow == null || _markers.Count == 0)
            {
                return;
            }

            var centroid = Vector3.zero;
            var count = 0;
            foreach (var marker in _markers.Values)
            {
                if (marker.Transform != null)
                {
                    centroid += marker.Transform.position;
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            _windArrow.position = centroid / count + windIndicatorOffset;
        }

        private Vector3 ToWorld(int simX, int simY)
        {
            return new Vector3(simX * worldUnitsPerSimUnit, markerHeight, simY * worldUnitsPerSimUnit);
        }

        private bool TryGetMarker(string shipId, out Marker marker)
        {
            marker = null;
            return shipId != null && _markers.TryGetValue(shipId, out marker);
        }

        private void Flash(string shipId, Color color)
        {
            if (shipId != null && _markers.TryGetValue(shipId, out var sunkCheck)
                && sunkCheck.View != null && sunkCheck.View.IsSunk)
            {
                return;
            }

            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = color;
            }
        }

        private void FadeFlash(string shipId, Color color, float progress)
        {
            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = Color.Lerp(color, RestingColorOf(marker), progress);
            }
        }

        private void RestoreColor(string shipId)
        {
            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = RestingColorOf(marker);
            }
        }

        // Flashes settle back to the view's status/sunk-aware resting color,
        // not the raw side color (W2 slice 3).
        private static Color RestingColorOf(Marker marker)
        {
            return marker.View != null ? marker.View.RestingColor : marker.BaseColor;
        }

        private void SetHud(string message)
        {
            _lastMessage = message;
            RefreshHudLabel();
            Debug.Log($"[Spectator] {message}");
        }

        // Re-composes the HUD line from the last narration plus control
        // status, so pause/speed changes surface without waiting for the
        // next playback step.
        private void RefreshHudLabel()
        {
            var status = string.Empty;
            if (IsPaused)
            {
                status += " | PAUSED (Right Arrow steps)";
            }
            if (!Mathf.Approximately(SpeedMultiplier, 1f))
            {
                status += $" | speed x{SpeedMultiplier:0.##}";
            }

            HudText = _lastMessage + status;
            if (hudLabel != null)
            {
                hudLabel.text = HudText;
            }
        }

        // W4 conditions zone (art-direction.md §7 item 3): the wind and turn
        // numbers the client already holds, as one compact numeric line. Fed
        // from the wind passed to BeginTurns/ShowBoard and the TurnStart
        // steps of the playback stream; empty when neither is known.
        private void RefreshConditionsLabel()
        {
            ConditionsText = ComposeConditions(_wind, _currentTurn, _turnLimit);
            if (conditionsLabel != null)
            {
                conditionsLabel.text = ConditionsText;
            }
        }

        /// <summary>Pure composition of the conditions readout; test hook.</summary>
        public static string ComposeConditions(SimWind wind, int turn, int turnLimit)
        {
            var windPart = wind != null ? $"Wind {wind.Direction}°/{wind.Speed}" : null;
            var turnPart = turn > 0
                ? (turnLimit > 0 ? $"Turn {turn}/{turnLimit}" : $"Turn {turn}")
                : null;
            if (windPart == null)
            {
                return turnPart ?? string.Empty;
            }

            return turnPart == null ? windPart : $"{windPart} | {turnPart}";
        }
    }
}
