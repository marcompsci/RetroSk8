using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.Rendering;
using Theme = RetroSk8.UI.Theme;

namespace RetroSk8.Game
{
    /// <summary>
    /// Makes Retro City feel alive (rules in <see cref="CityLife"/>): an 8-minute day/night cycle with neon that
    /// glows brighter after dark, passing rain showers, cars circling the ring road and downtown (they honk, and a
    /// hit knocks you off your board), pedestrians who step aside, and pop-up street events in Explore:
    /// block parties (2x points), golden tapes and photo shoots.
    /// </summary>
    public sealed class CityLifeController : MonoBehaviour
    {
        private const int Pedestrians = 10;

        private PlayerController _player;
        private ComboManager _combo;
        private BailHandler _bail;
        private CityController _city;
        private bool _events;
        private float _clock;

        // Lighting captured at start (the park's daytime look).
        private Light _sun;
        private Quaternion _sunRot;
        private Color _sunColor, _sky, _equator, _ground, _fog, _bg;
        private float _sunIntensity, _fogDensity;
        private Camera _cam;
        private readonly Dictionary<Material, Color> _neon = new Dictionary<Material, Color>();

        private static readonly Color NightSky = new Color(0.05f, 0.06f, 0.14f);
        private static readonly Color NightAmbient = new Color(0.16f, 0.17f, 0.28f);
        private static readonly Color NightFog = new Color(0.07f, 0.08f, 0.16f);
        private static readonly Color Moon = new Color(0.55f, 0.62f, 0.95f);
        private static readonly Color GoldenLight = new Color(1f, 0.62f, 0.36f);
        private static readonly Color RainFog = new Color(0.45f, 0.48f, 0.55f);

        // Weather.
        private ParticleSystem _rain;
        private AudioSource _rainAudio;
        private float _rainAmount;

        // Traffic.
        private sealed class Car
        {
            public Transform T;
            public int Loop;
            public float Offset;
            public Vector3 Velocity;
            public float HornCooldown;
        }
        private readonly List<Car> _cars = new List<Car>();
        private float _bumpCooldown;

        // Pedestrians.
        private sealed class Walker
        {
            public Transform T;
            public Vector3 A, B;
            public float Phase, Speed, Dodge, DodgeSide;
        }
        private readonly List<Walker> _walkers = new List<Walker>();

        // Street events.
        private StreetEventPlanner _planner;
        private bool _eventOn;
        private StreetEventKind _eventKind;
        private float _eventEnds;
        private CitySpot _eventSpot;
        private Vector3 _eventPos;
        private GameObject _eventMarker;

        /// <summary>Weekly events can lock the city at night or in the rain.</summary>
        public WeeklyModifier Modifier { get; set; } = WeeklyModifier.None;

        public float TimeOfDay => Modifier == WeeklyModifier.CityNight ? 0.02f : CityLife.TimeOfDay(_clock);
        public float NightAmount => CityLife.Night(TimeOfDay);
        public float Rain => _rainAmount;
        public int CarCount => _cars.Count;
        public bool EventRunning => _eventOn;
        public StreetEventKind EventKind => _eventKind;

        public void Init(PlayerController player, ComboManager combo, CityController city, bool events, int seed)
        {
            _player = player;
            _combo = combo;
            _bail = player.GetComponent<BailHandler>();
            _city = city;
            _events = events;
            _planner = new StreetEventPlanner(seed);
            _cam = Camera.main;
            Modifier = WeeklyService.Modifier;
            CaptureLook();
            CollectNeon();
            BuildRain();
            BuildTraffic();
            BuildPedestrians(seed);
            combo.Banked += OnBanked;
        }

        private void OnDestroy()
        {
            // The neon materials are shared and cached across scenes: put them back.
            foreach (var kv in _neon) if (kv.Key != null) kv.Key.SetColor("_EmissionColor", kv.Value);
            if (_combo != null)
            {
                _combo.Banked -= OnBanked;
                _combo.BonusFactor = 1f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused
            _clock += dt;
            UpdateSky();
            UpdateWeather();
            UpdateTraffic(dt);
            UpdateWalkers(dt);
            if (_events) UpdateEvents();
            if (_city != null)
            {
                // Phase 18 performance pass: rebuild the status text only when the minute or the weather changes.
                int weather = _rainAmount > 0.05f ? 2 : CityLife.WeatherAt(_clock, out _) == Weather.Cloudy ? 1 : 0;
                int key = (int)(TimeOfDay * 24f * 60f) * 4 + weather;
                if (key != _statusKey || _city.LifeStatus == null)
                {
                    _statusKey = key;
                    _city.LifeStatus = CityLife.Clock(TimeOfDay) + (weather == 2 ? " · RAIN" : weather == 1 ? " · CLOUDY" : "");
                }
            }
        }

        private int _statusKey = -1;
        private long _bannerKey = long.MinValue;

        /// <summary>True when the event banner's inputs changed since last frame (so the string is rebuilt only then).</summary>
        private bool BannerChanged(int kind, int a, int b)
        {
            long key = ((long)kind * 1000003L + a) * 1000003L + b;
            if (key == _bannerKey) return false;
            _bannerKey = key;
            return true;
        }

        /// <summary>Jumps the clock (tests and the debug menu).</summary>
        public void SkipAhead(float seconds) => _clock += Mathf.Max(0f, seconds);

        // ---------------------------------------------------------------- sky

        private void CaptureLook()
        {
            _sun = RenderSettings.sun;
            if (_sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { _sun = l; break; }
            if (_sun != null)
            {
                _sunRot = _sun.transform.rotation;
                _sunColor = _sun.color;
                _sunIntensity = _sun.intensity;
            }
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
            _bg = _cam != null ? _cam.backgroundColor : _fog;
        }

        private void CollectNeon()
        {
            var root = GameObject.Find(RetroCityBuilder.GeneratedRootName);
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || _neon.ContainsKey(m) || !m.name.StartsWith("Neon_") || !m.HasProperty("_EmissionColor")) continue;
                _neon[m] = m.GetColor("_EmissionColor");
            }
        }

        private void UpdateSky()
        {
            float t = TimeOfDay;
            float night = CityLife.Night(t);
            float golden = CityLife.Golden(t) * (1f - night);
            float cloud = Mathf.Max(_rainAmount, CityLife.WeatherAt(_clock, out _) == Weather.Cloudy ? 0.5f : 0f);

            if (_sun != null)
            {
                float elevation = Mathf.Lerp(Mathf.Max(CityLife.SunElevation(t), 6f), 38f, night); // the moon sits high at night
                var e = _sunRot.eulerAngles;
                _sun.transform.rotation = Quaternion.Euler(elevation, e.y + (t - 0.5f) * 90f, 0f);
                Color c = Color.Lerp(Color.Lerp(_sunColor, GoldenLight, golden), Moon, night);
                _sun.color = c;
                _sun.intensity = Mathf.Lerp(_sunIntensity, 0.35f, night) * (1f - 0.45f * cloud);
            }
            Color dim = Color.Lerp(Color.white, new Color(0.75f, 0.78f, 0.85f), cloud);
            RenderSettings.ambientSkyColor = Color.Lerp(_sky, NightAmbient * 1.2f, night) * dim;
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(_equator, GoldenLight * 0.6f, golden * 0.5f), NightAmbient, night) * dim;
            RenderSettings.ambientGroundColor = Color.Lerp(_ground, NightAmbient * 0.6f, night) * dim;
            RenderSettings.ambientLight = RenderSettings.ambientEquatorColor;
            Color fog = Color.Lerp(Color.Lerp(_fog, GoldenLight * 0.8f, golden * 0.4f), NightFog, night);
            RenderSettings.fogColor = Color.Lerp(fog, RainFog * (1f - 0.7f * night), _rainAmount * 0.7f);
            RenderSettings.fogDensity = _fogDensity * (1f + 1.6f * _rainAmount);
            if (_cam != null) _cam.backgroundColor = Color.Lerp(Color.Lerp(Color.Lerp(_bg, GoldenLight, golden * 0.35f), NightSky, night), RenderSettings.fogColor, cloud * 0.6f);

            float glow = 0.55f + 1.1f * night;
            foreach (var kv in _neon) if (kv.Key != null) kv.Key.SetColor("_EmissionColor", kv.Value * glow);
        }

        // ---------------------------------------------------------------- weather

        private void BuildRain()
        {
            if (SaveManager.Data.settings.lowEffects) return;
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.gravityModifier = 2.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1200;
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 1f, 36f);
            var emission = _rain.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(0.75f, 0.82f, 0.95f), 0.6f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _rain.Play();
            _rainAudio = AudioManager.Ensure().CreateEffectLoop(SfxId.RainLoop, transform);
        }

        private void UpdateWeather()
        {
            CityLife.WeatherAt(_clock, out float target);
            if (Modifier == WeeklyModifier.CityRain) target = 1f;
            _rainAmount = Mathf.MoveTowards(_rainAmount, target, Time.deltaTime * 0.2f);
            if (_rain != null)
            {
                var anchor = _cam != null ? _cam.transform.position : _player.transform.position;
                _rain.transform.position = anchor + Vector3.up * 14f;
                var emission = _rain.emission;
                emission.rateOverTime = 900f * _rainAmount;
            }
            if (_rainAudio != null)
            {
                _rainAudio.volume = 0.55f * _rainAmount * AudioManager.Ensure().BusVolume(AudioBus.Ambience);
                if (_rainAmount > 0.01f && !_rainAudio.isPlaying) _rainAudio.Play();
                else if (_rainAmount <= 0.01f && _rainAudio.isPlaying) _rainAudio.Stop();
            }
        }

        // ---------------------------------------------------------------- traffic

        private static readonly Color[] CarColors =
        {
            Palette.Coral, Palette.Teal, Palette.TapeYellow, Palette.Cream, Palette.ContainerTeal, Palette.ContainerMustard, Palette.NeonViolet,
        };

        private void BuildTraffic()
        {
            int colour = 0;
            for (int loop = 0; loop < CityLife.Loops.Length; loop++)
            {
                float len = CityLife.LoopLength(CityLife.Loops[loop]);
                int n = CityLife.CarsPerLoop[loop];
                for (int i = 0; i < n; i++)
                {
                    var car = new Car { Loop = loop, Offset = len * i / n, T = MakeCar(CarColors[colour++ % CarColors.Length]) };
                    _cars.Add(car);
                }
            }
            UpdateTraffic(0f);
        }

        private Transform MakeCar(Color color)
        {
            var root = new GameObject("Car").transform;
            root.SetParent(transform, false);
            PrimitiveMeshes.CreateVisual("Body", PrimitiveType.Cube, root, new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.8f, 4.2f), color);
            PrimitiveMeshes.CreateVisual("Cabin", PrimitiveType.Cube, root, new Vector3(0f, 1.3f, -0.3f), new Vector3(1.8f, 0.6f, 2.2f), Color.Lerp(color, Palette.Ink, 0.5f));
            foreach (var (x, z) in new[] { (-1f, 1.3f), (1f, 1.3f), (-1f, -1.3f), (1f, -1.3f) })
            {
                var w = PrimitiveMeshes.CreateVisual("Wheel", PrimitiveType.Cylinder, root, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.12f, 0.7f), Palette.Rubber);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            foreach (float x in new[] { -0.65f, 0.65f })
            {
                PrimitiveMeshes.CreateVisual("Headlight", PrimitiveType.Cube, root, new Vector3(x, 0.75f, 2.11f), new Vector3(0.4f, 0.18f, 0.04f), Palette.Cream)
                    .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(1f, 0.95f, 0.8f), 2.5f);
                PrimitiveMeshes.CreateVisual("Taillight", PrimitiveType.Cube, root, new Vector3(x, 0.75f, -2.11f), new Vector3(0.4f, 0.18f, 0.04f), Palette.Coral)
                    .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(1f, 0.15f, 0.12f), 2f);
            }
            return root;
        }

        private void UpdateTraffic(float dt)
        {
            float time = _clock;
            _bumpCooldown -= dt;
            var p = _player.transform.position;
            var pv = _player.Body != null ? _player.Body.linearVelocity : Vector3.zero;
            foreach (var car in _cars)
            {
                var loop = CityLife.Loops[car.Loop];
                float d = car.Offset + time * CityLife.CarSpeed;
                CityLife.PointOnLoop(loop, d, out float x, out float z, out _, out _);
                // Look a little ahead so corners are rounded instead of snapping 90 degrees.
                CityLife.PointOnLoop(loop, d + 5f, out float ax, out float az, out _, out _);
                var pos = new Vector3(x, 0f, z);
                var fwd = new Vector3(ax - x, 0f, az - z);
                if (fwd.sqrMagnitude < 1e-4f) fwd = car.T.forward;
                var prev = car.T.position;
                car.T.SetPositionAndRotation(pos, Quaternion.Slerp(car.T.rotation, Quaternion.LookRotation(fwd.normalized), dt > 0f ? 1f - Mathf.Exp(-dt * 8f) : 1f));
                car.Velocity = dt > 0f ? (pos - prev) / dt : car.T.forward * CityLife.CarSpeed;
                car.HornCooldown -= dt;

                var local = car.T.InverseTransformPoint(p);
                if (local.y < 1.8f && Mathf.Abs(local.x) < 1.35f && Mathf.Abs(local.z) < 2.5f)
                {
                    if (_bumpCooldown <= 0f && (car.Velocity - pv).magnitude > CityLife.BumpSpeed && _bail != null && !_bail.IsBailing)
                    {
                        _bumpCooldown = 2f;
                        AudioManager.Ensure().PlaySfx(SfxId.CarHorn);
                        HapticsManager.Play(HapticKind.Heavy);
                        _bail.Trigger(BailReason.Collision);
                    }
                }
                else if (car.HornCooldown <= 0f && local.z > 2.5f && local.z < 12f && Mathf.Abs(local.x) < 1.6f && local.y < 2f)
                {
                    car.HornCooldown = 4f; // you're in the lane ahead: honk
                    AudioManager.Ensure().PlaySfx(SfxId.CarHorn, 0.6f);
                }
            }
        }

        // ---------------------------------------------------------------- pedestrians

        private void BuildPedestrians(int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            const float side = RetroCityLayout.RoadHalf + 1.6f;
            for (int i = 0; i < Pedestrians; i++)
            {
                float line = RetroCityLayout.StreetLines[1 + rng.Next(2)] + (rng.Next(2) == 0 ? -side : side); // inner streets
                float c = RetroCityLayout.BlockCenters[rng.Next(RetroCityLayout.BlockCenters.Length)];
                bool alongZ = rng.Next(2) == 0;
                var a = alongZ ? new Vector3(line, 0f, c - 22f) : new Vector3(c - 22f, 0f, line);
                var b = alongZ ? new Vector3(line, 0f, c + 22f) : new Vector3(c + 22f, 0f, line);
                var root = new GameObject("Pedestrian").transform;
                root.SetParent(transform, false);
                var shirt = CarColors[rng.Next(CarColors.Length)];
                var skin = LookPalette.SkinTones[rng.Next(LookPalette.SkinTones.Length)];
                PrimitiveMeshes.CreateVisual("Legs", PrimitiveType.Capsule, root, new Vector3(0f, 0.45f, 0f), new Vector3(0.32f, 0.45f, 0.24f), Palette.Ink);
                PrimitiveMeshes.CreateVisual("Body", PrimitiveType.Capsule, root, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.36f, 0.28f), shirt);
                PrimitiveMeshes.CreateVisual("Head", PrimitiveType.Sphere, root, new Vector3(0f, 1.65f, 0f), new Vector3(0.26f, 0.28f, 0.26f), new Color(skin.R, skin.G, skin.B));
                _walkers.Add(new Walker { T = root, A = a, B = b, Phase = (float)rng.NextDouble(), Speed = 1.1f + (float)rng.NextDouble() * 0.5f });
            }
        }

        private void UpdateWalkers(float dt)
        {
            var p = _player.transform.position;
            float speed = _player.Speed;
            foreach (var w in _walkers)
            {
                float len = Vector3.Distance(w.A, w.B);
                w.Phase += dt * w.Speed / Mathf.Max(1f, len) * 0.5f;
                float k = Mathf.PingPong(w.Phase * 2f, 1f);
                var basePos = Vector3.Lerp(w.A, w.B, k);
                var dir = (w.B - w.A).normalized * (Mathf.Repeat(w.Phase * 2f, 2f) < 1f ? 1f : -1f);
                var across = Vector3.Cross(Vector3.up, dir);

                // A fast skater closing in makes them hop aside.
                var to = p - basePos;
                to.y = 0f;
                if (speed > 2f && to.magnitude < 3.5f && Mathf.Abs(p.y - basePos.y) < 2f)
                {
                    if (w.Dodge <= 0.01f) w.DodgeSide = Vector3.Dot(to, across) > 0f ? -1f : 1f;
                    w.Dodge = Mathf.MoveTowards(w.Dodge, 1f, dt * 6f);
                }
                else w.Dodge = Mathf.MoveTowards(w.Dodge, 0f, dt * 1.5f);

                w.T.position = basePos + across * (w.DodgeSide * 1.6f * w.Dodge) + Vector3.up * (Mathf.Sin(w.Dodge * Mathf.PI) * 0.25f);
                w.T.rotation = Quaternion.LookRotation(dir);
            }
        }

        // ---------------------------------------------------------------- street events

        private void UpdateEvents()
        {
            if (_city.Busy)
            {
                // Challenges and races take the HUD; events wait (and time out) in the background.
                _combo.BonusFactor = 1f;
                if (_eventOn && Time.time > _eventEnds) EndEvent(null);
                return;
            }

            if (!_eventOn)
            {
                _city.EventBanner = null;
                _city.EventTarget = null;
                if (_planner.TryStart(_clock, out var kind)) StartEvent(kind);
                return;
            }

            float left = _eventEnds - Time.time;
            if (left <= 0f) { EndEvent("TIME'S UP"); return; }
            var p = _player.transform.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_eventPos.x, _eventPos.z));
            switch (_eventKind)
            {
                case StreetEventKind.BlockParty:
                {
                    bool inside = dist <= _eventSpot.Radius * 1.1f;
                    _combo.BonusFactor = inside ? CityLife.BlockPartyFactor : 1f;
                    if (BannerChanged(1, inside ? 1 : 0, Mathf.CeilToInt(left)))
                        _city.EventBanner = $"BLOCK PARTY · {_eventSpot.Name.ToUpperInvariant()} · {(inside ? "2X POINTS NOW!" : "2X POINTS THERE")} · {Mathf.CeilToInt(left)}s";
                    _city.EventTarget = inside ? (Vector3?)null : _eventPos;
                    break;
                }
                case StreetEventKind.GoldenTape:
                    if (BannerChanged(2, Mathf.RoundToInt(dist), Mathf.CeilToInt(left)))
                        _city.EventBanner = $"GOLDEN TAPE · {dist:0} m · {Mathf.CeilToInt(left)}s";
                    _city.EventTarget = _eventPos;
                    if (_eventMarker != null && !SaveManager.Data.settings.reducedMotion) _eventMarker.transform.rotation = Quaternion.Euler(0f, Time.time * 160f, 0f);
                    if ((p + Vector3.up * 0.6f - (_eventPos + Vector3.up * 0.9f)).sqrMagnitude < 2.2f * 2.2f)
                    {
                        SaveManager.AddTokens(CityLife.GoldenTapeTokens);
                        AudioManager.Ensure().PlaySfx(SfxId.GoalComplete);
                        HapticsManager.Play(HapticKind.Success);
                        EndEvent($"GOLDEN TAPE!  +{CityLife.GoldenTapeTokens}");
                    }
                    break;
                case StreetEventKind.PhotoShoot:
                    if (BannerChanged(3, 0, Mathf.CeilToInt(left)))
                        _city.EventBanner = $"PHOTO SHOOT · {_eventSpot.Name.ToUpperInvariant()} · LAND {CityLife.PhotoShootPoints:N0}+ NEAR THE CAMERA · {Mathf.CeilToInt(left)}s";
                    _city.EventTarget = dist > CityLife.PhotoShootRadius ? (Vector3?)_eventPos : null;
                    break;
            }
        }

        private void StartEvent(StreetEventKind kind)
        {
            var here = RetroCityLayout.SpotAt(_player.transform.position.x, _player.transform.position.z);
            _eventKind = kind;
            _eventOn = true;
            _eventEnds = Time.time + CityLife.EventSeconds(kind);
            _eventSpot = _planner.PickSpot(_city.Progress.spots, kind == StreetEventKind.BlockParty ? null : here?.Id);
            switch (kind)
            {
                case StreetEventKind.BlockParty:
                    _eventPos = new Vector3(_eventSpot.X, 0f, _eventSpot.Z);
                    _eventMarker = Ring(_eventPos, _eventSpot.Radius, Palette.NeonPink);
                    break;
                case StreetEventKind.GoldenTape:
                    _eventPos = GoldenTapeSpot();
                    _eventMarker = GoldenTape(_eventPos);
                    break;
                default:
                    _eventPos = new Vector3(_eventSpot.MarkerX, 0f, _eventSpot.MarkerZ);
                    _eventMarker = Tripod(_eventPos);
                    break;
            }
            _city.Announce(CityLife.EventName(kind) + "! " + (kind == StreetEventKind.BlockParty ? "2X POINTS AT " + _eventSpot.Name.ToUpperInvariant()
                : kind == StreetEventKind.GoldenTape ? "FIND IT BEFORE IT'S GONE" : "LAND A BIG ONE AT " + _eventSpot.Name.ToUpperInvariant()), Theme.Tape);
            AudioManager.Ensure().PlaySfx(SfxId.SpecialReady, 0.8f);
        }

        private void EndEvent(string message)
        {
            _eventOn = false;
            _combo.BonusFactor = 1f;
            _city.EventBanner = null;
            _city.EventTarget = null;
            if (_eventMarker != null) Destroy(_eventMarker);
            _eventMarker = null;
            if (!string.IsNullOrEmpty(message)) _city.Announce(message, message.StartsWith("TIME") ? Theme.Coral : Theme.Tape);
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            if (!_eventOn || _eventKind != StreetEventKind.PhotoShoot || result.Points < CityLife.PhotoShootPoints) return;
            var p = _player.transform.position;
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_eventPos.x, _eventPos.z)) > CityLife.PhotoShootRadius) return;
            SaveManager.AddTokens(CityLife.PhotoShootTokens);
            AudioManager.Ensure().PlaySfx(SfxId.GoalComplete);
            HapticsManager.Play(HapticKind.Success);
            EndEvent($"GREAT SHOT!  +{CityLife.PhotoShootTokens}");
        }

        /// <summary>A street point 25-60 m away from the skater, on a road (always clear ground).</summary>
        private Vector3 GoldenTapeSpot()
        {
            var p = _player.transform.position;
            var lines = RetroCityLayout.StreetLines;
            Vector3 best = new Vector3(lines[1], 0f, 0f);
            float bestScore = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                float line = lines[Random.Range(0, lines.Length)];
                float along = Random.Range(-RetroCityLayout.HalfSize + 12f, RetroCityLayout.HalfSize - 12f);
                var c = Random.value < 0.5f ? new Vector3(line, 0f, along) : new Vector3(along, 0f, line);
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), c);
                float score = Mathf.Abs(d - 40f);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        private GameObject Ring(Vector3 c, float radius, Color color)
        {
            var root = new GameObject("BlockParty");
            root.transform.SetParent(transform, false);
            root.transform.position = c;
            PrimitiveMeshes.CreateVisual("Ring", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), color)
                .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(color, 0.6f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var pos = new Vector3(Mathf.Cos(a) * radius, 3f, Mathf.Sin(a) * radius);
                PrimitiveMeshes.CreateVisual("Pole", PrimitiveType.Cylinder, root.transform, pos, new Vector3(0.18f, 3f, 0.18f), i % 2 == 0 ? Palette.NeonPink : Palette.NeonCyan)
                    .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(i % 2 == 0 ? Palette.NeonPink : Palette.NeonCyan, 2.5f);
            }
            return root;
        }

        private GameObject GoldenTape(Vector3 c)
        {
            var root = new GameObject("GoldenTape");
            root.transform.SetParent(transform, false);
            root.transform.position = c + Vector3.up * 0.9f;
            PrimitiveMeshes.CreateVisual("Shell", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(1.1f, 0.7f, 0.16f), Palette.TapeYellow)
                .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(1f, 0.8f, 0.2f), 3f);
            PrimitiveMeshes.CreateVisual("Beam", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 8f, 0f), new Vector3(0.25f, 8f, 0.25f), Palette.TapeYellow)
                .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(1f, 0.8f, 0.2f), 1.5f);
            return root;
        }

        private GameObject Tripod(Vector3 c)
        {
            var root = new GameObject("PhotoShoot");
            root.transform.SetParent(transform, false);
            root.transform.position = c;
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                var leg = PrimitiveMeshes.CreateVisual("Leg", PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(0.05f, 0.8f, 0.05f), Palette.Ink).transform;
                leg.localRotation = Quaternion.Euler(0f, a, 15f);
                leg.localPosition = leg.localRotation * new Vector3(0f, 0.75f, 0f);
            }
            PrimitiveMeshes.CreateVisual("Camera", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.35f, 0.25f, 0.25f), Palette.Ink);
            PrimitiveMeshes.CreateVisual("Flash", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.82f, 0f), new Vector3(0.2f, 0.1f, 0.12f), Palette.Cream)
                .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Color.white, 3f);
            PrimitiveMeshes.CreateVisual("Zone", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(CityLife.PhotoShootRadius * 2f, 0.01f, CityLife.PhotoShootRadius * 2f), Palette.TapeYellow)
                .GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.TapeYellow, 0.4f);
            return root;
        }
    }
}
