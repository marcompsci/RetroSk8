using System;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → CREATE-A-PARK: six park slots, each with BUILD/EDIT, SKATE, 2-MIN RUN and DELETE.</summary>
    public sealed class CreateParkPanelView : MonoBehaviour
    {
        private readonly Text[] _names = new Text[CustomParkIds.MaxSlots];
        private readonly Text[] _infos = new Text[CustomParkIds.MaxSlots];
        private readonly Text[] _editLabels = new Text[CustomParkIds.MaxSlots];
        private readonly Button[] _skate = new Button[CustomParkIds.MaxSlots];
        private readonly Button[] _run = new Button[CustomParkIds.MaxSlots];
        private readonly Button[] _delete = new Button[CustomParkIds.MaxSlots];
        private readonly Text[] _deleteLabels = new Text[CustomParkIds.MaxSlots];
        private int _confirmDelete = -1;

        public void Build(RectTransform root, Action onClose)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.95f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "CREATE-A-PARK", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(640f, 100f));
            var sub = UIFactory.Label("Sub", root, "PLACE RAMPS, RAILS, LEDGES, STAIRS AND BOWLS · TAP ONE TO SELECT IT · ARROWS MOVE IT", 26, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(1800f, 40f));

            var col = UIFactory.Rect("Slots", root);
            UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1800f, 720f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;

            for (int i = 0; i < CustomParkIds.MaxSlots; i++)
            {
                int slot = i;
                var card = UIFactory.Panel("Slot" + (i + 1), col, Theme.InkSoft);
                card.rectTransform.sizeDelta = new Vector2(1800f, 104f);
                _names[i] = UIFactory.Label("Name", card.transform, "", 40, Theme.Tape, TextAnchor.MiddleLeft);
                UIFactory.Place(_names[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 12f), new Vector2(600f, 50f));
                _infos[i] = UIFactory.Label("Info", card.transform, "", 24, Theme.Cream, TextAnchor.MiddleLeft);
                UIFactory.Place(_infos[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, -28f), new Vector2(600f, 32f));

                var row = UIFactory.Rect("Buttons", card.transform);
                UIFactory.Place(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(1120f, 84f));
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 14f;
                h.childAlignment = TextAnchor.MiddleRight;
                h.childControlWidth = h.childControlHeight = false;
                var edit = UIFactory.MakeButton("Edit", row, "", new Vector2(250f, 80f), Theme.Tape, () => Edit(slot), 34);
                _editLabels[i] = edit.GetComponentInChildren<Text>();
                _skate[i] = UIFactory.MakeButton("Skate", row, "FREE SKATE", new Vector2(270f, 80f), Theme.Teal, () => Play(slot, RunMode.FreeSkate), 32);
                _run[i] = UIFactory.MakeButton("Run", row, "2-MIN RUN", new Vector2(250f, 80f), Theme.Cream, () => Play(slot, RunMode.TwoMinuteRun), 32);
                _delete[i] = UIFactory.MakeButton("Delete", row, "", new Vector2(250f, 80f), Theme.Coral, () => Delete(slot), 30);
                _deleteLabels[i] = _delete[i].GetComponentInChildren<Text>();
            }

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            Refresh();
        }

        private void OnEnable()
        {
            _confirmDelete = -1;
            if (_names[0] != null) Refresh();
        }

        private static string Id(int slot) => CustomParkIds.ForSlot(slot + 1);

        private void Refresh()
        {
            for (int i = 0; i < CustomParkIds.MaxSlots; i++)
            {
                var park = SaveManager.FindCustomPark(Id(i));
                bool exists = park != null;
                _names[i].text = exists ? park.name : $"SLOT {i + 1} · EMPTY";
                _names[i].color = exists ? Theme.Tape : new Color(0.6f, 0.6f, 0.62f);
                if (exists)
                {
                    var rec = SaveManager.Data.Record(park.id);
                    _infos[i].text = $"{park.pieces.Count} PIECES · {ParkEditorController.ThemeName(park.Theme)}" + (rec.bestScore > 0 ? $" · BEST {rec.bestScore:N0}" : "");
                }
                else _infos[i].text = "START FROM A STARTER LAYOUT";
                _editLabels[i].text = exists ? "EDIT" : "BUILD";
                _skate[i].gameObject.SetActive(exists);
                _run[i].gameObject.SetActive(exists);
                _delete[i].gameObject.SetActive(exists);
                _deleteLabels[i].text = _confirmDelete == i ? "SURE?" : "DELETE";
            }
        }

        private static void Edit(int slot)
        {
            string id = Id(slot);
            if (SaveManager.FindCustomPark(id) == null)
                SaveManager.SaveCustomPark(CustomPark.Starter(id, "MY PARK " + (slot + 1)));
            GameSession.EditPark = true;
            GameSession.Mode = RunMode.FreeSkate;
            SceneRouter.LoadPark(id, null);
        }

        private static void Play(int slot, RunMode mode)
        {
            GameSession.EditPark = false;
            GameSession.Mode = mode;
            SceneRouter.LoadPark(Id(slot), null);
        }

        private void Delete(int slot)
        {
            // Two taps: the first arms it ("SURE?"), the second deletes.
            if (_confirmDelete != slot)
            {
                _confirmDelete = slot;
                Refresh();
                return;
            }
            _confirmDelete = -1;
            SaveManager.DeleteCustomPark(Id(slot));
            Refresh();
        }
    }
}
