using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// CustomizationScene composition root: a turntable preview of the skater plus a slot-by-slot shop.
    /// Tapping an item previews it; the action button equips it, or buys it with Tape Tokens.
    /// </summary>
    public sealed class CustomizationView : MonoBehaviour
    {
        public ContentRegistry content;

        private static readonly CosmeticSlot[] Slots =
        {
            CosmeticSlot.Deck, CosmeticSlot.Wheels, CosmeticSlot.Grip, CosmeticSlot.Shirt, CosmeticSlot.Hat, CosmeticSlot.Palette,
        };

        private SkaterVisual _preview;
        private Transform _turntable;
        private RectTransform _grid;
        private Text _tokens;
        private Text _detail;
        private Button _action;
        private Text _actionLabel;
        private CosmeticSlot _slot = CosmeticSlot.Deck;
        private CosmeticDefinition _selected;
        private readonly List<Image> _tabs = new List<Image>();
        private CreateSkaterPanelView _createSkater;

        private void Start()
        {
            GameBootstrap.ApplyRuntimeSettings();
            Time.timeScale = 1f;
            SaveManager.Load();
            if (content == null) content = DefaultContent.CreateRegistry();
            PlaceholderMaterials.SetBase(content.baseLitMaterial);
            AudioManager.Ensure().PlayMusic();

            BuildStage();
            BuildUi();
            ShowSlot(CosmeticSlot.Deck);
        }

        // ------------------------------------------------------------------ 3D preview

        private void BuildStage()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            cam.fieldOfView = 40f;
            // The preview sits right of centre so the shop panel on the left doesn't cover it.
            cam.transform.position = new Vector3(-0.9f, 1.35f, 4.2f);
            cam.transform.LookAt(new Vector3(-0.9f, 0.85f, 0f));

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Key Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.6f);

            _turntable = new GameObject("Turntable").transform;
            _turntable.position = new Vector3(-1.6f, 0f, 0f);
            PrimitiveMeshes.CreateVisual("Plinth", PrimitiveType.Cylinder, _turntable, new Vector3(0f, -0.05f, 0f), new Vector3(1.6f, 0.05f, 1.6f), Palette.TapeYellow);

            var visualGo = new GameObject("Preview");
            visualGo.transform.SetParent(_turntable, false);
            _preview = visualGo.AddComponent<SkaterVisual>();
            _preview.Build();
            _preview.ApplyLook(SaveManager.Data.look);
            _preview.ApplyLoadout(CosmeticsService.CurrentLoadout(content));
        }

        private void Update()
        {
            if (_turntable != null) _turntable.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("Customization", 0, transform);
            var safe = UIFactory.SafeArea(canvas.transform);

            var panel = UIFactory.Panel("ShopPanel", safe, Theme.InkSoft, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(1200f, 1000f));

            var title = UIFactory.TapeLabel("Title", panel.transform, "CUSTOMIZE", 56, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, -10f), new Vector2(400f, 90f));

            _tokens = UIFactory.Label("Tokens", safe, "", 48, Theme.Tape, TextAnchor.UpperRight);
            UIFactory.Place(_tokens.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(700f, 60f));

            // Slot tabs
            var tabs = UIFactory.Rect("Tabs", panel.transform);
            UIFactory.Place(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1150f, 80f));
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = false;
            foreach (var slot in Slots)
            {
                var s = slot;
                var b = UIFactory.MakeButton("Tab_" + s, tabs, s.ToString().ToUpperInvariant(), new Vector2(182f, 70f), Theme.Cream, () => ShowSlot(s), 28);
                _tabs.Add(b.GetComponent<Image>());
            }

            // Item grid
            _grid = UIFactory.Rect("Grid", panel.transform);
            UIFactory.Place(_grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1150f, 560f));
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(360f, 170f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            // Detail + action
            _detail = UIFactory.Label("Detail", panel.transform, "", 36, Theme.Cream, TextAnchor.MiddleLeft, false);
            UIFactory.Place(_detail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 150f), new Vector2(700f, 60f));
            _action = UIFactory.MakeButton("Action", panel.transform, "", new Vector2(400f, 100f), Theme.Tape, OnAction, 42);
            UIFactory.Place((RectTransform)_action.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(400f, 100f));
            _actionLabel = _action.GetComponentInChildren<Text>();

            var back = UIFactory.MakeButton("Back", panel.transform, "BACK", new Vector2(260f, 90f), Theme.Coral, GoBack, 40);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(260f, 90f));

            // Create-a-Skater + board maker open over the shop panel; the turntable keeps previewing.
            var skaterRoot = UIFactory.Rect("CreateSkater", safe);
            UIFactory.Stretch(skaterRoot);
            _createSkater = skaterRoot.gameObject.AddComponent<CreateSkaterPanelView>();
            _createSkater.Build(skaterRoot, _preview, () =>
            {
                _preview.ApplyLook(SaveManager.Data.look);
                _preview.ApplyLoadout(CosmeticsService.CurrentLoadout(content));
            });
            skaterRoot.gameObject.SetActive(false);
            var open = UIFactory.MakeButton("CreateSkater", safe, "CREATE-A-SKATER", new Vector2(520f, 96f), Theme.Teal, _createSkater.Open, 38);
            UIFactory.Place((RectTransform)open.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(520f, 96f));
            open.transform.SetSiblingIndex(skaterRoot.GetSiblingIndex()); // stays under the panel when it is open

            var note = UIFactory.Label("Note", safe, "Tokens are earned by skating only. Optional looks-only packs in the SHOP. No random drops.", 26, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerRight, false);
            UIFactory.Place(note.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(1000f, 40f));
        }

        private void ShowSlot(CosmeticSlot slot)
        {
            _slot = slot;
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].color = Slots[i] == slot ? Theme.Tape : Theme.Cream;
            foreach (Transform child in _grid)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            _selected = null;
            foreach (var c in CosmeticsService.InSlot(content, slot))
            {
                AddCard(c);
                if (Equipped(c)) _selected = c;
            }
            Refresh();
        }

        private void AddCard(CosmeticDefinition c)
        {
            var card = UIFactory.MakeButton("Card_" + c.id, _grid, "", new Vector2(360f, 170f), Theme.Cream, () => Select(c), 28);
            Destroy(card.transform.Find("Label").gameObject); // cards lay out their own text

            var swatch = UIFactory.Rect("Swatch", card.transform);
            UIFactory.Place(swatch, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(100f, 140f));
            if (c.slot == CosmeticSlot.Deck && c.pattern != DeckPattern.Solid)
            {
                var raw = swatch.gameObject.AddComponent<RawImage>();
                raw.texture = DeckTextures.Get(c.pattern, c.primary, c.secondary);
                raw.raycastTarget = false;
            }
            else
            {
                var img = swatch.gameObject.AddComponent<Image>();
                img.color = c.hidesItem ? new Color(0f, 0f, 0f, 0.15f) : c.primary;
                img.raycastTarget = false;
            }

            var name = UIFactory.Label("Name", card.transform, c.displayName.ToUpperInvariant(), 28, Theme.Ink, TextAnchor.UpperLeft, false);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -18f), new Vector2(220f, 70f));
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            var status = UIFactory.Label("Status", card.transform, StatusText(c), 26, Theme.Ink, TextAnchor.LowerLeft, false);
            UIFactory.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(130f, 16f), new Vector2(220f, 40f));
        }

        // The loadout falls back to the free item per slot, so "equipped" is judged against it rather than the raw save entry.
        private List<string> _featured;
        private List<string> Featured => _featured ?? (_featured = CosmeticsService.Featured(content));

        private bool Equipped(CosmeticDefinition c) => CosmeticsService.CurrentLoadout(content)[c.slot] == c;

        private string StatusText(CosmeticDefinition c)
        {
            if (Equipped(c)) return "■ EQUIPPED";
            if (CosmeticsService.IsOwned(c)) return "OWNED";
            if (c.IsPackItem) return "IN SHOP PACK";
            return $"{CosmeticsService.PriceToday(c, Featured)} TOKENS";
        }

        private void Select(CosmeticDefinition c)
        {
            _selected = c;
            // Try-on: preview the selection over the saved loadout.
            var loadout = CosmeticsService.CurrentLoadout(content).Clone();
            loadout[c.slot] = c;
            _preview.ApplyLoadout(loadout);
            Refresh();
        }

        private void OnAction()
        {
            if (_selected == null) return;
            if (CosmeticsService.IsOwned(_selected))
            {
                CosmeticsService.Equip(_selected);
            }
            else
            {
                int price = CosmeticsService.PriceToday(_selected, Featured);
                var result = CosmeticsService.Buy(_selected, price);
                if (result == PurchaseResult.Ok)
                {
                    CosmeticsService.Equip(_selected);
                    RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.Coin);
                }
                else
                {
                    var pack = Shop.FindPack(_selected.packId);
                    _detail.text = result == PurchaseResult.NotEnoughTokens
                        ? $"Need {price - SaveManager.Data.tapeTokens} more Tape Tokens. Skate to earn them."
                        : result == PurchaseResult.PackOnly && pack != null
                            ? $"Comes in the {pack.Name} in the SHOP (main menu)."
                            : "Can't buy that right now.";
                    return;
                }
            }
            _preview.ApplyLoadout(CosmeticsService.CurrentLoadout(content));
            ShowSlot(_slot);
        }

        private void Refresh()
        {
            _tokens.text = $"TAPE TOKENS  {SaveManager.Data.tapeTokens}";
            if (_selected == null)
            {
                _detail.text = "";
                _action.interactable = false;
                _actionLabel.text = "—";
                return;
            }
            _detail.text = _selected.displayName;
            bool owned = CosmeticsService.IsOwned(_selected);
            bool equipped = Equipped(_selected);
            _action.interactable = !equipped;
            _actionLabel.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : _selected.IsPackItem ? "IN A PACK" : $"BUY · {CosmeticsService.PriceToday(_selected, Featured)}";
        }

        private static void GoBack()
        {
            string scene = Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu) ? SceneNames.MainMenu : SceneNames.Boot;
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
        }
    }
}
