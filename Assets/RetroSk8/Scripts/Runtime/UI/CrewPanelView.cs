using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → CREW: your crew level, the eight skaters you can recruit, and the two who ride with you.</summary>
    public sealed class CrewPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Text _level;
        private Image _xpFill;
        private Text _riding;
        private readonly List<(CrewMember m, Image card, Text status, Button action, Text actionLabel)> _cards = new List<(CrewMember, Image, Text, Button, Text)>();

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "CREW", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(260f, 100f));

            _level = UIFactory.Label("Level", root, "", 34, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_level.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, -34f), new Vector2(1000f, 50f));
            var bar = UIFactory.Panel("XpBar", root, new Color(1f, 1f, 1f, 0.12f));
            UIFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(360f, -94f), new Vector2(900f, 18f));
            _xpFill = UIFactory.Panel("Fill", bar.transform, Theme.Tape);
            _xpFill.rectTransform.anchorMin = Vector2.zero;
            _xpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _xpFill.rectTransform.offsetMin = _xpFill.rectTransform.offsetMax = Vector2.zero;
            _riding = UIFactory.Label("Riding", root, "", 28, Theme.Teal, TextAnchor.MiddleRight);
            UIFactory.Place(_riding.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -50f), new Vector2(760f, 80f));
            _riding.horizontalOverflow = HorizontalWrapMode.Wrap;

            var grid = UIFactory.Rect("Grid", root);
            UIFactory.Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(2000f, 760f));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(485f, 370f);
            g.spacing = new Vector2(20f, 20f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            foreach (var m in CrewRoster.Members) _cards.Add(Card(grid, m));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
            var hint = UIFactory.Label("Hint", root, "RECRUIT SKATERS BY BEATING THEIR CHALLENGE · TWO RIDE WITH YOU AND ADD THEIR PERKS · EVERY BANKED COMBO GIVES CREW XP", 24, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(1600f, 60f));
            Refresh();
        }

        private (CrewMember, Image, Text, Button, Text) Card(RectTransform grid, CrewMember m)
        {
            var card = UIFactory.Panel("Card_" + m.Id, grid, Theme.InkSoft);
            var portrait = UIFactory.Rect("Portrait", card.transform);
            UIFactory.Place(portrait, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(110f, 140f));
            var skin = LookPalette.SkinTones[m.SkinTone % LookPalette.SkinTones.Length];
            var hair = LookPalette.HairColors[m.HairColor % LookPalette.HairColors.Length];
            var head = UIFactory.Panel("Head", portrait, new Color(skin.R, skin.G, skin.B));
            head.sprite = UIFactory.Circle;
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(80f, 90f));
            var hairImg = UIFactory.Panel("Hair", portrait, new Color(hair.R, hair.G, hair.B));
            hairImg.sprite = UIFactory.Circle;
            UIFactory.Place(hairImg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, m.HairStyle == 2 ? -4f : -12f), new Vector2(m.HairStyle == 2 ? 110f : 86f, m.HairStyle == 2 ? 70f : 40f));
            hairImg.transform.SetAsFirstSibling();
            var body = UIFactory.Panel("Body", portrait, Theme.Teal);
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(100f, 36f));

            var name = UIFactory.Label("Name", card.transform, m.Name.ToUpperInvariant(), 28, Theme.Tape, TextAnchor.UpperLeft, false);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(145f, -18f), new Vector2(325f, 70f));
            var park = _content.FindLocation(m.HomePark);
            var info = UIFactory.Label("Info", card.transform,
                $"{StyleTricks.StyleNames[(int)m.Style]} · {(park != null ? park.displayName.ToUpperInvariant() : m.HomePark)}\nPERK: {CrewRoster.PerkText(m.Perk)}", 22, Theme.Cream, TextAnchor.UpperLeft, false);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(145f, -92f), new Vector2(325f, 80f));
            var bio = UIFactory.Label("Bio", card.transform, m.Bio, 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperLeft, false);
            bio.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(bio.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -175f), new Vector2(445f, 70f));
            var status = UIFactory.Label("Status", card.transform, "", 22, Theme.Teal, TextAnchor.UpperLeft, false);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -245f), new Vector2(445f, 40f));
            var action = UIFactory.MakeButton("Action", card.transform, "", new Vector2(445f, 70f), Theme.Tape, () => OnAction(m), 30);
            UIFactory.Place((RectTransform)action.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(445f, 70f));
            return (m, card, status, action, action.GetComponentInChildren<Text>());
        }

        private void OnEnable()
        {
            if (_level != null) Refresh();
        }

        private void OnAction(CrewMember m)
        {
            var s = CrewService.State;
            if (!s.IsRecruited(m.Id))
            {
                CrewService.StartRecruit(m, _content);
                return;
            }
            s.ToggleActive(m.Id);
            RetroSk8.Save.SaveManager.Save();
            Refresh();
        }

        private void Refresh()
        {
            var s = CrewService.State;
            int level = s.Level;
            _level.text = $"CREW LEVEL {level} · {CrewLevels.Title(level)} · {s.xp:N0} XP" + (WeeklyService.Modifier == WeeklyModifier.CrewXp ? "   (2X XP THIS WEEK)" : "");
            _xpFill.rectTransform.anchorMax = new Vector2(CrewLevels.Progress(s.xp), 1f);
            var names = new List<string>();
            foreach (var id in s.active) { var m = CrewRoster.Find(id); if (m != null) names.Add(CrewService.ShortName(m)); }
            _riding.text = names.Count == 0 ? "NOBODY RIDING WITH YOU YET" : "RIDING WITH YOU: " + string.Join(" + ", names.ToArray());

            foreach (var (m, card, status, action, label) in _cards)
            {
                bool recruited = s.IsRecruited(m.Id), active = s.IsActive(m.Id);
                card.color = active ? new Color(0.12f, 0.32f, 0.3f, 0.95f) : Theme.InkSoft;
                status.text = recruited ? (active ? "RIDING WITH YOU" : "IN YOUR CREW") : "TO RECRUIT: " + CrewRoster.RecruitText(m);
                label.text = !recruited ? "CHALLENGE" : active ? "BENCH" : s.active.Count >= CrewRoster.MaxActive ? "PAIR IS FULL" : "RIDE WITH ME";
                action.interactable = !recruited || active || s.active.Count < CrewRoster.MaxActive;
            }
        }
    }
}
