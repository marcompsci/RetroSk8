using System.Collections.Generic;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.Audio;

namespace RetroSk8.Audio
{
    /// <summary>Original songs (RetroSk8.Core.MusicComposer): one for the menus and one per park.</summary>
    public enum MusicTrack
    {
        Menu = 0,
        Harbor = 1,
        Warehouse = 2,
        Rooftop = 3,
        City = 4,
        Bowls = 5,
    }

    public enum AudioBus
    {
        Music = 0,
        Effects = 1,
        Ambience = 2,
    }

    /// <summary>
    /// Persistent audio service with three buses. If mixer groups are assigned, volumes drive the exposed
    /// mixer parameters "MusicVolume", "EffectsVolume", "AmbienceVolume" (dB); otherwise source volumes are scaled directly.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SfxVoices = 10;

        public AudioMixer mixer;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup effectsGroup;
        public AudioMixerGroup ambienceGroup;

        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private AudioSource _music;
        private AudioSource _musicFade;   // the outgoing song during a crossfade
        private AudioSource _ambience;
        private readonly Dictionary<MusicTrack, AudioClip> _songs = new Dictionary<MusicTrack, AudioClip>();
        private float _crossfade = 1f;    // 0 → 1 while a new song fades in
        private float _duck = 1f;         // music multiplier, dips on bails and recovers
        private float _duckRecover = 1f;
        private const float CrossfadeSeconds = 1.2f;
        private int _nextVoice;
        private readonly float[] _volumes = { 0.6f, 1f, 0.7f };

        public static AudioManager Instance { get; private set; }

        public static AudioManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _music = CreateSource("Music", AudioBus.Music, true);
            _musicFade = CreateSource("MusicFade", AudioBus.Music, true);
            _ambience = CreateSource("Ambience", AudioBus.Ambience, true);
            for (int i = 0; i < SfxVoices; i++) _voices.Add(CreateSource("Sfx" + i, AudioBus.Effects, false));

            var s = SaveManager.Data.settings;
            SetVolume(AudioBus.Music, s.musicVolume);
            SetVolume(AudioBus.Effects, s.effectsVolume);
            SetVolume(AudioBus.Ambience, s.ambienceVolume);
        }

        public AudioClip Clip(SfxId id)
        {
            if (!_clips.TryGetValue(id, out var clip) || clip == null)
            {
                clip = ProceduralSfx.Create(id);
                _clips[id] = clip;
            }
            return clip;
        }

        public void PlaySfx(SfxId id, float volume = 1f, float pitch = 1f)
        {
            var v = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            v.pitch = pitch;
            v.volume = volume * (mixer != null ? 1f : _volumes[(int)AudioBus.Effects]);
            v.clip = Clip(id);
            v.Play();
        }

        public void PlayMusic() => PlayMusic(MusicTrack.Menu);

        /// <summary>Crossfades to <paramref name="track"/> (no-op if it is already playing). Songs render once per session.</summary>
        public void PlayMusic(MusicTrack track)
        {
            var clip = Song(track);
            if (_music.clip == clip && _music.isPlaying) return;

            // Swap roles: the current song becomes the fading-out source.
            var old = _music;
            _music = _musicFade;
            _musicFade = old;
            _music.clip = clip;
            _music.Play();
            _crossfade = _musicFade.isPlaying ? 0f : 1f;
            if (_crossfade >= 1f) _musicFade.Stop();
            ApplyMusicVolume();
        }

        public static MusicTrack TrackFor(AmbienceKind kind) =>
            kind == AmbienceKind.Warehouse ? MusicTrack.Warehouse
            : kind == AmbienceKind.Rooftop ? MusicTrack.Rooftop
            : kind == AmbienceKind.City ? MusicTrack.City
            : kind == AmbienceKind.Bowls ? MusicTrack.Bowls
            : MusicTrack.Harbor;

        private AudioClip Song(MusicTrack track)
        {
            if (_songs.TryGetValue(track, out var clip) && clip != null) return clip;
            var spec = track == MusicTrack.Harbor ? RetroSk8.Core.MusicComposer.Harbor
                     : track == MusicTrack.Warehouse ? RetroSk8.Core.MusicComposer.Warehouse
                     : track == MusicTrack.Rooftop ? RetroSk8.Core.MusicComposer.Rooftop
                     : track == MusicTrack.City ? RetroSk8.Core.MusicComposer.City
                     : track == MusicTrack.Bowls ? RetroSk8.Core.MusicComposer.Bowls
                     : RetroSk8.Core.MusicComposer.Menu;
            clip = ProceduralSfx.Music(spec);
            _songs[track] = clip;
            return clip;
        }

        /// <summary>Dips the music (e.g. on a bail) to <paramref name="level"/> and lets it recover over <paramref name="seconds"/>.</summary>
        public void Duck(float level, float seconds)
        {
            _duck = Mathf.Min(_duck, Mathf.Clamp01(level));
            _duckRecover = Mathf.Max(0.05f, seconds);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool changed = false;
            if (_crossfade < 1f)
            {
                _crossfade = Mathf.Min(1f, _crossfade + dt / CrossfadeSeconds);
                if (_crossfade >= 1f) _musicFade.Stop();
                changed = true;
            }
            if (_duck < 1f)
            {
                _duck = Mathf.Min(1f, _duck + dt / _duckRecover);
                changed = true;
            }
            if (changed) ApplyMusicVolume();
        }

        private void ApplyMusicVolume()
        {
            float bus = mixer != null ? 1f : _volumes[(int)AudioBus.Music];
            _music.volume = bus * _duck * _crossfade;
            _musicFade.volume = _musicFade.isPlaying ? bus * _duck * (1f - _crossfade) : 0f;
        }

        public void PlayAmbience(AmbienceKind kind)
        {
            SfxId id = kind == AmbienceKind.Warehouse ? SfxId.AmbienceWarehouse
                     : kind == AmbienceKind.Rooftop || kind == AmbienceKind.Bowls ? SfxId.AmbienceRooftop
                     : kind == AmbienceKind.City ? SfxId.AmbienceCity
                     : SfxId.AmbienceHarbor;
            _ambience.clip = Clip(id);
            _ambience.Play();
        }

        public void StopAmbience() => _ambience.Stop();

        /// <summary>A looping effects source owned by the caller (wheel roll, grind). Bus volume is applied automatically.</summary>
        public AudioSource CreateEffectLoop(SfxId id, Transform parent)
        {
            var go = new GameObject("Loop_" + id);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = Clip(id);
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = effectsGroup;
            src.volume = 0f;
            return src;
        }

        /// <summary>Scale callers apply to their own sources (1 when a mixer handles bus volume).</summary>
        public float BusVolume(AudioBus bus) => mixer != null ? 1f : _volumes[(int)bus];

        public float StoredVolume(AudioBus bus) => _volumes[(int)bus];

        public void SetVolume(AudioBus bus, float linear)
        {
            linear = Mathf.Clamp01(linear);
            _volumes[(int)bus] = linear;
            if (mixer != null)
            {
                string param = bus == AudioBus.Music ? "MusicVolume" : bus == AudioBus.Effects ? "EffectsVolume" : "AmbienceVolume";
                mixer.SetFloat(param, linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f);
                return;
            }
            if (bus == AudioBus.Music) ApplyMusicVolume();
            if (bus == AudioBus.Ambience) _ambience.volume = linear;
        }

        private AudioSource CreateSource(string name, AudioBus bus, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = bus == AudioBus.Music ? musicGroup : bus == AudioBus.Effects ? effectsGroup : ambienceGroup;
            return src;
        }
    }
}
