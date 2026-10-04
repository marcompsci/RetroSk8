using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Main menu → SHOP. FEATURED TODAY: four token items that rotate daily, one at 25% off. PACKS: three optional
    /// App Store cosmetic packs (looks only), with RESTORE PURCHASES. Everything bought shows up in SKATER.
    /// </summary>
    public sealed class ShopPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Text _tokens;
        private Text _status;
        private RectTransform _featuredRow;
        private readonly List<(CosmeticPack pack, Button buy, Text label)> _packs = new List<(CosmeticPack, Button, Text)>();
        private Button _restore;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "SHOP", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(240f, 100f));
            _tokens = UIFactory.Label("Tokens", root, "", 40, Theme.Tape, TextAnchor.MiddleRight);
            UIFactory.Place(_tokens.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -40f), new Vector2(800f, 70f));

            Header(root, "FEATURED TODAY  ·  TAPE TOKENS  ·  ONE DEAL A DAY AT 25% OFF", -140f);
            _featuredRow = Row(root, -190f, 230f);

            Header(root, "COSMETIC PACKS  ·  OPTIONAL  ·  LOOKS ONLY, NO STATS, NO TOKENS, NO RANDOM REWARDS", -450f);
            var packRow = Row(root, -500f, 360f);
            foreach (var p in Shop.Packs) _packs.Add(PackCard(packRow, p));

            _status = UIFactory.Label("Status", root, "", 28, Theme.Teal, TextAnchor.MiddleCenter, false);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1500f, 50f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            _restore = UIFactory.MakeButton("Restore", root, "RESTORE PURCHASES", new Vector2(480f, 90f), Theme.Cream, StoreService.Restore, 34);
            UIFactory.Place((RectTransform)_restore.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(480f, 90f));
            var fine = UIFactory.Label("Fine", root, StoreService.IsTestStore
                    ? "EDITOR TEST STORE: PACKS UNLOCK WITHOUT CHARGING. REAL PURCHASES HAPPEN IN THE IPHONE APP."
                    : "PACKS ARE ONE-TIME PURCHASES ON YOUR APPLE ID. TAPE TOKENS ARE ONLY EARNED BY SKATING.",
                22, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, false);
            UIFactory.Place(fine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 50f));

            StoreService.Message += OnMessage;
            StoreService.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            StoreService.Message -= OnMessage;
            StoreService.Changed -= Refresh;
        }

        private void OnEnable()
        {
            if (_content == null) return;
            StoreService.Init();
            _status.text = "";
            Refresh();
        }

        private void OnMessage(string text)
        {
            if (_status != null) _status.text = text;
        }

        private static void Header(RectTransform root, string text, float y)
        {
            var h = UIFactory.Label("Header", root, text, 30, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(h.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1900f, 44f));
        }

        private static RectTransform Row(RectTransform root, float y, float height)
        {
            var row = UIFactory.Rect("Row", root);
            UIFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1900f, height));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.UpperCenter;
            h.childControlWidth = h.childControlHeight = false;
            return row;
        }

        // ---------------------------------------------------------------- featured (tokens)

        private void BuildFeatured()
        {
            foreach (Transform child in _featuredRow) Destroy(child.gameObject);
            var featured = CosmeticsService.Featured(_content);
            for (int i = 0; i < featured.Count; i++)
            {
                var c = _content.cosmetics.Find(x => x != null && x.id == featured[i]);
                if (c != null) FeaturedCard(c, featured, i == 0);
            }
        }

        private void FeaturedCard(CosmeticDefinition c, List<string> featured, bool deal)
        {
            var card = UIFactory.Panel("Featured_" + c.id, _featuredRow, Theme.InkSoft);
            card.rectTransform.sizeDelta = new Vector2(440f, 230f);
            Swatch(card.transform, c, new Vector2(20f, -20f), new Vector2(110f, 140f));
            var name = UIFactory.Label("Name", card.transform, c.displayName.ToUpperInvariant(), 30, Theme.Cream, TextAnchor.UpperLeft, false);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -20f), new Vector2(270f, 80f));
            var slot = UIFactory.Label("Slot", card.transform, SlotName(c.slot), 22, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft, false);
            UIFactory.Place(slot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -100f), new Vector2(270f, 34f));
            if (deal)
            {
                var tag = UIFactory.TapeLabel("Deal", card.transform, "DEAL -25%", 24, Theme.Coral, 4f);
                UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, 12f), new Vector2(170f, 40f));
            }

            bool owned = CosmeticsService.IsOwned(c);
            int price = CosmeticsService.PriceToday(c, featured);
            string label = owned ? "OWNED" : deal ? $"BUY · {price}  (WAS {c.price})" : $"BUY · {price}";
            var buy = UIFactory.MakeButton("Buy", card.transform, label, new Vector2(400f, 64f), owned ? Theme.Cream : Theme.Tape, () => BuyToken(c, price), 26);
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 64f));
            buy.interactable = !owned;
        }

        private void BuyToken(CosmeticDefinition c, int price)
        {
            var result = CosmeticsService.Buy(c, price);
            if (result == PurchaseResult.Ok)
            {
                CosmeticsService.Equip(c);
                RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.Coin);
                _status.text = $"{c.displayName.ToUpperInvariant()} IS YOURS (AND EQUIPPED).";
            }
            else if (result == PurchaseResult.NotEnoughTokens)
                _status.text = $"NEED {price - SaveManager.Data.tapeTokens} MORE TAPE TOKENS. SKATE TO EARN THEM.";
            Refresh();
        }

        // ---------------------------------------------------------------- packs (App Store)

        private (CosmeticPack, Button, Text) PackCard(RectTransform row, CosmeticPack pack)
        {
            var card = UIFactory.Panel("Pack_" + pack.Id, row, Theme.InkSoft);
            card.rectTransform.sizeDelta = new Vector2(600f, 360f);
            var name = UIFactory.Label("Name", card.transform, pack.Name, 34, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(560f, 50f));
            var blurb = UIFactory.Label("Blurb", card.transform, pack.Blurb, 24, Theme.Cream, TextAnchor.UpperLeft, false);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(blurb.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -70f), new Vector2(560f, 40f));

            // What's inside: one swatch per item, then the list.
            var items = new List<string>();
            float x = 24f;
            foreach (var id in pack.ItemIds)
            {
                var c = _content.cosmetics.Find(i => i != null && i.id == id);
                if (c == null) continue;
                Swatch(card.transform, c, new Vector2(x, -118f), new Vector2(76f, 96f));
                x += 90f;
                items.Add(c.displayName.ToUpperInvariant());
            }
            var list = UIFactory.Label("Items", card.transform, string.Join(" · ", items.ToArray()), 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperLeft, false);
            list.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(list.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -224f), new Vector2(560f, 50f));

            var buy = UIFactory.MakeButton("BuyPack", card.transform, "", new Vector2(552f, 70f), Theme.Tape, () => StoreService.Buy(pack), 32);
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(552f, 70f));
            return (pack, buy, buy.GetComponentInChildren<Text>());
        }

        private static void Swatch(Transform parent, CosmeticDefinition c, Vector2 pos, Vector2 size)
        {
            var rt = UIFactory.Rect("Swatch", parent);
            UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            if (c.slot == CosmeticSlot.Deck && c.pattern != DeckPattern.Solid)
            {
                var raw = rt.gameObject.AddComponent<RawImage>();
                raw.texture = DeckTextures.Get(c.pattern, c.primary, c.secondary);
                raw.raycastTarget = false;
            }
            else
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = c.primary;
                img.raycastTarget = false;
                if (c.slot != CosmeticSlot.Wheels)
                {
                    var accent = UIFactory.Panel("Accent", rt, c.secondary);
                    UIFactory.Place(accent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x * 0.6f, size.y * 0.18f));
                }
                else img.sprite = UIFactory.Circle;
            }
        }

        private static string SlotName(CosmeticSlot slot) =>
            slot == CosmeticSlot.Deck ? "DECK" : slot == CosmeticSlot.Wheels ? "WHEELS" : slot == CosmeticSlot.Grip ? "GRIP TAPE"
            : slot == CosmeticSlot.Shirt ? "SHIRT" : slot == CosmeticSlot.Hat ? "CAP" : "PANTS";

        private void Refresh()
        {
            if (_tokens == null) return;
            _tokens.text = $"TAPE TOKENS  {SaveManager.Data.tapeTokens}";
            BuildFeatured();
            foreach (var (pack, buy, label) in _packs)
            {
                bool owned = StoreService.Owns(pack);
                label.text = owned ? "OWNED · IN SKATER" : StoreService.Busy ? "..." : "BUY · " + StoreService.Price(pack);
                buy.interactable = !owned && !StoreService.Busy;
                buy.GetComponent<Image>().color = owned ? Theme.Cream : Theme.Tape;
            }
            _restore.interactable = !StoreService.Busy;
        }

        private float _nextPriceCheck;

        private void Update()
        {
            // Local prices arrive from the App Store a moment after opening; pick them up.
            if (Time.unscaledTime < _nextPriceCheck) return;
            _nextPriceCheck = Time.unscaledTime + 1f;
            foreach (var (pack, buy, label) in _packs)
                if (!StoreService.Owns(pack) && !StoreService.Busy) label.text = "BUY · " + StoreService.Price(pack);
        }
    }
}
