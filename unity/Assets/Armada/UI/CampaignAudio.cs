using Armada.Client.Playback;
using UnityEngine;

namespace Armada.Client.UI
{
    /// <summary>Presentation-only sounds from the licensed asset ledger; six bounded voices.</summary>
    public sealed class CampaignAudio : MonoBehaviour
    {
        [SerializeField] private SpectatorRenderer spectator;
        [SerializeField] private CampaignUIController view;
        [SerializeField] private AudioClip sea, music, click, cannon, impact;
        private AudioSource _sea, _music;
        private AudioSource[] _effects;
        private PlaybackStep _last;
        private int _voice;
        public bool Muted { get; private set; }
        private const string Preference = "armada.audio.muted";

        private void Awake()
        {
            Muted = PlayerPrefs.GetInt(Preference, 0) == 1;
            _sea = Source(true); _sea.clip = sea; _sea.volume = 0.10f;
            _music = Source(true); _music.clip = music; _music.volume = 0.12f;
            _effects = new AudioSource[4];
            for (var i = 0; i < _effects.Length; i++) _effects[i] = Source(false);
            ApplyMute();
            if (sea != null) _sea.Play();
        }
        private AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            return source;
        }
        public void ToggleMuted()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(Preference, Muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMute();
        }
        private void ApplyMute()
        {
            _sea.mute = Muted; _music.mute = Muted;
            foreach (var source in _effects) source.mute = Muted;
        }
        public void PlayClick() => Play(click, 0.25f);
        private void Play(AudioClip clip, float volume)
        {
            if (Muted || clip == null || _effects == null) return;
            var source = _effects[_voice++ % _effects.Length];
            source.Stop(); source.clip = clip; source.volume = volume; source.Play();
        }
        private void LateUpdate()
        {
            var inBattle = view != null && view.ScreenName == "Battle";
            if (inBattle && !_music.isPlaying && music != null) _music.Play();
            else if (!inBattle && _music.isPlaying) _music.Stop();
            var step = spectator?.CurrentStep;
            if (ReferenceEquals(step, _last)) return;
            _last = step;
            if (!inBattle || step == null) return;
            if (step.Kind == PlaybackStepKind.Broadside) Play(cannon, 0.36f);
            if (step.Kind == PlaybackStepKind.Ram || step.Kind == PlaybackStepKind.Boarding || step.TargetSunk) Play(impact, 0.32f);
        }
    }
}
