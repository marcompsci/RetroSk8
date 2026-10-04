using UnityEngine;

namespace RetroSk8.Data
{
    public enum CosmeticSlot
    {
        Deck = 0,
        Wheels = 1,
        Grip = 2,
        Shirt = 3,
        Hat = 4,
        Palette = 5,
    }

    /// <summary>Procedural deck graphics (original, generated at runtime; no external art).</summary>
    public enum DeckPattern
    {
        Solid = 0,
        Stripes = 1,
        Checker = 2,
        Split = 3,
        Chevron = 4,
        Dots = 5,
        Bands = 6,
    }

    /// <summary>
    /// A purely cosmetic unlock. Most are bought with Tape Tokens (earned only by skating); a few come in an
    /// optional App Store cosmetic pack (<see cref="packId"/>). No stats, no randomness, no pay-to-win.
    /// </summary>
    [CreateAssetMenu(menuName = "Retro Sk8/Cosmetic", fileName = "Cosmetic_")]
    public sealed class CosmeticDefinition : ScriptableObject
    {
        public string id = "cosmetic_id";
        public string displayName = "Cosmetic";
        public CosmeticSlot slot;
        [Tooltip("Tape Tokens. 0 = owned from the start.")]
        public int price;
        public Color primary = Color.white;
        public Color secondary = Color.black;
        public DeckPattern pattern = DeckPattern.Solid;
        [Tooltip("Hat slot only: hides the cap.")]
        public bool hidesItem;
        [Tooltip("Set for items that come in an App Store cosmetic pack (RetroSk8.Core.Shop.Packs). Empty for token items.")]
        public string packId = "";

        public bool IsPackItem => !string.IsNullOrEmpty(packId);
        public bool IsFree => price <= 0 && !IsPackItem;

        public static CosmeticDefinition CreateRuntime(string id, string name, CosmeticSlot slot, int price, Color a, Color b,
            DeckPattern pattern = DeckPattern.Solid, bool hides = false, string pack = null)
        {
            var c = CreateInstance<CosmeticDefinition>();
            c.name = id;
            c.id = id;
            c.displayName = name;
            c.slot = slot;
            c.price = price;
            c.primary = a;
            c.secondary = b;
            c.pattern = pattern;
            c.hidesItem = hides;
            c.packId = pack ?? "";
            return c;
        }
    }
}
