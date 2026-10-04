using System.Collections;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Procedural low-poly skater (original design: jointed capsule limbs, cap with a brim, chunky shoes) plus all
    /// of its poses. Knees and elbows bend for crouches, grabs reach down to the board, arms balance on grinds.
    /// Pure presentation: never touches physics. Swap for a rigged model later behind the same method surface.
    /// </summary>
    public sealed class SkaterVisual : MonoBehaviour
    {
        private const float ThighLength = 0.42f;
        private const float ShinLength = 0.42f;
        private const float HipHeight = 1.02f;

        private Transform _pivot;     // whole-body pose (lean, bail tumble)
        private Transform _body;      // side-on stance
        private Transform _hips;
        private Transform _board;
        private Transform _thighL, _thighR, _shinL, _shinR;
        private Transform _upperArmL, _upperArmR, _foreArmL, _foreArmR;
        private Vector3 _boardBasePos;
        private Coroutine _bailRoutine;
        private float _crouch;
        private float _reach;        // 0..1 grab reach toward the board
        private float _armsOut;      // 0..1 balance arms
        // Phase 16 motion layer (push, squash, pop, carve), written by SkaterMotionDriver.
        private float _pushSwing, _pushReach, _pushBob, _squash, _airTuck, _flick;
        private bool _trickActive, _balanceActive;
        public bool MotionFree => _pivot != null && !_trickActive && !_balanceActive && _bailRoutine == null;

        // Renderers recoloured by cosmetics.
        private MeshRenderer _deck, _grip, _cap, _brim, _stripe;
        private readonly List<MeshRenderer> _wheels = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _shirt = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _pants = new List<MeshRenderer>();
        // Create-a-Skater parts.
        private readonly List<MeshRenderer> _skin = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _shoes = new List<MeshRenderer>();
        private readonly List<Transform> _torso = new List<Transform>();
        private readonly List<Transform> _upperArms = new List<Transform>();
        // Phase 13 outfit parts.
        private readonly List<MeshRenderer> _upperSleeves = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _forearms = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _thighs = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _shins = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _soles = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _trucks = new List<MeshRenderer>();
        private readonly List<GameObject> _outfitParts = new List<GameObject>();
        private Transform _shinJointL, _shinJointR, _thighJointL, _thighJointR;
        private Color _gearShirt, _gearTrim = Palette.Cream, _gearPants, _gearWheels, _gearGrip = Palette.Ink;
        private readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();
        private Transform _hair;
        private Transform _eyewear;
        private bool _hatHiddenByGear;
        private SkaterLook _look;
        private CosmeticDefinition _deckItem;

        public Color shirtColor = Palette.Coral;
        public Color pantsColor = Palette.Ink;
        public Color deckColor = Palette.TapeYellow;
        public Color wheelColor = Palette.Cream;
        public Color hatColor = Palette.Teal;
        public Color skinColor = new Color(0.55f, 0.38f, 0.28f);
        public Color shoeColor = new Color(0.92f, 0.9f, 0.86f);

        public Transform Board => _board;

        public void Build()
        {
            _pivot = new GameObject("Pose").transform;
            _pivot.SetParent(transform, false);

            _body = new GameObject("Body").transform;
            _body.SetParent(_pivot, false);
            _body.localRotation = Quaternion.Euler(0f, 70f, 0f); // side-on stance

            _hips = Joint("Hips", _body, new Vector3(0f, HipHeight, 0f));
            _pants.Add(Shape(Part("Pelvis", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.14f, 0.24f), pantsColor), SkaterShapes.Hips));

            // Torso: tapered by stacking a wide chest over a narrower waist.
            _shirt.Add(Shape(Part("Waist", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.17f, 0.22f), shirtColor), SkaterShapes.Hips));
            _shirt.Add(Shape(Part("Chest", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.42f, 0.01f), new Vector3(0.44f, 0.2f, 0.26f), shirtColor), SkaterShapes.Chest));
            _stripe = Part("ShirtStripe", PrimitiveType.Cylinder, _hips, new Vector3(0f, 0.36f, 0.01f), new Vector3(0.43f, 0.025f, 0.27f), Palette.Cream);
            _torso.Add(_shirt[0].transform);
            _torso.Add(_shirt[1].transform);
            _torso.Add(_stripe.transform);

            _skin.Add(Part("Neck", PrimitiveType.Cylinder, _hips, new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skinColor));
            // Phase 17: an egg-shaped head (the sphere's unit box is half the capsule's, so halve the height) with a face.
            _skin.Add(Shape(Part("Head", PrimitiveType.Sphere, _hips, new Vector3(0f, 0.76f, 0.01f), new Vector3(0.26f, 0.15f, 0.27f), skinColor), SkaterShapes.Head));
            BuildFace();
            _cap = Part("Cap", PrimitiveType.Sphere, _hips, new Vector3(0f, 0.85f, 0f), new Vector3(0.28f, 0.17f, 0.29f), hatColor);
            _brim = Part("CapBrim", PrimitiveType.Cube, _hips, new Vector3(0f, 0.83f, 0.16f), new Vector3(0.24f, 0.025f, 0.14f), hatColor);

            BuildArm(-1f, out _upperArmL, out _foreArmL);
            BuildArm(1f, out _upperArmR, out _foreArmR);
            BuildLeg(-1f, out _thighL, out _shinL);
            BuildLeg(1f, out _thighR, out _shinR);

            _board = new GameObject("Board").transform;
            _board.SetParent(_pivot, false);
            _boardBasePos = new Vector3(0f, 0.12f, 0f);
            _board.localPosition = _boardBasePos;
            _deck = Part("Deck", PrimitiveType.Cube, _board, Vector3.zero, new Vector3(0.24f, 0.035f, 0.84f), deckColor);
            _grip = Part("Grip", PrimitiveType.Cube, _board, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.005f, 0.8f), Palette.Ink);
            // Kicked-up nose and tail.
            var nose = Part("Nose", PrimitiveType.Cube, _board, new Vector3(0f, 0.035f, 0.47f), new Vector3(0.22f, 0.03f, 0.14f), deckColor);
            nose.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            var tail = Part("Tail", PrimitiveType.Cube, _board, new Vector3(0f, 0.035f, -0.47f), new Vector3(0.22f, 0.03f, 0.14f), deckColor);
            tail.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            _deckEnds.Add(nose);
            _deckEnds.Add(tail);
            foreach (float z in new[] { -0.28f, 0.28f })
            {
                _trucks.Add(Part("Truck", PrimitiveType.Cube, _board, new Vector3(0f, -0.04f, z), new Vector3(0.2f, 0.03f, 0.04f), Palette.Metal));
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    var w = Part("Wheel", PrimitiveType.Cylinder, _board, new Vector3(x, -0.06f, z), new Vector3(0.07f, 0.02f, 0.07f), wheelColor);
                    w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    _wheels.Add(w);
                }
            }
            _gearShirt = shirtColor;
            _gearPants = pantsColor;
            _gearWheels = wheelColor;
            ApplyLimbs();
        }

        private readonly List<MeshRenderer> _deckEnds = new List<MeshRenderer>();

        private void BuildArm(float side, out Transform upper, out Transform fore)
        {
            upper = Joint(side < 0 ? "ShoulderL" : "ShoulderR", _hips, new Vector3(0.25f * side, 0.52f, 0f));
            var upperArm = Shape(Part("UpperArm", PrimitiveType.Capsule, upper, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.15f, 0.1f), shirtColor), SkaterShapes.Limb);
            _upperSleeves.Add(upperArm);
            _upperArms.Add(upperArm.transform);
            fore = Joint("Elbow", upper, new Vector3(0f, -0.27f, 0f));
            _forearms.Add(Shape(Part("Forearm", PrimitiveType.Capsule, fore, new Vector3(0f, -0.12f, 0f), new Vector3(0.085f, 0.13f, 0.085f), skinColor), SkaterShapes.Limb));
            _skin.Add(Part("Hand", PrimitiveType.Sphere, fore, new Vector3(0f, -0.27f, 0f), new Vector3(0.09f, 0.1f, 0.09f), skinColor));
        }

        private void BuildLeg(float side, out Transform thigh, out Transform shin)
        {
            thigh = Joint(side < 0 ? "HipL" : "HipR", _hips, new Vector3(0.16f * side, 0f, 0f));
            var thighPart = Shape(Part("Thigh", PrimitiveType.Capsule, thigh, new Vector3(0f, -ThighLength * 0.5f, 0f), new Vector3(0.15f, ThighLength * 0.55f, 0.15f), pantsColor), SkaterShapes.Limb);
            _pants.Add(thighPart);
            _thighs.Add(thighPart);
            shin = Joint("Knee", thigh, new Vector3(0f, -ThighLength, 0f));
            var shinPart = Shape(Part("Shin", PrimitiveType.Capsule, shin, new Vector3(0f, -ShinLength * 0.5f, 0f), new Vector3(0.13f, ShinLength * 0.55f, 0.13f), pantsColor), SkaterShapes.Calf);
            _shins.Add(shinPart);
            _shoes.Add(Part("Shoe", PrimitiveType.Cube, shin, new Vector3(0f, -ShinLength - 0.03f, 0.03f), new Vector3(0.13f, 0.08f, 0.22f), shoeColor));
            _shoes.Add(Part("ToeCap", PrimitiveType.Sphere, shin, new Vector3(0f, -ShinLength - 0.035f, 0.14f), new Vector3(0.13f, 0.085f, 0.11f), shoeColor)); // rounded toe (Phase 17)
            _soles.Add(Part("Sole", PrimitiveType.Cube, shin, new Vector3(0f, -ShinLength - 0.075f, 0.04f), new Vector3(0.135f, 0.02f, 0.265f), Palette.Cream));
            if (side < 0) { _thighJointL = thigh; _shinJointL = shin; } else { _thighJointR = thigh; _shinJointR = shin; }
        }

        /// <summary>Swaps a part's primitive mesh for a shaped one (same unit box, so its scale still fits). Phase 17.</summary>
        private static MeshRenderer Shape(MeshRenderer part, SkaterShapes.Profile profile)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = SkaterMeshes.Get(profile);
            return part;
        }

        /// <summary>Eyes, brows, nose and mouth on the front of the head (all original, simple shapes).</summary>
        private void BuildFace()
        {
            var dark = new Color(0.08f, 0.07f, 0.08f);
            foreach (float x in new[] { -0.055f, 0.055f })
            {
                Part("Eye", PrimitiveType.Sphere, _hips, new Vector3(x, 0.785f, 0.147f), new Vector3(0.034f, 0.04f, 0.02f), dark);
                var brow = Part("Brow", PrimitiveType.Cube, _hips, new Vector3(x, 0.825f, 0.14f), new Vector3(0.06f, 0.012f, 0.012f), dark);
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, x < 0f ? -8f : 8f);
                _brows.Add(brow.transform);
            }
            _skin.Add(Part("Nose", PrimitiveType.Sphere, _hips, new Vector3(0f, 0.755f, 0.145f), new Vector3(0.035f, 0.045f, 0.035f), skinColor));
            Part("Mouth", PrimitiveType.Cube, _hips, new Vector3(0f, 0.705f, 0.137f), new Vector3(0.06f, 0.012f, 0.01f), dark);
        }

        private readonly List<Transform> _brows = new List<Transform>();

        private static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static MeshRenderer Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color) =>
            PrimitiveMeshes.CreateVisual(name, type, parent, pos, scale, color).GetComponent<MeshRenderer>();

        /// <summary>
        /// Two-bone legs: the hips drop with the crouch while knees bend so the feet stay planted on the deck.
        /// Arms swing out for balance and reach toward the board for grabs.
        /// </summary>
        private void ApplyLimbs()
        {
            if (_hips == null) return;
            float crouch = Mathf.Clamp01(Mathf.Max(Mathf.Max(_crouch, _squash * 0.6f), _airTuck * 0.35f) + 0.12f * _pushBob * _pushReach);
            float drop = 0.3f * crouch;
            _hips.localPosition = new Vector3(0f, HipHeight - drop, 0f);
            float lean = 18f * crouch + 25f * _reach;
            _hips.localRotation = Quaternion.Euler(lean, 0f, 0f); // lean forward

            // Solve the knee angle for the new hip height (law of cosines, feet fixed under the hips).
            float reachLen = Mathf.Clamp(HipHeight - drop - 0.21f, 0.3f, ThighLength + ShinLength - 0.001f); // ankle sits just above the deck
            float cosKnee = (ThighLength * ThighLength + ShinLength * ShinLength - reachLen * reachLen) / (2f * ThighLength * ShinLength);
            float knee = 180f - Mathf.Acos(Mathf.Clamp(cosKnee, -1f, 1f)) * Mathf.Rad2Deg;
            // Thigh forward by half the knee angle (minus the hip lean) and shin back by the full knee angle,
            // so the foot ends up straight under the hip joint, on the deck.
            // The front foot (left) can flick a flip; the back foot (right) leaves the board to push and
            // straightens to reach the ground beside it.
            // (Two calls, not a loop over a new array: this runs every frame and must not allocate. Phase 17.)
            PoseLeg(_thighL, _shinL, -1f, knee, lean, crouch);
            PoseLeg(_thighR, _shinR, 1f, knee, lean, crouch);

            float armOut = Mathf.Lerp(12f, 70f, _armsOut);
            float armSwing = 0.6f * _pushSwing * _pushReach; // arms counter-swing the kick
            _upperArmL.localRotation = Quaternion.Euler(-20f * crouch + armSwing, 0f, -armOut);
            _upperArmR.localRotation = Quaternion.Euler(-20f * crouch - 30f * _reach - armSwing, 0f, armOut * (1f - _reach));
            _foreArmL.localRotation = Quaternion.Euler(-25f - 20f * crouch, 0f, 0f);
            _foreArmR.localRotation = Quaternion.Euler(-25f * (1f - _reach), 0f, 0f);
        }

        private void PoseLeg(Transform thigh, Transform shin, float side, float knee, float lean, float crouch)
        {
            bool back = side > 0f;
            float k = back ? knee * (1f - 0.85f * _pushReach) : knee;
            float roll = 4f * side * (1f - crouch) + (back ? _pushSwing : -_flick);
            thigh.localRotation = Quaternion.Euler(-k * 0.5f - lean, 0f, roll);
            shin.localRotation = Quaternion.Euler(k, 0f, 0f);
        }

        // ---------------------------------------------------------------- replay support

        /// <summary>Reads the three posed joints so a replay can reproduce tricks, leans and bails.</summary>
        public void CapturePose(out Quaternion pose, out Quaternion body, out Vector3 boardPosition, out Quaternion board)
        {
            pose = _pivot != null ? _pivot.localRotation : Quaternion.identity;
            body = _body != null ? _body.localRotation : Quaternion.identity;
            boardPosition = _board != null ? _board.localPosition : Vector3.zero;
            board = _board != null ? _board.localRotation : Quaternion.identity;
        }

        public void ApplyPose(Quaternion pose, Quaternion body, Vector3 boardPosition, Quaternion board)
        {
            if (_pivot == null) return;
            _pivot.localRotation = pose;
            _body.localRotation = body;
            _board.localPosition = boardPosition;
            _board.localRotation = board;
            // Ghosts don't record limbs: infer a crouch from how high the board has been lifted.
            _crouch = Mathf.Clamp01((boardPosition.y - _boardBasePos.y) / 0.25f);
            ApplyLimbs();
        }

        /// <summary>Turns this skater into a glowing, shadowless ghost (best-run playback).</summary>
        public void MakeGhost(Color color)
        {
            var mat = PlaceholderMaterials.GetEmissive(color, 1.4f);
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Applies equipped cosmetics. Missing slots keep their current look.</summary>
        public void ApplyLoadout(CosmeticLoadout loadout)
        {
            if (loadout == null || _deck == null) return;

            var deck = loadout[CosmeticSlot.Deck];
            _deckItem = deck ?? _deckItem;
            if (deck != null)
            {
                // The deck's underside graphic shows on flips; the grip covers the top.
                _deck.sharedMaterial = deck.pattern == DeckPattern.Solid
                    ? PlaceholderMaterials.Get(deck.primary)
                    : PlaceholderMaterials.GetTextured(DeckTextures.Get(deck.pattern, deck.primary, deck.secondary));
                foreach (var e in _deckEnds) e.sharedMaterial = PlaceholderMaterials.Get(deck.primary);
            }
            var wheels = loadout[CosmeticSlot.Wheels];
            if (wheels != null) { _gearWheels = wheels.primary; foreach (var w in _wheels) w.sharedMaterial = PlaceholderMaterials.Get(wheels.primary); }
            var grip = loadout[CosmeticSlot.Grip];
            if (grip != null) { _gearGrip = grip.primary; _grip.sharedMaterial = PlaceholderMaterials.Get(grip.primary); }
            var shirt = loadout[CosmeticSlot.Shirt];
            if (shirt != null)
            {
                _gearShirt = shirt.primary;
                _gearTrim = shirt.secondary;
                foreach (var r in _shirt) r.sharedMaterial = PlaceholderMaterials.Get(shirt.primary);
                foreach (var r in _upperSleeves) r.sharedMaterial = PlaceholderMaterials.Get(shirt.primary);
                _stripe.sharedMaterial = PlaceholderMaterials.Get(shirt.secondary);
            }
            var hat = loadout[CosmeticSlot.Hat];
            if (hat != null)
            {
                _hatHiddenByGear = hat.hidesItem;
                _cap.sharedMaterial = PlaceholderMaterials.Get(hat.primary);
                _brim.sharedMaterial = PlaceholderMaterials.Get(hat.primary);
            }
            var palette = loadout[CosmeticSlot.Palette];
            if (palette != null)
            {
                _gearPants = palette.primary;
                foreach (var p in _pants) p.sharedMaterial = PlaceholderMaterials.Get(palette.primary);
                foreach (var p in _shins) p.sharedMaterial = PlaceholderMaterials.Get(palette.primary);
            }
            ApplyLook(_look);
        }

        // ================================================================ Create-a-Skater

        private static Color C(Rgb c) => new Color(c.R, c.G, c.B);

        /// <summary>Applies body, hair, eyewear, shoes and the custom board graphic. Null keeps the default look.</summary>
        public void ApplyLook(SkaterLook look)
        {
            _look = look;
            if (_deck == null) return;
            if (look == null) { RefreshCap(false); return; }
            look.Sanitize();

            var skin = PlaceholderMaterials.Get(C(LookPalette.SkinTones[look.skinTone]));
            foreach (var r in _skin) r.sharedMaterial = skin;
            var shoes = PlaceholderMaterials.Get(C(LookPalette.Colors[look.shoeColor]));
            foreach (var r in _shoes) r.sharedMaterial = shoes;
            ApplyOutfit(look, skin);

            float torso = look.build == (int)BodyBuild.Slim ? 0.88f : look.build == (int)BodyBuild.Broad ? 1.14f : 1f;
            float arms = look.build == (int)BodyBuild.Slim ? 0.88f : look.build == (int)BodyBuild.Broad ? 1.18f : 1f;
            foreach (var t in _torso) ScaleXZ(t, torso);
            foreach (var t in _upperArms) ScaleXZ(t, arms);

            bool hairHidesCap = BuildHair((HairStyle)look.hairStyle, C(LookPalette.HairColors[look.hairColor]));
            BuildEyewear((Eyewear)look.eyewear);
            RefreshCap(hairHidesCap);

            if (look.customBoard)
            {
                var art = look.board;
                _deck.sharedMaterial = PlaceholderMaterials.GetTextured(DeckTextures.Get(art));
                var ends = PlaceholderMaterials.Get(C(LookPalette.Colors[art.primary]));
                foreach (var e in _deckEnds) e.sharedMaterial = ends;
            }
            else if (_deckItem != null)
            {
                _deck.sharedMaterial = _deckItem.pattern == DeckPattern.Solid
                    ? PlaceholderMaterials.Get(_deckItem.primary)
                    : PlaceholderMaterials.GetTextured(DeckTextures.Get(_deckItem.pattern, _deckItem.primary, _deckItem.secondary));
                foreach (var e in _deckEnds) e.sharedMaterial = PlaceholderMaterials.Get(_deckItem.primary);
            }
        }

        // ================================================================ Phase 13: clothes, shoes, board parts

        /// <summary>
        /// Shirt cut and colours, bottoms, shoes and socks, deck shape, wheels, trucks and grip. Colour choices left on
        /// "shop" fall back to the equipped cosmetics, so the shop gear still shows unless the player picks their own.
        /// </summary>
        private void ApplyOutfit(SkaterLook look, Material skin)
        {
            foreach (var go in _outfitParts) if (go != null) Destroy(go);
            _outfitParts.Clear();

            Color skinC = C(LookPalette.SkinTones[look.skinTone]);
            Color shirt = SkaterLook.Pick(look.shirtColor, out var sc) ? C(sc) : _gearShirt;
            Color trim = SkaterLook.Pick(look.shirtTrim, out var tc) ? C(tc) : _gearTrim;
            Color pants = SkaterLook.Pick(look.bottomsColor, out var pc) ? C(pc) : _gearPants;
            Color sole = SkaterLook.Pick(look.soleColor, out var oc) ? C(oc) : Palette.Cream;
            Color sock = SkaterLook.Pick(look.sockColor, out var kc) ? C(kc) : Palette.Cream;
            Color shoeC = C(LookPalette.Colors[look.shoeColor]);
            var shirtStyle = (ShirtStyle)look.shirtStyle;

            // Shirt: flannel is a checked cloth; everything else is a plain colour with a trim stripe.
            var shirtMat = shirtStyle == ShirtStyle.Flannel
                ? PlaceholderMaterials.GetTextured(DeckTextures.Get(DeckPattern.Checker, shirt, trim))
                : PlaceholderMaterials.Get(shirt);
            foreach (var r in _shirt) r.sharedMaterial = shirtMat;
            foreach (var r in _upperSleeves) r.sharedMaterial = look.Sleeveless ? skin : shirtMat;
            foreach (var r in _forearms) r.sharedMaterial = look.LongSleeves ? shirtMat : skin;
            _stripe.sharedMaterial = PlaceholderMaterials.Get(trim);
            _stripe.enabled = shirtStyle != ShirtStyle.Flannel && shirtStyle != ShirtStyle.Tank;

            switch (shirtStyle)
            {
                case ShirtStyle.Hoodie:
                    OutfitPart("Hood", PrimitiveType.Sphere, _hips, new Vector3(0f, 0.6f, -0.13f), new Vector3(0.3f, 0.17f, 0.15f), shirt);
                    OutfitPart("Pocket", PrimitiveType.Cube, _hips, new Vector3(0f, 0.22f, 0.11f), new Vector3(0.24f, 0.09f, 0.03f), shirt * 0.85f);
                    foreach (float x in new[] { -0.05f, 0.05f })
                        OutfitPart("String", PrimitiveType.Cylinder, _hips, new Vector3(x, 0.5f, 0.135f), new Vector3(0.012f, 0.05f, 0.012f), trim);
                    Cuffs(trim, wrist: true);
                    break;
                case ShirtStyle.LongSleeve:
                case ShirtStyle.Flannel:
                    Cuffs(trim, wrist: true);
                    break;
                case ShirtStyle.Jersey:
                    Cuffs(trim, wrist: false);
                    OutfitPart("Collar", PrimitiveType.Cylinder, _hips, new Vector3(0f, 0.57f, 0.01f), new Vector3(0.16f, 0.012f, 0.14f), trim);
                    break;
                case ShirtStyle.Tank:
                    foreach (float x in new[] { -0.12f, 0.12f })
                        OutfitPart("Strap", PrimitiveType.Cube, _hips, new Vector3(x, 0.55f, 0.01f), new Vector3(0.06f, 0.08f, 0.24f), shirt);
                    break;
            }

            // Bottoms.
            var bottoms = (BottomsStyle)look.bottomsStyle;
            var pantsMat = PlaceholderMaterials.Get(pants);
            foreach (var r in _pants) r.sharedMaterial = pantsMat;
            foreach (var r in _shins) r.sharedMaterial = look.BareShins ? skin : pantsMat;
            float legWidth = bottoms == BottomsStyle.Chinos ? 0.9f : bottoms == BottomsStyle.Sweats ? 1.15f : bottoms == BottomsStyle.Cargo ? 1.06f : 1f;
            foreach (var r in _thighs) ScaleXZ(r.transform, legWidth);
            foreach (var r in _shins) ScaleXZ(r.transform, look.BareShins ? 0.92f : legWidth);
            foreach (var (thigh, shin, side) in new[] { (_thighJointL, _shinJointL, -1f), (_thighJointR, _shinJointR, 1f) })
            {
                if (bottoms == BottomsStyle.Cargo)
                    OutfitPart("CargoPocket", PrimitiveType.Cube, thigh, new Vector3(0.08f * side, -ThighLength * 0.55f, 0f), new Vector3(0.03f, 0.1f, 0.09f), pants * 0.82f);
                if (bottoms == BottomsStyle.Sweats)
                    OutfitPart("Cuff", PrimitiveType.Cylinder, shin, new Vector3(0f, -ShinLength + 0.06f, 0f), new Vector3(0.155f, 0.03f, 0.155f), pants * 0.78f);
                if (bottoms == BottomsStyle.Shorts)
                {
                    OutfitPart("ShortsHem", PrimitiveType.Cylinder, thigh, new Vector3(0f, -ThighLength * 0.85f, 0f), new Vector3(0.165f, 0.02f, 0.165f), pants * 0.85f);
                    OutfitPart("Sock", PrimitiveType.Cylinder, shin, new Vector3(0f, -ShinLength + 0.06f, 0f), new Vector3(0.12f, 0.06f, 0.12f), sock);
                }

                // Shoes.
                var shoeStyle = (ShoeStyle)look.shoeStyle;
                if (shoeStyle == ShoeStyle.HighTop)
                {
                    OutfitPart("Collar", PrimitiveType.Cube, shin, new Vector3(0f, -ShinLength + 0.035f, 0.01f), new Vector3(0.14f, 0.09f, 0.15f), shoeC);
                    OutfitPart("Patch", PrimitiveType.Cylinder, shin, new Vector3(0.07f * side, -ShinLength + 0.03f, 0.01f), new Vector3(0.05f, 0.005f, 0.05f), sole)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            var style = (ShoeStyle)look.shoeStyle;
            var shoeMat = style == ShoeStyle.SlipOn
                ? PlaceholderMaterials.GetTextured(DeckTextures.Get(DeckPattern.Checker, shoeC, sole))
                : PlaceholderMaterials.Get(shoeC);
            foreach (var r in _shoes) { r.sharedMaterial = shoeMat; ScaleXZ(r.transform, style == ShoeStyle.Chunky ? 1.12f : style == ShoeStyle.SlipOn ? 0.94f : 1f); }
            var soleMat = PlaceholderMaterials.Get(sole);
            foreach (var r in _soles)
            {
                r.sharedMaterial = soleMat;
                ScaleXZ(r.transform, style == ShoeStyle.Chunky ? 1.12f : style == ShoeStyle.SlipOn ? 0.94f : 1f); // resets y to base
                ScaleY(r.transform, style == ShoeStyle.Chunky ? 2.6f : 1f);
            }

            ApplyBoardParts(look);
        }

        private void Cuffs(Color color, bool wrist)
        {
            foreach (var arm in new[] { _foreArmL, _foreArmR })
            {
                if (wrist) OutfitPart("Cuff", PrimitiveType.Cylinder, arm, new Vector3(0f, -0.21f, 0f), new Vector3(0.095f, 0.02f, 0.095f), color);
            }
            if (!wrist)
                foreach (var arm in new[] { _upperArmL, _upperArmR })
                    OutfitPart("SleeveBand", PrimitiveType.Cylinder, arm, new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.02f, 0.11f), color);
        }

        private MeshRenderer OutfitPart(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var r = Part(name, type, parent, pos, scale, color);
            _outfitParts.Add(r.gameObject);
            return r;
        }

        private void ScaleY(Transform t, float k)
        {
            if (!_baseScale.TryGetValue(t, out var s)) { s = t.localScale; _baseScale[t] = s; }
            t.localScale = new Vector3(t.localScale.x, s.y * k, t.localScale.z);
        }

        /// <summary>Deck shape plus wheel, truck and grip colours ("shop" choices keep the equipped gear).</summary>
        private void ApplyBoardParts(SkaterLook look)
        {
            var shape = (DeckShape)look.deckShape;
            Vector3 deck = shape == DeckShape.Cruiser ? new Vector3(0.27f, 0.035f, 0.74f) : shape == DeckShape.OldSchool ? new Vector3(0.3f, 0.035f, 0.8f) : new Vector3(0.24f, 0.035f, 0.84f);
            _deck.transform.localScale = deck;
            _grip.transform.localScale = new Vector3(deck.x - 0.02f, 0.005f, deck.z - 0.04f);
            if (_deckEnds.Count == 2)
            {
                var nose = _deckEnds[0].transform;
                var tail = _deckEnds[1].transform;
                float half = deck.z * 0.5f + 0.05f;
                switch (shape)
                {
                    case DeckShape.Cruiser:
                        nose.localPosition = new Vector3(0f, 0.025f, half - 0.01f); nose.localScale = new Vector3(0.25f, 0.03f, 0.1f); nose.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                        tail.localPosition = new Vector3(0f, 0.025f, -half + 0.01f); tail.localScale = new Vector3(0.25f, 0.03f, 0.1f); tail.localRotation = Quaternion.Euler(8f, 0f, 0f);
                        break;
                    case DeckShape.OldSchool:
                        nose.localPosition = new Vector3(0f, 0.03f, half); nose.localScale = new Vector3(0.2f, 0.03f, 0.12f); nose.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                        tail.localPosition = new Vector3(0f, 0.04f, -half - 0.01f); tail.localScale = new Vector3(0.3f, 0.03f, 0.16f); tail.localRotation = Quaternion.Euler(20f, 0f, 0f);
                        break;
                    default:
                        nose.localPosition = new Vector3(0f, 0.035f, 0.47f); nose.localScale = new Vector3(0.22f, 0.03f, 0.14f); nose.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                        tail.localPosition = new Vector3(0f, 0.035f, -0.47f); tail.localScale = new Vector3(0.22f, 0.03f, 0.14f); tail.localRotation = Quaternion.Euler(18f, 0f, 0f);
                        break;
                }
            }
            float wheelSize = shape == DeckShape.Cruiser ? 0.09f : shape == DeckShape.OldSchool ? 0.08f : 0.07f;
            var wheelMat = PlaceholderMaterials.Get(SkaterLook.Pick(look.wheelColor, out var wc) ? C(wc) : _gearWheels);
            foreach (var w in _wheels)
            {
                w.sharedMaterial = wheelMat;
                w.transform.localScale = new Vector3(wheelSize, 0.02f + (wheelSize - 0.07f) * 0.5f, wheelSize);
            }
            var truckMat = PlaceholderMaterials.Get(SkaterLook.Pick(look.truckColor, out var tc) ? C(tc) : Palette.Metal);
            foreach (var t in _trucks) t.sharedMaterial = truckMat;
            _grip.sharedMaterial = PlaceholderMaterials.Get(SkaterLook.Pick(look.gripColor, out var gc) ? C(gc) : _gearGrip);
        }

        private void RefreshCap(bool hairHidesCap)
        {
            bool show = !_hatHiddenByGear && !hairHidesCap;
            _cap.enabled = show;
            _brim.enabled = show;
        }

        private void ScaleXZ(Transform t, float k)
        {
            if (!_baseScale.TryGetValue(t, out var s)) { s = t.localScale; _baseScale[t] = s; }
            t.localScale = new Vector3(s.x * k, s.y, s.z * k);
        }

        /// <returns>True when the style hides the cap (big hair).</returns>
        private bool BuildHair(HairStyle style, Color color)
        {
            if (_hair != null) Destroy(_hair.gameObject);
            _hair = Joint("Hair", _hips, Vector3.zero);
            if (style == HairStyle.None) return false;
            void H(PrimitiveType type, Vector3 pos, Vector3 scale, float pitch = 0f)
            {
                var r = Part("HairPart", type, _hair, pos, scale, color);
                if (pitch != 0f) r.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            switch (style)
            {
                case HairStyle.Afro:
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.84f, -0.02f), new Vector3(0.42f, 0.36f, 0.42f));
                    return true;
                case HairStyle.Mohawk:
                    H(PrimitiveType.Cube, new Vector3(0f, 0.9f, -0.01f), new Vector3(0.05f, 0.14f, 0.28f));
                    return true;
                case HairStyle.Bun:
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.79f, -0.015f), new Vector3(0.275f, 0.25f, 0.28f));
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.86f, -0.15f), new Vector3(0.13f, 0.13f, 0.13f));
                    return false;
                case HairStyle.Long:
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.79f, -0.015f), new Vector3(0.28f, 0.26f, 0.29f));
                    H(PrimitiveType.Capsule, new Vector3(0f, 0.62f, -0.11f), new Vector3(0.26f, 0.16f, 0.08f));
                    return false;
                case HairStyle.Twists:
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.79f, -0.015f), new Vector3(0.28f, 0.25f, 0.29f));
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.Lerp(-150f, 150f, i / 6f) * Mathf.Deg2Rad;
                        var p = new Vector3(Mathf.Sin(a) * 0.13f, 0.66f, -Mathf.Cos(a) * 0.12f);
                        H(PrimitiveType.Capsule, p, new Vector3(0.045f, 0.09f, 0.045f));
                    }
                    return false;
                default: // Short
                    H(PrimitiveType.Sphere, new Vector3(0f, 0.8f, -0.012f), new Vector3(0.272f, 0.24f, 0.282f));
                    return false;
            }
        }

        private void BuildEyewear(Eyewear kind)
        {
            if (_eyewear != null) Destroy(_eyewear.gameObject);
            _eyewear = Joint("Eyewear", _hips, Vector3.zero);
            if (kind == Eyewear.Shades)
                Part("Shades", PrimitiveType.Cube, _eyewear, new Vector3(0f, 0.785f, 0.135f), new Vector3(0.22f, 0.055f, 0.04f), Palette.Ink);
            else if (kind == Eyewear.Round)
                foreach (float x in new[] { -0.055f, 0.055f })
                {
                    var lens = Part("Lens", PrimitiveType.Cylinder, _eyewear, new Vector3(x, 0.785f, 0.14f), new Vector3(0.07f, 0.008f, 0.07f), Palette.Metal);
                    lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
        }

        /// <summary>
        /// The everyday motion layer (Phase 16): push kick, landing squash, ollie pop (board nose-up), carve lean and
        /// an air tuck. Ignored while a trick, grind/manual or bail owns the pose.
        /// </summary>
        public void SetMotion(float pushSwing, float pushReach, float pushBob, float squash, float popPitch, float carveLean, float airTuck)
        {
            if (!MotionFree) return;
            _pushSwing = pushSwing;
            _pushReach = pushReach;
            _pushBob = pushBob;
            _squash = squash;
            _airTuck = airTuck;
            _board.localRotation = Quaternion.Euler(-popPitch, 0f, 0f);
            _board.localPosition = _boardBasePos + Vector3.up * (0.004f * popPitch);
            _pivot.localRotation = Quaternion.Euler(0f, 0f, -carveLean);
            ApplyLimbs();
        }

        private void ClearMotion()
        {
            _pushSwing = _pushReach = _pushBob = _squash = _airTuck = _flick = 0f;
        }

        public void SetCrouch(float amount)
        {
            _crouch = Mathf.Clamp01(amount);
            ApplyLimbs();
        }

        /// <param name="progress">0..1 through the trick.</param>
        /// <param name="sideSign">-1 for left swipes, used to mirror shove direction.</param>
        public void SetTrickPose(TrickDefinition trick, float progress, float sideSign)
        {
            if (_board == null || trick == null) return;
            if (!_trickActive) ClearMotion();
            _trickActive = true;
            float p = Mathf.SmoothStep(0f, 1f, progress);
            float bump = Mathf.Sin(progress * Mathf.PI);
            bool flipping = Mathf.Abs(trick.rollTurns) > 0.01f || Mathf.Abs(trick.pitchTurns) > 0.01f;
            // Front foot flicks the board at the start of a flip; feet tuck up out of its way.
            _flick = flipping ? 14f * Mathf.Clamp01(1f - progress / 0.3f) : 0f;

            Quaternion rot = Quaternion.Euler(
                trick.pitchTurns * 360f * p + trick.grabTilt.x * bump,
                trick.yawDegrees * sideSign * p + trick.grabTilt.y * bump,
                trick.rollTurns * 360f * p + trick.grabTilt.z * bump);
            _board.localRotation = rot;
            _board.localPosition = _boardBasePos + Vector3.up * ((flipping ? 0.34f : 0.25f) * bump);
            bool grab = trick.category == Core.TrickCategory.Grab;
            _reach = grab ? bump : 0f;
            _armsOut = grab ? 0f : 0.55f * bump;
            SetCrouch(Mathf.Max(0.35f, bump * (grab ? 1f : flipping ? 0.8f : 0.6f)));
            if (Mathf.Abs(trick.bodySpinDegrees) > 0f)
                _pivot.localRotation = Quaternion.Euler(0f, trick.bodySpinDegrees * p, 0f);
        }

        public void ClearTrickPose()
        {
            if (_board == null) return;
            _trickActive = false;
            _flick = 0f;
            _board.localRotation = Quaternion.identity;
            _board.localPosition = _boardBasePos;
            _pivot.localRotation = Quaternion.identity;
            _reach = 0f;
            _armsOut = 0f;
            SetCrouch(0f);
        }

        /// <summary>Balance lean for grinds (roll) and manuals (pitch onto tail/nose). Arms go out to balance.</summary>
        public void SetBalancePose(float lean, bool manual, bool nose)
        {
            if (_pivot == null) return;
            if (!_balanceActive) ClearMotion();
            _balanceActive = true;
            if (manual)
            {
                float pitch = nose ? 12f : -12f;
                _board.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                _pivot.localRotation = Quaternion.Euler(0f, 0f, -lean * 25f);
            }
            else
            {
                _board.localRotation = Quaternion.Euler(0f, 60f, 0f); // board across the rail
                _pivot.localRotation = Quaternion.Euler(0f, 0f, -lean * 30f);
            }
            _armsOut = 0.6f + 0.4f * Mathf.Abs(lean);
            _crouch = 0.3f;
            ApplyLimbs();
        }

        public void ClearBalancePose()
        {
            if (_pivot == null) return;
            _balanceActive = false;
            _pivot.localRotation = Quaternion.identity;
            _board.localRotation = Quaternion.identity;
            _armsOut = 0f;
            SetCrouch(0f);
        }

        public void PlayBail()
        {
            if (_bailRoutine != null) StopCoroutine(_bailRoutine);
            _bailRoutine = StartCoroutine(BailRoutine());
        }

        public void ResetPose()
        {
            if (_bailRoutine != null) { StopCoroutine(_bailRoutine); _bailRoutine = null; }
            if (_pivot == null) return;
            _pivot.localPosition = Vector3.zero;
            _balanceActive = false;
            ClearMotion();
            ClearTrickPose();
        }

        /// <summary>
        /// The slam (Phase 16): the body pitches over hard, hits the ground with a small bounce and slides to a stop,
        /// arms flailing; the board flies off on its own arc, spinning, and bounces away.
        /// </summary>
        private IEnumerator BailRoutine()
        {
            ClearTrickPose();
            ClearMotion();
            _balanceActive = false;
            float t = 0f;
            Vector3 tumbleAxis = new Vector3(Random.Range(-1f, 1f), 0.15f, Random.Range(0.6f, 1f)).normalized;
            float vx = Random.Range(-2.2f, 2.2f), vy = Random.Range(2.6f, 3.6f), vz = Random.Range(1.5f, 3.5f);
            Vector3 boardSpin = new Vector3(Random.Range(240f, 520f), Random.Range(-90f, 90f), Random.Range(-360f, 360f));
            const float duration = 1.25f;
            while (t < duration)
            {
                t += Time.deltaTime;
                // Body: fast pitch over (0-0.18 s), impact bounce, then a slide that settles.
                float over = Core.SkaterMotion.Smooth(t / 0.18f);
                float settle = Core.SkaterMotion.Smooth((t - 0.18f) / 0.6f);
                float angle = Mathf.Lerp(0f, 82f, over) + 14f * settle;
                float bounce = t > 0.18f && t < 0.45f ? Mathf.Sin((t - 0.18f) / 0.27f * Mathf.PI) * 0.09f : 0f;
                var pivotRot = Quaternion.AngleAxis(angle, tumbleAxis);
                var pivotPos = Vector3.down * (0.5f * over) + Vector3.up * bounce;
                _pivot.localRotation = pivotRot;
                _pivot.localPosition = pivotPos;

                // Board: its own throw, expressed in the (rotating) pivot's space.
                var (bx, by, bz) = Core.SkaterMotion.BoardArc(t, vx, vy, vz);
                Vector3 boardWorld = _boardBasePos + new Vector3(bx, by, bz);
                var inv = Quaternion.Inverse(pivotRot);
                _board.localPosition = inv * (boardWorld - pivotPos);
                float spinFade = 1f - Core.SkaterMotion.Smooth((t - 0.5f) / 0.6f);
                _board.localRotation = inv * Quaternion.Euler(boardSpin * (t * (0.4f + 0.6f * spinFade)));

                // Arms flail, then go limp; knees fold.
                float flail = (1f - settle) * Mathf.Abs(Mathf.Sin(t * 22f));
                _armsOut = Mathf.Clamp01(0.4f + 0.6f * flail);
                _crouch = 0.65f * over;
                ApplyLimbs();
                yield return null;
            }
            _bailRoutine = null;
        }
    }
}
