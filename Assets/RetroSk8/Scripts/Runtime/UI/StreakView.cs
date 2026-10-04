using System;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// The daily check-in card (Phase 15): your streak day, the 7-day reward strip with today stamped, Tape Savers,
    /// and what happened (a saver used, a new saver, a milestone, a fresh start). Pops in, tap to close.
    /// </summary>
    public sealed class StreakView : MonoBehaviour
    {
        private RectTransform _card;
        private float _t;
        private bool _reduced;

        public static void Show(RectTransform parent, StreakResult r)
        {
            if (r == null || !r.NewDay) return;
            var root = UIFactory.Rect("Streak", parent);
            UIFactory.Stretch(root);
            root.gameObject.AddComponent<StreakView>().Build(root, r);
        }

        private void Build(RectTransform root, StreakResult r)
        {
            _reduced = SaveManager.Data.settings.reducedMotion;
            var dim = UIFactory.Panel("Dim", root, new Color(0f, 0f, 0f, 0.72f), true);
            UIFactory.Stretch(dim.rectTransform);

            var burst = new GameObject("Burst").AddComponent<RawImage>();
            burst.transform.SetParent(root, false);
            burst.texture = ComicArt.Sunburst;
            burst.color = new Color(Theme.Tape.r, Theme.Tape.g, Theme.Tape.b, 0.16f);
            burst.raycastTarget = false;
            UIFactory.Place(burst.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 1500f));
            burst.gameObject.AddComponent<Spin>().Speed = _reduced ? 0f : 6f;

            var card = UIFactory.Panel("Card", root, Theme.Ink, true);
            _card = card.rectTransform;
            UIFactory.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 700f));
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(14f, -14f);
            UIFactory.Scanlines(card.transform, 0.12f);

            var tag = UIFactory.TapeLabel("Tag", card.transform, "DAILY STREAK", 34, Theme.Tape, -3f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 64f));

            var day = UIFactory.Label("Day", card.transform, $"DAY {r.Day}", 120, Theme.Tape, TextAnchor.MiddleCenter);
            day.GetComponent<Outline>().effectDistance = new Vector2(6f, -6f);
            UIFactory.Place(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 150f));

            // The 7-day strip.
            int slot = Streaks.CardSlot(r.Day);
            for (int i = 0; i < Streaks.Card.Length; i++)
            {
                bool today = i == slot, done = i < slot;
                var cell = UIFactory.Panel("Slot" + i, card.transform, today ? Theme.Tape : done ? Theme.Teal : new Color(1f, 1f, 1f, 0.08f));
                float w = i == 6 ? 190f : 140f;
                float x = -540f + i * 152f + (i == 6 ? 25f : 0f);
                UIFactory.Place(cell.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -240f), new Vector2(w, 170f));
                if (today) cell.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
                var c = today ? Theme.Ink : Theme.Cream;
                var d = UIFactory.Label("D", cell.transform, $"DAY {i + 1}", 24, c, TextAnchor.UpperCenter, false);
                UIFactory.Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(w, 34f));
                var t = UIFactory.Label("T", cell.transform, "+" + Streaks.Card[i], i == 6 ? 56 : 46, c, TextAnchor.MiddleCenter, false);
                UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(w, 70f));
                if (done)
                {
                    var tick = UIFactory.Label("Tick", cell.transform, "OK", 22, Theme.Ink, TextAnchor.LowerCenter, false);
                    UIFactory.Place(tick.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(w, 30f));
                }
            }

            var reward = UIFactory.Label("Reward", card.transform, $"+{r.Tokens} TAPE TOKENS", 52, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(reward.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(1100f, 70f));

            var note = UIFactory.Label("Note", card.transform, Note(r), 26, Theme.Teal, TextAnchor.MiddleCenter, false);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(1100f, 60f));

            var go = UIFactory.MakeButton("Go", card.transform, "LET'S SKATE", new Vector2(420f, 90f), Theme.Coral, () => Destroy(root.gameObject), 42);
            UIFactory.Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(420f, 90f));

            AudioManager.Instance?.PlaySfx(r.Milestone || slot == 6 ? SfxId.GoalComplete : SfxId.UiClick);
            if (!_reduced) _card.localScale = Vector3.one * 0.6f;
        }

        private static string Note(StreakResult r)
        {
            var s = StreakService.State;
            string savers = $"TAPE SAVERS: {s.savers}/{Streaks.MaxSavers}";
            if (r.Milestone) return $"{r.Day} DAYS IN A ROW. MILESTONE BONUS +{Streaks.MilestoneBonus}!  ·  {savers}";
            if (r.SaversUsed > 0) return $"A TAPE SAVER COVERED {r.SaversUsed} MISSED DAY{(r.SaversUsed == 1 ? "" : "S")}. STREAK SAFE.  ·  {savers}";
            if (r.EarnedSaver) return $"NEW TAPE SAVER! IT COVERS A MISSED DAY.  ·  {savers}";
            if (r.Reset) return $"FRESH START. YOUR BEST IS {s.best} DAYS.  ·  {savers}";
            return $"COME BACK TOMORROW FOR +{Streaks.RewardFor(r.Day + 1)}  ·  BEST {s.best}  ·  {savers}";
        }

        private void Update()
        {
            if (_reduced || _card == null || _t > 1f) return;
            _t += Time.unscaledDeltaTime * 3f;
            float k = Mathf.Clamp01(_t);
            // EaseOutBack
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, e);
        }

        private sealed class Spin : MonoBehaviour
        {
            public float Speed;
            private void Update() => transform.Rotate(0f, 0f, -Speed * Time.unscaledDeltaTime);
        }
    }
}
