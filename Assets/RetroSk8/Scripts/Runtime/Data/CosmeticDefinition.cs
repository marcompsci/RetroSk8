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

    /// <summary>A purely cosmetic unlock bought with Tape Tokens. No stats, no randomness, no real-money price.</summary>
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

        public bool IsFree => price <= 0;

        public static CosmeticDefinition CreateRuntime(string id, string name, CosmeticSlot slot, int price, Color a, Color b,
            DeckPattern pattern = DeckPattern.Solid, bool hides = false)
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
            return c;
        }
    }
}
