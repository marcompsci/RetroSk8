using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Main menu → CODES: paste (or type) a friend's park or challenge code, see what it is, then import the park
    /// or take on the challenge. Also where you set the name shown on your own codes.
    /// </summary>
    public sealed class CodesPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private InputField _code;
        private InputField _name;
        private Text _info;
        private Button _accept;
        private Text _acceptLabel;
        private CustomPark _park;
        private ScoreChallenge _challenge;
        /// <summary>Phase 26: a pasted race ghost code, decoded.</summary>
        private RaceGhost _raceGhost;
        /// <summary>A pasted ghost code (tens of thousands of characters: kept here, not drawn in the text field).</summary>
        private string _ghostText;
        private Text _rivals;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "CODES", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(360f, 100f));
            var sub = UIFactory.Label("Sub", root, "SHARE PARKS, SCORE CHALLENGES, GHOSTS AND RACE GHOSTS WITH FRIENDS · COPY A CODE, SEND IT ANY WAY YOU LIKE, PASTE IT HERE", 26, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1900f, 40f));

            _code = Field(root, "CodeField", new Vector2(0f, -220f), new Vector2(1500f, 150f), "PASTE OR TYPE A CODE", 30, 400);
            _code.onEndEdit.AddListener(_ => { if (_ghostText == null) Inspect(_code.text); });
            _code.onValueChanged.AddListener(v => { if (_ghostText != null && !v.StartsWith("GHOST CODE") && !v.StartsWith("RACE GHOST CODE")) _ghostText = null; });

            var row = UIFactory.Rect("Row", root);
            UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(1500f, 100f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 20f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            UIFactory.MakeButton("Paste", row, "PASTE", new Vector2(300f, 96f), Theme.Cream, PasteCode, 40);
            UIFactory.MakeButton("Clear", row, "CLEAR", new Vector2(240f, 96f), Theme.Cream, () => { _ghostText = null; _code.text = ""; Inspect(""); }, 36);
            _accept = UIFactory.MakeButton("Accept", row, "", new Vector2(520f, 96f), Theme.Tape, Accept, 40);
            _acceptLabel = _accept.GetComponentInChildren<Text>();

            _info = UIFactory.Label("Info", root, "", 34, Theme.Cream, TextAnchor.UpperCenter, false);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(1500f, 180f));

            var nameLabel = UIFactory.Label("NameLabel", root, "YOUR NAME ON CODES", 28, Theme.Teal, TextAnchor.MiddleRight);
            UIFactory.Place(nameLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-520f, 50f), new Vector2(420f, 80f));
            _name = Field(root, "NameField", Vector2.zero, new Vector2(440f, 80f), "", 34, ShareCodes.MaxFromLength);
            UIFactory.Place((RectTransform)_name.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(440f, 80f));
            _name.onEndEdit.AddListener(v =>
            {
                string clean = Gallery.SafeName(v, ShareCodes.MaxFromLength, "SKATER"); // Phase 25: the word filter too
                SaveManager.Data.settings.playerName = clean;
                SaveManager.Save();
                _name.text = clean;
            });

            _rivals = UIFactory.Label("Rivals", root, "", 26, Theme.Cream, TextAnchor.LowerLeft, false);
            _rivals.lineSpacing = 1.1f;
            UIFactory.Place(_rivals.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 150f), new Vector2(1100f, 200f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            // Phase 16: the online gallery of shared parks and ghosts.
            var galleryButton = UIFactory.MakeButton("Gallery", root, "GALLERY", new Vector2(300f, 90f), Theme.Teal, () => _gallery.SetActive(true), 40);
            UIFactory.Place((RectTransform)galleryButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -30f), new Vector2(300f, 90f));
            var galleryRoot = UIFactory.Rect("GalleryPanel", root);
            UIFactory.Stretch(galleryRoot);
            galleryRoot.gameObject.AddComponent<GalleryView>().Build(galleryRoot, content, () => _gallery.SetActive(false));
            _gallery = galleryRoot.gameObject;
            _gallery.SetActive(false);
        }

        private GameObject _gallery;

        private void OnEnable()
        {
            if (_name == null) return;
            _name.text = SaveManager.Data.settings.playerName;
            Inspect(_code.text);
            RefreshRivals();
        }

        private static InputField Field(RectTransform root, string name, Vector2 pos, Vector2 size, string placeholder, int font, int limit)
        {
            var bg = UIFactory.Panel(name, root, new Color(1f, 1f, 1f, 0.1f), true);
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, size);
            var text = UIFactory.Label("Text", bg.transform, "", font, Theme.Tape, TextAnchor.MiddleLeft, false);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            UIFactory.Stretch(text.rectTransform, 16f);
            var hint = UIFactory.Label("Placeholder", bg.transform, placeholder, font, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft, false);
            UIFactory.Stretch(hint.rectTransform, 16f);
            var field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.MultiLineNewline;
            return field;
        }

        private void PasteCode()
        {
            string text = ShareService.Paste();
            if (GhostCodes.IsGhostCode(text) || RaceGhostCodes.IsRaceCode(text))
            {
                _ghostText = text;
                _code.text = (RaceGhostCodes.IsRaceCode(text) ? "RACE GHOST CODE" : "GHOST CODE") + $"  ({text.Length:N0} CHARACTERS)";
                InspectGhost(text);
                return;
            }
            _ghostText = null;
            _code.text = text.Length > 400 ? text.Substring(0, 400) : text;
            Inspect(_code.text);
        }

        private void InspectGhost(string text)
        {
            _park = null;
            _challenge = null;
            _raceGhost = null;
            if (RaceGhostCodes.IsRaceCode(text))
            {
                // Phase 26: a friend's run of a Retro City race.
                if (!RaceGhostCodes.TryDecode(text, out var g, out var raceError)) { Show(raceError, null); return; }
                _raceGhost = g;
                var race = RetroCityLayout.FindRace(g.RaceId);
                string who = string.IsNullOrEmpty(g.From) ? "A FRIEND" : g.From;
                float mine = SaveManager.Data.city.RaceBest(g.RaceId);
                string yours = mine > 0f ? $"  ·  YOUR BEST {CityController.FormatTime(mine)}" : "";
                Show($"{who}'S {race.Name.ToUpperInvariant()} GHOST · {CityController.FormatTime(g.Time)}{yours}\nRACE THEIR LINE THROUGH RETRO CITY, WITH SPLITS AT EVERY GATE (THEIR GHOST SKATES IN PINK)", "RACE IT");
                return;
            }
            if (!GhostCodes.TryDecode(text, out var c, out var error)) { Show(error, null); return; }
            _challenge = c;
            string where = c.Park != null ? c.Park.name + " (THEIR PARK)" : _content.FindLocation(c.LocationId).displayName.ToUpperInvariant();
            string from = string.IsNullOrEmpty(c.From) ? "A FRIEND" : c.From;
            Show($"{from}'S GHOST · {c.Target:N0} AT {where}\nRACE THEIR ACTUAL RUN IN A TWO-MINUTE RUN (THEIR GHOST SKATES IN PINK)", "RACE THE GHOST");
        }

        private void Inspect(string text)
        {
            if (_ghostText != null) { InspectGhost(_ghostText); return; }
            if (RaceGhostCodes.IsRaceCode(text)) { InspectGhost(text); return; }
            _park = null;
            _challenge = null;
            _raceGhost = null;
            string kind = ShareCodes.KindOf(text);
            string error;
            if (string.IsNullOrWhiteSpace(text))
            {
                Show("Codes start with RP (a park), RC (a challenge), RG: (a ghost to race) or RR: (a city race ghost).", null);
                return;
            }
            if (kind == ShareCodes.ParkPrefix && ShareCodes.TryDecodePark(text, out var park, out error))
            {
                _park = park;
                Show($"PARK: {park.name}\n{park.pieces.Count} PIECES · {ParkEditorController.ThemeName(park.Theme)}", "ADD TO MY PARKS");
                return;
            }
            if (kind == ShareCodes.ChallengePrefix && ShareCodes.TryDecodeChallenge(text, out var c, out error))
            {
                _challenge = c;
                string where = c.Park != null ? c.Park.name + " (THEIR PARK)" : _content.FindLocation(c.LocationId).displayName.ToUpperInvariant();
                string from = string.IsNullOrEmpty(c.From) ? "A FRIEND" : c.From;
                Show($"CHALLENGE FROM {from}\nBEAT {c.Target:N0} IN A TWO-MINUTE RUN AT {where}", "TAKE IT ON");
                return;
            }
            if (kind == null) error = "THAT ISN'T A RETRO SK8 CODE";
            else if (kind == ShareCodes.ParkPrefix) ShareCodes.TryDecodePark(text, out _, out error);
            else ShareCodes.TryDecodeChallenge(text, out _, out error);
            Show(error, null);
        }

        private void RefreshRivals()
        {
            var list = SaveManager.Data.rivals;
            if (list == null || list.Count == 0) { _rivals.text = ""; return; }
            var sb = new System.Text.StringBuilder("RECENT GHOST RACES\n");
            for (int i = 0; i < list.Count && i < 4; i++)
            {
                var r = list[i];
                // Phase 26: race ghosts are times (hundredths); an unfinished race shows as DNF.
                string mine = r.timed ? (r.myScore > 0 ? CityController.FormatTime(r.myScore / 100f) : "DNF") : r.myScore.ToString("N0");
                string theirs = r.timed ? CityController.FormatTime(r.theirScore / 100f) : r.theirScore.ToString("N0");
                sb.Append(r.won ? "■ BEAT " : "□ LOST TO ").Append(r.from).Append("  ")
                  .Append(mine).Append(" VS ").Append(theirs)
                  .Append("  · ").Append(r.parkName).Append('\n');
            }
            _rivals.text = sb.ToString().TrimEnd();
        }

        private void Show(string info, string action)
        {
            _info.text = info;
            _accept.gameObject.SetActive(action != null);
            if (action != null) _acceptLabel.text = action;
        }

        private void Accept()
        {
            if (_park != null)
            {
                string id = ShareService.ImportPark(_park);
                _info.text = id == null
                    ? "ALL SIX PARK SLOTS ARE FULL. DELETE ONE IN CREATE-A-PARK FIRST."
                    : $"ADDED TO CREATE-A-PARK SLOT {id.Substring(CustomParkIds.Prefix.Length)}.";
                if (id != null) _accept.gameObject.SetActive(false);
                return;
            }
            if (_raceGhost != null) { ShareService.StartRaceGhost(_raceGhost); return; }
            if (_challenge != null) ShareService.StartChallenge(_challenge, _content);
        }
    }
}
