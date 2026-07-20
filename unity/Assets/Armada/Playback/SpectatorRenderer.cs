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
        [SerializeField] private float turnBannerSeconds = 0.4f;
        [SerializeField] private float maneuverSeconds = 0.2f;
        [SerializeField] private float moveSeconds = 0.35f;
        [SerializeField] private float flashSeconds = 0.45f;

        [Header("Colors (design-tunable placeholders)")]
        [SerializeField] private Color playerColor = new Color(0.20f, 0.75f, 0.35f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.20f);
        [SerializeField] private Color roundShotFlashColor = new Color(1.00f, 0.60f, 0.10f);
        [Tooltip("Chain shot is the Mission 10 showcase; its flash must read distinct from round shot.")]
        [SerializeField] private Color chainShotFlashColor = new Color(0.20f, 0.90f, 1.00f);
        [SerializeField] private Color ramFlashColor = Color.white;

        [Header("UI Wiring")]
        [SerializeField] private TMP_Text hudLabel;

        private sealed class Marker
        {
            public Transform Transform;
            public Renderer Renderer;
            public Color BaseColor;
            public Vector3 MoveFrom;
            public Vector3 MoveTo;
        }

        private readonly Dictionary<string, Marker> _markers = new();
        private TurnPlayback _playback;
        private Mission10Outcome _outcome;
        private PlaybackStep _currentStep;
        private float _stepElapsed;
        private float _stepDuration;

        /// <summary>Step currently being animated; test hook, may be null.</summary>
        public PlaybackStep CurrentStep => _currentStep;

        /// <summary>Last HUD line written; test hook.</summary>
        public string HudText { get; private set; }

        public bool IsFinished { get; private set; }

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
            ClearMarkers();
            _outcome = outcome;
            _currentStep = null;
            IsFinished = false;

            var ships = Mission10Scenario.BuildExpectedStart(outcome.Seed).State.Ships;
            foreach (var ship in ships)
            {
                SpawnMarker(ship);
            }

            _playback = new TurnPlayback(ships, outcome.Turns);
            SetHud($"Spectating {outcome.MissionCode} (seed {outcome.Seed})...");
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
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances playback by dt seconds. A tick either begins the next
        /// step or advances the active one, never both, so tests stepping
        /// with fixed dt values see every step deterministically.
        /// </summary>
        public void Tick(float dt)
        {
            if (_playback == null || IsFinished)
            {
                return;
            }

            if (_currentStep == null)
            {
                if (!_playback.TryStep(out var step))
                {
                    FinishRun();
                    return;
                }

                BeginStep(step);
                return;
            }

            _stepElapsed += dt;
            var progress = PlaybackEase.Progress(_stepElapsed, _stepDuration);
            AnimateStep(progress);
            if (_stepElapsed >= _stepDuration)
            {
                EndStep();
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
                    SetHud($"Turn {step.Turn}/{_outcome.TurnLimit}");
                    break;
                case PlaybackStepKind.Maneuver:
                    _stepDuration = maneuverSeconds;
                    if (step.Heading.HasValue && TryGetMarker(step.ShipId, out var maneuvering))
                    {
                        maneuvering.Transform.rotation = Quaternion.Euler(0f, step.Heading.Value, 0f);
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
                    Flash(step.ShipId, step.ChainShot ? chainShotFlashColor : roundShotFlashColor);
                    SetHud(step.Hit
                        ? $"T{step.Turn} {step.ShipId} => {step.TargetShipId}: {(step.ChainShot ? "CHAIN SHOT" : "round shot")} hit (hull -{step.AppliedHull}, sail -{step.AppliedSail}, crew -{step.AppliedCrew})"
                        : $"T{step.Turn} {step.ShipId} => {step.TargetShipId}: {(step.ChainShot ? "CHAIN SHOT" : "round shot")} miss");
                    break;
                case PlaybackStepKind.Ram:
                    _stepDuration = flashSeconds;
                    Flash(step.ShipId, ramFlashColor);
                    Flash(step.TargetShipId, ramFlashColor);
                    SetHud($"T{step.Turn} {step.ShipId} rams {step.TargetShipId} (hull -{step.AppliedHull}, recoil -{step.SelfAppliedHull})");
                    break;
                case PlaybackStepKind.Boarding:
                    _stepDuration = flashSeconds;
                    Flash(step.ShipId, ramFlashColor);
                    SetHud($"T{step.Turn} {step.ShipId} boards {step.TargetShipId}: {(step.Hit ? "success" : "repelled")} (crew -{step.AppliedCrew})");
                    break;
                case PlaybackStepKind.Status:
                    _stepDuration = maneuverSeconds;
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
                    FadeFlash(step.ShipId, step.ChainShot ? chainShotFlashColor : roundShotFlashColor, progress);
                    break;
                case PlaybackStepKind.Ram:
                    FadeFlash(step.ShipId, ramFlashColor, progress);
                    FadeFlash(step.TargetShipId, ramFlashColor, progress);
                    break;
                case PlaybackStepKind.Boarding:
                    FadeFlash(step.ShipId, ramFlashColor, progress);
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
            var bonuses = _outcome?.BonusObjectives;
            var inflicted = _playback.PlayerInflicted;
            SetHud(
                $"Result: {_outcome?.Result} at turn {_outcome?.TurnCount}/{_outcome?.TurnLimit}"
                + $" | bonuses: sailShredder={(bonuses?.SailShredder == true ? "yes" : "no")}, mixedBattery={(bonuses?.MixedBattery == true ? "yes" : "no")}"
                + $" | applied to enemy: hull {inflicted.Hull}, sail {inflicted.Sail}, crew {inflicted.Crew}");
        }

        private void SpawnMarker(SimShip ship)
        {
            // Placeholder art: player ships are cubes, enemy ships capsules,
            // flat-tinted with the side color.
            var primitive = GameObject.CreatePrimitive(ship.Side == "player" ? PrimitiveType.Cube : PrimitiveType.Capsule);
            primitive.name = $"marker-{ship.Id}";
            primitive.transform.SetParent(transform, worldPositionStays: false);
            primitive.transform.position = ToWorld(ship.Position.X, ship.Position.Y);
            primitive.transform.rotation = Quaternion.Euler(0f, ship.Heading, 0f);

            var markerRenderer = primitive.GetComponent<Renderer>();
            var baseColor = ship.Side == "player" ? playerColor : enemyColor;
            if (markerRenderer != null)
            {
                markerRenderer.material.color = baseColor;
            }

            _markers[ship.Id] = new Marker
            {
                Transform = primitive.transform,
                Renderer = markerRenderer,
                BaseColor = baseColor
            };
        }

        private void ClearMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                if (marker.Transform != null)
                {
                    Destroy(marker.Transform.gameObject);
                }
            }

            _markers.Clear();
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
            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = color;
            }
        }

        private void FadeFlash(string shipId, Color color, float progress)
        {
            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = Color.Lerp(color, marker.BaseColor, progress);
            }
        }

        private void RestoreColor(string shipId)
        {
            if (TryGetMarker(shipId, out var marker) && marker.Renderer != null)
            {
                marker.Renderer.material.color = marker.BaseColor;
            }
        }

        private void SetHud(string message)
        {
            HudText = message;
            if (hudLabel != null)
            {
                hudLabel.text = message;
            }
            Debug.Log($"[Spectator] {message}");
        }
    }
}
