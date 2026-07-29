using Armada.Client.Playback;
using TMPro;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>
    /// On-screen playback controls for the spectator surface (D2-B touch
    /// controls, docs/design/art-direction.md §3.3): pause/resume toggle,
    /// single-step, and speed preset cycling, wired by the scene builders to
    /// touch-size buttons. Calls the same SpectatorRenderer public API the
    /// keyboard bindings use, so the two input paths cannot drift; the
    /// keyboard remains the Editor dev harness. The pause button's label
    /// tracks the renderer state so it always names the action it performs.
    /// </summary>
    public sealed class PlaybackControlsController : MonoBehaviour
    {
        private const string PauseText = "Pause";
        private const string ResumeText = "Resume";

        [Header("Wiring")]
        [SerializeField] private SpectatorRenderer spectator;
        [Tooltip("Label of the pause/resume toggle button; kept in sync with the renderer's paused state.")]
        [SerializeField] private TMP_Text pauseLabel;

        /// <summary>Current pause-toggle caption; test hook.</summary>
        public string PauseCaption => spectator != null && spectator.IsPaused ? ResumeText : PauseText;

        public void OnTogglePause()
        {
            if (spectator == null)
            {
                return;
            }

            if (spectator.IsPaused)
            {
                spectator.Resume();
            }
            else
            {
                spectator.Pause();
            }

            RefreshPauseLabel();
        }

        public void OnStep()
        {
            if (spectator != null)
            {
                spectator.StepOnce();
            }
        }

        public void OnSpeedDown()
        {
            if (spectator != null)
            {
                spectator.CycleSpeedPreset(-1);
            }
        }

        public void OnSpeedUp()
        {
            if (spectator != null)
            {
                spectator.CycleSpeedPreset(1);
            }
        }

        private void Update()
        {
            // The keyboard path can also change the paused state; a per-frame
            // string-reference compare keeps the caption honest either way.
            RefreshPauseLabel();
        }

        private void RefreshPauseLabel()
        {
            if (pauseLabel == null)
            {
                return;
            }

            var caption = PauseCaption;
            if (!ReferenceEquals(pauseLabel.text, caption) && pauseLabel.text != caption)
            {
                pauseLabel.text = caption;
            }
        }
    }
}
