using System;
using System.Collections.Generic;
using System.Text;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Pause-menu city map: streets, spots (found ones show their medal and can be tapped to fast travel),
    /// race start flags (tap to warp to the line and go), tape count and your position.
    /// </summary>
    public sealed class CityMapView : MonoBehaviour
    {
        private const float MapHalf = 420f;

        private CityController _city;
        private Action _back;
        private Action _resume;
        private RectTransform _map;
        private RectTransform _player;
        private Text _summary;
        private Button _jamButton;
        private Button _ghostButton;
        private Text _ghostLabel;
        private string _ghostRaceId;
        private readonly List<(CitySpot spot, Image icon, Text label, Button button)> _spots = new List<(CitySpot, Image, Text, Button)>();

        // The map covers the grid plus the Riverside Yards to the north, so it is centred a little north of the grid.
        private const float WorldSouth = -RetroCityLayout.HalfSize - 5f;
        private const float WorldNorth = RetroCityLayout.YardsNorth + 5f;
        private const float CenterZ = (WorldSouth + WorldNorth) * 0.5f;
        private static float Scale => MapHalf / ((WorldNorth - WorldSouth) * 0.5f);
        private static Vector2 ToMap(float x, float z) => new Vector2(x * Scale, (z - CenterZ) * Scale);

        /// <param name="back">Close the map and return to the pause menu.</param>
        /// <param name="resume">Close everything and resume skating (after fast travel or a race start).</param>
        public void Build(RectTransform root, CityController city, Action back, Action resume)
        {
            _city = city;
            _back = back;
            _resume = resume;

            var dim = UIFactory.Panel("Dim", root, new Color(0.04f, 0.05f, 0.07f, 0.94f), true);
            UIFactory.Stretch(dim.rectTransform);

            // Map square.
            var frame = UIFactory.Panel("MapFrame", root, new Color(0.16f, 0.17f, 0.2f));
            UIFactory.Place(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(MapHalf * 2f + 20f, MapHalf * 2f + 20f));
            _map = UIFactory.Rect("Map", frame.transform);
            UIFactory.Place(_map, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapHalf * 2f, MapHalf * 2f));
            float citySize = 2f * RetroCityLayout.HalfSize * Scale;
            Bar("Blocks", ToMap(0f, 0f), new Vector2(citySize, citySize), new Color(0.3f, 0.3f, 0.33f));
            float yardsLen = RetroCityLayout.YardsNorth - RetroCityLayout.YardsSouth;
            Bar("Yards", ToMap(0f, RetroCityLayout.YardsSouth + yardsLen * 0.5f), new Vector2(2f * RetroCityLayout.YardsHalfWidth * Scale, yardsLen * Scale), new Color(0.34f, 0.3f, 0.28f));
            float road = RetroCityLayout.RoadHalf * 2f * Scale;
            foreach (float line in RetroCityLayout.StreetLines)
            {
                Bar("StreetX", ToMap(0f, line), new Vector2(citySize, road), new Color(0.12f, 0.12f, 0.14f));
                Bar("StreetZ", ToMap(line, 0f), new Vector2(road, citySize), new Color(0.12f, 0.12f, 0.14f));
            }
            // Canal through the east block, and the yard's track.
            Bar("Canal", ToMap(70f, 0f), new Vector2(16f * Scale, 58f * Scale), new Color(0.15f, 0.35f, 0.45f));
            Bar("Track", ToMap(-6f, RetroCityLayout.YardsSouth + yardsLen * 0.5f), new Vector2(2f * Scale, yardsLen * Scale), new Color(0.45f, 0.4f, 0.32f));
            UIFactory.Scanlines(frame.transform, 0.12f);

            foreach (var r in RetroCityLayout.Races)
            {
                var race = r;
                var flag = UIFactory.MakeButton("Race_" + r.Id, _map, "", new Vector2(44f, 44f), Theme.Tape, () => StartRace(race), 22);
                ((RectTransform)flag.transform).anchoredPosition = ToMap(r.Gates[0], r.Gates[1]);
                var txt = flag.GetComponentInChildren<Text>();
                txt.text = ">";
                txt.color = Theme.Ink;
            }

            foreach (var s in RetroCityLayout.Spots)
            {
                var spot = s;
                var b = UIFactory.MakeButton("Spot_" + s.Id, _map, "?", new Vector2(64f, 64f), Theme.Cream, () => Travel(spot), 34);
                var rt = (RectTransform)b.transform;
                rt.anchoredPosition = ToMap(s.X, s.Z);
                var img = b.GetComponent<Image>();
                img.sprite = UIFactory.Circle;
                var label = UIFactory.Label("Name", rt, "", 20, Theme.Cream, TextAnchor.UpperCenter);
                UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(240f, 30f));
                _spots.Add((s, img, label, b));
            }

            var you = UIFactory.Panel("You", _map, Theme.Coral);
            you.sprite = UIFactory.Circle;
            _player = you.rectTransform;
            UIFactory.Place(_player, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var nose = UIFactory.Panel("Nose", _player, Theme.Coral);
            UIFactory.Place(nose.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(10f, 22f));

            // Side panel.
            var title = UIFactory.TapeLabel("Title", root, "RETRO CITY", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260f, -50f), new Vector2(520f, 110f));
            _summary = UIFactory.Label("Summary", root, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            UIFactory.Place(_summary.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -190f), new Vector2(1060f, 640f));

            var close = UIFactory.MakeButton("Close", root, "BACK", new Vector2(300f, 90f), Theme.Cream, () => _back?.Invoke(), 40);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(300f, 90f));
            // Phase 15: today's City Jam.
            var jam = UIFactory.MakeButton("Jam", root, "START CITY JAM", new Vector2(420f, 90f), Theme.Coral, StartJam, 36);
            UIFactory.Place((RectTransform)jam.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-380f, 120f), new Vector2(420f, 90f));
            _jamButton = jam;
            // Phase 26: copy a race ghost code (your best run of a race) to send to a friend.
            _ghostButton = UIFactory.MakeButton("SendRaceGhost", root, "", new Vector2(520f, 90f), Theme.Teal, SendRaceGhost, 32);
            UIFactory.Place((RectTransform)_ghostButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-850f, 120f), new Vector2(520f, 90f));
            _ghostLabel = _ghostButton.GetComponentInChildren<Text>();
            var hint = UIFactory.Label("Hint", root, "TAP A FOUND SPOT TO SKATE THERE · TAP > TO RACE YOUR BEST GHOST", 24, Theme.Cream, TextAnchor.LowerRight);
            UIFactory.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-380f, 60f), new Vector2(900f, 40f));

            city.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_city != null) _city.Changed -= Refresh;
        }

        private void OnEnable()
        {
            if (_city != null) Refresh();
        }

        private void Bar(string name, Vector2 pos, Vector2 size, Color color)
        {
            var bar = UIFactory.Panel(name, _map, color);
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        }

        private void Refresh()
        {
            var p = _city.Progress;
            foreach (var (spot, icon, label, button) in _spots)
            {
                bool found = p.HasSpot(spot.Id);
                var medal = p.ChallengeMedal(spot.Id);
                icon.color = !found ? new Color(0.45f, 0.45f, 0.48f) : MedalColor(medal);
                button.GetComponentInChildren<Text>().text = !found ? "?" : medal == Medal.None ? "*" : medal.ToString().Substring(0, 1);
                label.text = found ? spot.Name.ToUpperInvariant() : "";
                button.interactable = found && _city.ActivitiesEnabled;
            }

            var sb = new StringBuilder();
            sb.Append($"SPOTS FOUND  {p.spots.Count}/{RetroCityLayout.Spots.Count}\n");
            sb.Append($"TAPES        {p.tapes.Count}/{RetroCityLayout.Tapes.Count}\n\n");
            sb.Append("SPOT CHALLENGES\n");
            foreach (var s in RetroCityLayout.Spots)
            {
                if (!p.HasSpot(s.Id)) continue;
                var m = p.ChallengeMedal(s.Id);
                sb.Append($"  {s.Name.ToUpperInvariant(),-22} {MedalRules.Label(m)}\n");
            }
            sb.Append("\nRACES\n");
            foreach (var r in RetroCityLayout.Races)
            {
                float best = p.RaceBest(r.Id);
                sb.Append($"  {r.Name.ToUpperInvariant(),-22} {MedalRules.Label(p.RaceMedal(r.Id))}{(best > 0f ? "  " + CityController.FormatTime(best) : "")}\n");
            }
            var jamRec = SaveManager.Data.jam;
            var todayMedal = jamRec.day == CityController.Today ? (Medal)jamRec.medal : Medal.None;
            var kind = CityJam.KindFor(CityController.Today);
            sb.Append($"\n{CityJam.Title(kind)} TODAY: ");
            var stops = CityController.TodaysJam();
            for (int i = 0; i < stops.Count; i++) sb.Append(i == 0 ? "" : " > ").Append(stops[i].Spot.Name.ToUpperInvariant());
            if (kind == JamKind.BonkHunt) sb.Append("\n  ").Append(CityJam.Rule(kind));
            sb.Append($"\n  TODAY'S MEDAL: {MedalRules.Label(todayMedal)}\n");
            _jamButton.interactable = _city.ActivitiesEnabled && _city.Jam == null;
            if (!_city.ActivitiesEnabled) sb.Append("\nCHALLENGES, RACES, JAMS AND FAST TRAVEL: PLAY EXPLORE CITY");
            _summary.text = sb.ToString();
            RefreshGhostButton();
        }

        /// <summary>The race to send: the one you just finished, else the first with a saved best ghost.</summary>
        private void RefreshGhostButton()
        {
            var p = _city.Progress;
            _ghostRaceId = null;
            string last = _city.LastFinishedRaceId;
            foreach (var r in RetroCityLayout.Races)
            {
                if (!RaceSplits.AreValid(p.RaceBestSplits(r.Id), r.GateCount, p.RaceBest(r.Id))) continue;
                if (r.Id != last && _ghostRaceId != null) continue; // only one file check per refresh after the first
                if (ShareService.LoadBestRaceGhost(r.Id) == null) continue;
                if (_ghostRaceId == null || r.Id == last) _ghostRaceId = r.Id;
            }
            _ghostButton.gameObject.SetActive(_ghostRaceId != null);
            if (_ghostRaceId != null) _ghostLabel.text = $"SEND {RetroCityLayout.FindRace(_ghostRaceId).Name.ToUpperInvariant()} GHOST";
        }

        private void SendRaceGhost()
        {
            if (_ghostRaceId == null) return;
            string code = ShareService.RaceGhostCode(_ghostRaceId);
            if (code == null) { _ghostLabel.text = "NO GHOST SAVED YET"; return; }
            ShareService.Copy(code);
            _ghostLabel.text = "RACE GHOST COPIED!";
        }

        private static Color MedalColor(Medal m) =>
            m == Medal.Gold ? Theme.Tape : m == Medal.Silver ? new Color(0.8f, 0.82f, 0.86f) : m == Medal.Bronze ? new Color(0.8f, 0.5f, 0.3f) : Theme.Teal;

        private void Update()
        {
            if (_city == null) return;
            var pos = _city.PlayerPosition;
            _player.anchoredPosition = ToMap(pos.x, pos.z);
            var h = _city.PlayerHeading;
            _player.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg);
        }

        private void Travel(CitySpot spot)
        {
            if (_city.TravelTo(spot)) _resume?.Invoke();
        }

        private void StartJam()
        {
            if (!_city.ActivitiesEnabled) return;
            _city.Abandon();
            _resume?.Invoke();
            _city.StartJam();
        }

        private void StartRace(CityRace race)
        {
            if (!_city.ActivitiesEnabled) return;
            _city.Abandon();
            _resume?.Invoke();
            _city.StartRace(race);
        }
    }
}
