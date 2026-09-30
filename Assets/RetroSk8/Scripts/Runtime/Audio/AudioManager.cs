using System.Collections.Generic;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.Audio;

namespace RetroSk8.Audio
{
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
        private AudioSource _ambience;
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

        public void PlayMusic()
        {
            _music.clip = Clip(SfxId.MusicLoop);
            if (!_music.isPlaying) _music.Play();
        }

        public void PlayAmbience(AmbienceKind kind)
        {
            SfxId id = kind == AmbienceKind.Warehouse ? SfxId.AmbienceWarehouse
                     : kind == AmbienceKind.Rooftop ? SfxId.AmbienceRooftop
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
            if (bus == AudioBus.Music) _music.volume = linear;
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
