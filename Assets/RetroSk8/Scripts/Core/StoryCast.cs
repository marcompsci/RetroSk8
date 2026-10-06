using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// How each story character looks, so the comic panels can show them as real 3D skaters. Crew members reuse
    /// their Crew roster looks; The Gloss wear matching outfits (they're a sponsored crew). "YOU" is the player's own
    /// Create-a-Skater look. All characters are original and fictional.
    /// </summary>
    public static class StoryCast
    {
        public const string You = "YOU";

        /// <summary>The look for a speaker, or null when there's nobody to show (narration, unknown names).</summary>
        public static SkaterLook LookFor(string speaker, SkaterLook player)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            string name = speaker.Trim().ToUpperInvariant();
            if (name == You) return player != null ? player.Clone() : new SkaterLook();

            foreach (var m in CrewRoster.Members)
                if (FirstName(m.Name) == name)
                    return Crew(m);

            switch (name)
            {
                // The Gloss: white jerseys with a coral trim, white cruisers. Each member keeps their own face and hair.
                case "SHEEN": return Gloss(skin: 2, hair: (int)HairStyle.Long, hairColor: 3);
                case "GLINT": return Gloss(skin: 5, hair: (int)HairStyle.Mohawk, hairColor: 7);
                case "LUSTRE": return Gloss(skin: 1, hair: (int)HairStyle.Bun, hairColor: 4);
                case "VAL STERLING":
                {
                    var val = Gloss(skin: 6, hair: (int)HairStyle.Short, hairColor: 5);
                    val.shirtStyle = (int)ShirtStyle.Hoodie; // the boss wears the hoodie
                    val.shirtColor = 1;                      // ink
                    val.eyewear = (int)Eyewear.Shades;
                    return val;
                }
                // Phase 20: The Projectionists, the drive-in crew. Ink work jackets with tape-yellow trim.
                case "MARQUEE": return Projectionist(skin: 3, hair: (int)HairStyle.Twists, hairColor: 2, eyewear: (int)Eyewear.Round);
                case "REEL": return Projectionist(skin: 4, hair: (int)HairStyle.Afro, hairColor: 1, eyewear: (int)Eyewear.None);
                // Phase 22: The Rink Rats, the rink crew. Teal jerseys with cream trim, sweats and high-tops.
                case "FROST": return RinkRat(skin: 2, hair: (int)HairStyle.Short, hairColor: 6, eyewear: (int)Eyewear.Shades);
                case "SLAPSHOT": return RinkRat(skin: 5, hair: (int)HairStyle.Bun, hairColor: 1, eyewear: (int)Eyewear.None);
                // Phase 25: The Deckhands, the shipyard crew. Mustard flannels over cargo pants and chunky boots-style shoes.
                case "RIVET": return Deckhand(skin: 6, hair: (int)HairStyle.Mohawk, hairColor: 4, eyewear: (int)Eyewear.None);
                case "ANCHOR": return Deckhand(skin: 1, hair: (int)HairStyle.Long, hairColor: 5, eyewear: (int)Eyewear.Round);
                // Phase 26: The Originals, the crew who built the city's first spots. Violet tanks, shorts, old-school boards.
                case "REWIND": return Original(skin: 4, hair: (int)HairStyle.Short, hairColor: 5, eyewear: (int)Eyewear.Round);
                case "STATIC": return Original(skin: 3, hair: (int)HairStyle.Afro, hairColor: 0, eyewear: (int)Eyewear.Shades);
                default: return null;
            }
        }

        private static SkaterLook Original(int skin, int hair, int hairColor, int eyewear)
        {
            var look = new SkaterLook
            {
                skinTone = skin, hairStyle = hair, hairColor = hairColor, eyewear = eyewear,
                shirtStyle = (int)ShirtStyle.Tank, shirtColor = 7, shirtTrim = 3, // violet with tape-yellow trim
                bottomsStyle = (int)BottomsStyle.Shorts, bottomsColor = 6,
                shoeStyle = (int)ShoeStyle.LowTop, shoeColor = 2,
                deckShape = (int)DeckShape.OldSchool,
            };
            look.Sanitize();
            return look;
        }

        private static SkaterLook Deckhand(int skin, int hair, int hairColor, int eyewear)
        {
            var look = new SkaterLook
            {
                skinTone = skin, hairStyle = hair, hairColor = hairColor, eyewear = eyewear,
                shirtStyle = (int)ShirtStyle.Flannel, shirtColor = 9, shirtTrim = 1,
                bottomsStyle = (int)BottomsStyle.Cargo, bottomsColor = 11,
                shoeStyle = (int)ShoeStyle.Chunky, shoeColor = 1,
            };
            look.Sanitize();
            return look;
        }

        private static SkaterLook RinkRat(int skin, int hair, int hairColor, int eyewear)
        {
            var look = new SkaterLook
            {
                skinTone = skin, hairStyle = hair, hairColor = hairColor, eyewear = eyewear,
                shirtStyle = (int)ShirtStyle.Jersey, shirtColor = 5, shirtTrim = 2,
                bottomsStyle = (int)BottomsStyle.Sweats, bottomsColor = 1,
                shoeStyle = (int)ShoeStyle.HighTop, shoeColor = 2,
            };
            look.Sanitize();
            return look;
        }

        private static SkaterLook Projectionist(int skin, int hair, int hairColor, int eyewear)
        {
            var look = new SkaterLook
            {
                skinTone = skin, hairStyle = hair, hairColor = hairColor, eyewear = eyewear,
                shirtStyle = (int)ShirtStyle.LongSleeve, shirtColor = 1, shirtTrim = 3, // ink with tape-yellow trim
                bottomsStyle = (int)BottomsStyle.Cargo, bottomsColor = 1,
                shoeStyle = (int)ShoeStyle.Chunky, shoeColor = 3,
            };
            look.Sanitize();
            return look;
        }

        /// <summary>"Pilar \"Pier\" Ochoa" → "PILAR".</summary>
        public static string FirstName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            string n = fullName.Trim().ToUpperInvariant();
            int space = n.IndexOf(' ');
            return space > 0 ? n.Substring(0, space) : n;
        }

        private static SkaterLook Crew(CrewMember m)
        {
            var look = new SkaterLook
            {
                skinTone = m.SkinTone, hairStyle = m.HairStyle, hairColor = m.HairColor,
                // Each crew member gets their own colours (1-based palette choices; 0 would mean "shop gear").
                shirtStyle = (int)ShirtStyleFor(m.Style),
                shirtColor = 1 + Math.Abs(m.Id.GetHashCodeStable()) % LookPalette.Colors.Length,
                shirtTrim = 2,
                bottomsColor = 1,
            };
            look.Sanitize();
            return look;
        }

        private static ShirtStyle ShirtStyleFor(SkaterStyle style) =>
            style == SkaterStyle.Tech ? ShirtStyle.LongSleeve
            : style == SkaterStyle.Vert ? ShirtStyle.Hoodie
            : style == SkaterStyle.Flow ? ShirtStyle.Flannel
            : ShirtStyle.Tee;

        private static SkaterLook Gloss(int skin, int hair, int hairColor)
        {
            var look = new SkaterLook
            {
                skinTone = skin, hairStyle = hair, hairColor = hairColor,
                shirtStyle = (int)ShirtStyle.Jersey, shirtColor = 2, shirtTrim = 4, // cream with coral trim
                bottomsStyle = (int)BottomsStyle.Chinos, bottomsColor = 1,
                shoeStyle = (int)ShoeStyle.HighTop, shoeColor = 1,
            };
            look.Sanitize();
            return look;
        }

        /// <summary>A hash that is the same on every runtime (string.GetHashCode isn't).</summary>
        private static int GetHashCodeStable(this string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h == int.MinValue ? 0 : h;
            }
        }
    }
}
