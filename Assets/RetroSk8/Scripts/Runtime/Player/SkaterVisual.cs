using System.Collections;
using System.Collections.Generic;
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

        // Renderers recoloured by cosmetics.
        private MeshRenderer _deck, _grip, _cap, _brim, _stripe;
        private readonly List<MeshRenderer> _wheels = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _shirt = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _pants = new List<MeshRenderer>();

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
            _pants.Add(Part("Pelvis", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.14f, 0.24f), pantsColor));

            // Torso: tapered by stacking a wide chest over a narrower waist.
            _shirt.Add(Part("Waist", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.17f, 0.22f), shirtColor));
            _shirt.Add(Part("Chest", PrimitiveType.Capsule, _hips, new Vector3(0f, 0.42f, 0.01f), new Vector3(0.44f, 0.2f, 0.26f), shirtColor));
            _stripe = Part("ShirtStripe", PrimitiveType.Cylinder, _hips, new Vector3(0f, 0.36f, 0.01f), new Vector3(0.43f, 0.025f, 0.27f), Palette.Cream);

            Part("Neck", PrimitiveType.Cylinder, _hips, new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 0.05f, 0.1f), skinColor);
            Part("Head", PrimitiveType.Sphere, _hips, new Vector3(0f, 0.76f, 0.01f), new Vector3(0.26f, 0.3f, 0.27f), skinColor);
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
                Part("Truck", PrimitiveType.Cube, _board, new Vector3(0f, -0.04f, z), new Vector3(0.2f, 0.03f, 0.04f), Palette.Metal);
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    var w = Part("Wheel", PrimitiveType.Cylinder, _board, new Vector3(x, -0.06f, z), new Vector3(0.07f, 0.02f, 0.07f), wheelColor);
                    w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    _wheels.Add(w);
                }
            }
            ApplyLimbs();
        }

        private readonly List<MeshRenderer> _deckEnds = new List<MeshRenderer>();

        private void BuildArm(float side, out Transform upper, out Transform fore)
        {
            upper = Joint(side < 0 ? "ShoulderL" : "ShoulderR", _hips, new Vector3(0.25f * side, 0.52f, 0f));
            _shirt.Add(Part("UpperArm", PrimitiveType.Capsule, upper, new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.15f, 0.1f), shirtColor));
            fore = Joint("Elbow", upper, new Vector3(0f, -0.27f, 0f));
            Part("Forearm", PrimitiveType.Capsule, fore, new Vector3(0f, -0.12f, 0f), new Vector3(0.085f, 0.13f, 0.085f), skinColor);
            Part("Hand", PrimitiveType.Sphere, fore, new Vector3(0f, -0.27f, 0f), new Vector3(0.09f, 0.1f, 0.09f), skinColor);
        }

        private void BuildLeg(float side, out Transform thigh, out Transform shin)
        {
            thigh = Joint(side < 0 ? "HipL" : "HipR", _hips, new Vector3(0.16f * side, 0f, 0f));
            _pants.Add(Part("Thigh", PrimitiveType.Capsule, thigh, new Vector3(0f, -ThighLength * 0.5f, 0f), new Vector3(0.15f, ThighLength * 0.55f, 0.15f), pantsColor));
            shin = Joint("Knee", thigh, new Vector3(0f, -ThighLength, 0f));
            _pants.Add(Part("Shin", PrimitiveType.Capsule, shin, new Vector3(0f, -ShinLength * 0.5f, 0f), new Vector3(0.13f, ShinLength * 0.55f, 0.13f), pantsColor));
            Part("Shoe", PrimitiveType.Cube, shin, new Vector3(0f, -ShinLength - 0.03f, 0.04f), new Vector3(0.13f, 0.08f, 0.26f), shoeColor);
        }

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
            float drop = 0.3f * _crouch;
            _hips.localPosition = new Vector3(0f, HipHeight - drop, 0f);
            float lean = 18f * _crouch + 25f * _reach;
            _hips.localRotation = Quaternion.Euler(lean, 0f, 0f); // lean forward

            // Solve the knee angle for the new hip height (law of cosines, feet fixed under the hips).
            float reachLen = Mathf.Clamp(HipHeight - drop - 0.21f, 0.3f, ThighLength + ShinLength - 0.001f); // ankle sits just above the deck
            float cosKnee = (ThighLength * ThighLength + ShinLength * ShinLength - reachLen * reachLen) / (2f * ThighLength * ShinLength);
            float knee = 180f - Mathf.Acos(Mathf.Clamp(cosKnee, -1f, 1f)) * Mathf.Rad2Deg;
            // Thigh forward by half the knee angle (minus the hip lean) and shin back by the full knee angle,
            // so the foot ends up straight under the hip joint, on the deck.
            float thighPitch = -knee * 0.5f - lean;
            foreach (var (thigh, shin, side) in new[] { (_thighL, _shinL, -1f), (_thighR, _shinR, 1f) })
            {
                thigh.localRotation = Quaternion.Euler(thighPitch, 0f, 4f * side * (1f - _crouch));
                shin.localRotation = Quaternion.Euler(knee, 0f, 0f);
            }

            float armOut = Mathf.Lerp(12f, 70f, _armsOut);
            _upperArmL.localRotation = Quaternion.Euler(-20f * _crouch, 0f, -armOut);
            _upperArmR.localRotation = Quaternion.Euler(-20f * _crouch - 30f * _reach, 0f, armOut * (1f - _reach));
            _foreArmL.localRotation = Quaternion.Euler(-25f - 20f * _crouch, 0f, 0f);
            _foreArmR.localRotation = Quaternion.Euler(-25f * (1f - _reach), 0f, 0f);
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
            if (deck != null)
            {
                // The deck's underside graphic shows on flips; the grip covers the top.
                _deck.sharedMaterial = deck.pattern == DeckPattern.Solid
                    ? PlaceholderMaterials.Get(deck.primary)
                    : PlaceholderMaterials.GetTextured(DeckTextures.Get(deck.pattern, deck.primary, deck.secondary));
                foreach (var e in _deckEnds) e.sharedMaterial = PlaceholderMaterials.Get(deck.primary);
            }
            var wheels = loadout[CosmeticSlot.Wheels];
            if (wheels != null) foreach (var w in _wheels) w.sharedMaterial = PlaceholderMaterials.Get(wheels.primary);
            var grip = loadout[CosmeticSlot.Grip];
            if (grip != null) _grip.sharedMaterial = PlaceholderMaterials.Get(grip.primary);
            var shirt = loadout[CosmeticSlot.Shirt];
            if (shirt != null)
            {
                foreach (var r in _shirt) r.sharedMaterial = PlaceholderMaterials.Get(shirt.primary);
                _stripe.sharedMaterial = PlaceholderMaterials.Get(shirt.secondary);
            }
            var hat = loadout[CosmeticSlot.Hat];
            if (hat != null)
            {
                _cap.enabled = !hat.hidesItem;
                _brim.enabled = !hat.hidesItem;
                _cap.sharedMaterial = PlaceholderMaterials.Get(hat.primary);
                _brim.sharedMaterial = PlaceholderMaterials.Get(hat.primary);
            }
            var palette = loadout[CosmeticSlot.Palette];
            if (palette != null) foreach (var p in _pants) p.sharedMaterial = PlaceholderMaterials.Get(palette.primary);
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
            float p = Mathf.SmoothStep(0f, 1f, progress);
            float bump = Mathf.Sin(progress * Mathf.PI);

            Quaternion rot = Quaternion.Euler(
                trick.pitchTurns * 360f * p + trick.grabTilt.x * bump,
                trick.yawDegrees * sideSign * p + trick.grabTilt.y * bump,
                trick.rollTurns * 360f * p + trick.grabTilt.z * bump);
            _board.localRotation = rot;
            _board.localPosition = _boardBasePos + Vector3.up * (0.25f * bump);
            bool grab = trick.category == Core.TrickCategory.Grab;
            _reach = grab ? bump : 0f;
            _armsOut = grab ? 0f : 0.5f * bump;
            SetCrouch(Mathf.Max(0.35f, bump * (grab ? 1f : 0.6f)));
            if (Mathf.Abs(trick.bodySpinDegrees) > 0f)
                _pivot.localRotation = Quaternion.Euler(0f, trick.bodySpinDegrees * p, 0f);
        }

        public void ClearTrickPose()
        {
            if (_board == null) return;
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
            ClearTrickPose();
        }

        private IEnumerator BailRoutine()
        {
            ClearTrickPose();
            float t = 0f;
            Vector3 tumbleAxis = new Vector3(Random.Range(-1f, 1f), 0.2f, Random.Range(0.5f, 1f)).normalized;
            Vector3 boardDrift = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(0.5f, 1.5f));
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.6f);
                _pivot.localRotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 95f, k), tumbleAxis);
                _pivot.localPosition = Vector3.down * 0.3f * k;
                _board.localPosition = _boardBasePos + boardDrift * k;
                _board.localRotation = Quaternion.Euler(0f, 0f, 180f * k);
                _armsOut = k;   // arms flail out
                _crouch = 0.6f * k;
                ApplyLimbs();
                yield return null;
            }
            _bailRoutine = null;
        }
    }
}
