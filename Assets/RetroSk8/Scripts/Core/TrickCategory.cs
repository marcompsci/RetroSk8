namespace RetroSk8.Core
{
    public enum TrickCategory
    {
        BoardFlip = 0,
        Grab = 1,
        Spin = 2,
        Grind = 3,
        Manual = 4,
        Special = 5,
        Gap = 6,
        /// <summary>Stalls on quarter-pipe coping (Phase 7).</summary>
        Lip = 7,
        /// <summary>Wallrides, wallplants and wallies (Phase 7).</summary>
        Wall = 8,
        /// <summary>Reverts: spinning back to fakie out of a ramp landing, which keeps the combo alive (Phase 7).</summary>
        Revert = 9,
        /// <summary>Ground pops with a foot: no-comply and boneless (Phase 10).</summary>
        Pop = 10,
    }

    /// <summary>The kind of line segment a trick happened on. Changing element mid-combo counts as a Line Flow link.</summary>
    public enum LineElement
    {
        None = 0,
        Air = 1,
        RampAir = 2,
        Grind = 3,
        Manual = 4,
        Wall = 5,
        Lip = 6,
    }

    public enum SwipeDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }

    public enum TrickFamily
    {
        Flip = 0,   // swipe up
        Grab = 1,   // swipe down
        Shove = 2,  // swipe left / right
    }

    public static class TrickCategoryExtensions
    {
        public static LineElement ToLineElement(this TrickCategory category)
        {
            switch (category)
            {
                case TrickCategory.Grind: return LineElement.Grind;
                case TrickCategory.Manual: return LineElement.Manual;
                case TrickCategory.Revert: return LineElement.Manual; // a revert links the same way a manual does
                case TrickCategory.Wall: return LineElement.Wall;
                case TrickCategory.Lip: return LineElement.Lip;
                default: return LineElement.Air;
            }
        }
    }
}
