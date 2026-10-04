using System.Collections.Generic;
using RetroSk8.Core;
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

        /// <summary>
        /// Asks for the music that belongs here: <paramref name="track"/> when the player hears park themes, the radio
        /// when it's on (it keeps playing across scenes), or silence. Songs render once per session.
        /// </summary>
        public void PlayMusic(MusicTrack track)
        {
            _requested = track;
            switch (Mode)
            {
                case MusicMode.Off:
                    StopMusic();
                    return;
                case MusicMode.Radio:
                    StartRadio(false);
                    return;
                default:
                    _radioClip = null;
                    CrossfadeTo(Song(track));
                    return;
            }
        }

        /// <summary>Crossfades to <paramref name="clip"/> (no-op if it is already playing).</summary>
        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null) return;
            if (_music.clip == clip && _music.isPlaying) return;

            // Swap roles: the current song becomes the fading-out source.
            var old = _music;
            _music = _musicFade;
            _musicFade = old;
            _music.clip = clip;
            _music.Play();
            _lastSamples = 0;
            _loops = 0;
            _crossfade = _musicFade.isPlaying ? 0f : 1f;
            if (_crossfade >= 1f) _musicFade.Stop();
            ApplyMusicVolume();
        }

        private void StopMusic()
        {
            _radioClip = null;
            if (!_music.isPlaying) return;
            var old = _music;
            _music = _musicFade;
            _musicFade = old;
            _music.Stop();
            _music.clip = null;
            _crossfade = 0f; // fades the old song out
            ApplyMusicVolume();
        }

        // ---------------------------------------------------------------- music mode and the radio

        private MusicTrack _requested = MusicTrack.Menu;
        private RadioPlayer _radio;
        private AudioClip _radioClip;
        private int _lastSamples;
        private int _loops;
        private readonly Dictionary<string, AudioClip> _radioSongs = new Dictionary<string, AudioClip>();
        private System.Threading.Tasks.Task<float[]> _rendering;
        private SongSpec _renderingSpec;

        public static MusicMode Mode => (MusicMode)SaveManager.Data.settings.musicMode;

        /// <summary>The radio station and song (null unless the radio is on).</summary>
        public RadioPlayer Radio => Mode == MusicMode.Radio ? _radio : null;

        /// <summary>"STATION · SONG" while the radio is on, else empty.</summary>
        public string NowPlayingText => Mode == MusicMode.Radio && _radio != null ? _radio.NowPlaying : "";

        /// <summary>Raised when a radio song starts (the HUD shows a now-playing card).</summary>
        public event System.Action<string> NowPlaying;

        /// <summary>Switches park themes / radio / off and saves the choice.</summary>
        public void SetMusicMode(MusicMode mode, int station)
        {
            var s = SaveManager.Data.settings;
            bool retune = mode == MusicMode.Radio && (s.musicMode != (int)MusicMode.Radio || s.radioStation != station);
            s.musicMode = (int)mode;
            s.radioStation = Mathf.Clamp(station, 0, RetroSk8.Core.Radio.Stations.Length - 1);
            if (mode == MusicMode.Radio) StartRadio(retune);
            else PlayMusic(_requested);
        }

        /// <summary>The music button: park themes → each station → off.</summary>
        public void CycleMusicMode()
        {
            var s = SaveManager.Data.settings;
            var mode = (MusicMode)s.musicMode;
            int station = s.radioStation;
            RetroSk8.Core.Radio.Cycle(ref mode, ref station);
            SetMusicMode(mode, station);
        }

        /// <summary>Next song on the radio (no-op otherwise).</summary>
        public void SkipSong()
        {
            if (Mode != MusicMode.Radio || _radio == null) return;
            _radio.Next();
            QueueRadioSong();
        }

        private void StartRadio(bool retune)
        {
            int station = SaveManager.Data.settings.radioStation;
            if (_radio == null) _radio = new RadioPlayer(station, System.Environment.TickCount);
            else if (retune || _radio.StationIndex != station) _radio.Tune(station);
            else if (_radioClip != null && _music.clip == _radioClip && _music.isPlaying) return; // already on air
            QueueRadioSong();
        }

        /// <summary>Plays the radio's current song: straight away if rendered, otherwise renders it off the main thread.</summary>
        private void QueueRadioSong()
        {
            var spec = _radio.Song;
            if (_radioSongs.TryGetValue(spec.Name, out var ready) && ready != null)
            {
                BeginRadioSong(ready);
                return;
            }
            if (_rendering != null && _renderingSpec == spec) return;
            _renderingSpec = spec;
            _rendering = System.Threading.Tasks.Task.Run(() => MusicComposer.Render(spec, ProceduralSfx.SampleRate));
        }

        private void BeginRadioSong(AudioClip clip)
        {
            _radioClip = clip;
            CrossfadeTo(clip);
            NowPlaying?.Invoke(_radio.NowPlaying);
        }

        private void UpdateRadio()
        {
            if (_rendering != null && _rendering.IsCompleted)
            {
                var spec = _renderingSpec;
                var task = _rendering;
                _rendering = null;
                _renderingSpec = null;
                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result != null)
                {
                    var clip = AudioClip.Create(spec.Name, task.Result.Length, 1, ProceduralSfx.SampleRate, false);
                    clip.SetData(task.Result, 0);
                    _radioSongs[spec.Name] = clip;
                }
                // Still wanted? (The player may have skipped or switched off while it rendered.)
                if (Mode == MusicMode.Radio && _radio != null)
                {
                    if (_radio.Song == spec && _radioSongs.TryGetValue(spec.Name, out var done)) BeginRadioSong(done);
                    else QueueRadioSong();
                }
            }

            // Count loops of the current song; after enough, the station moves on.
            if (Mode != MusicMode.Radio || _radio == null || _radioClip == null || _music.clip != _radioClip || !_music.isPlaying) return;
            int samples = _music.timeSamples;
            if (samples < _lastSamples) _loops++;
            _lastSamples = samples;
            if (_loops >= RetroSk8.Core.Radio.RepeatsFor(_radio.Song))
            {
                _loops = 0;
                _radio.Next();
                QueueRadioSong();
            }
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
            UpdateRadio();
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
