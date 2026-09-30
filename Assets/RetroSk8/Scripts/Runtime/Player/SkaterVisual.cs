using System.Collections;
using System.Collections.Generic;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Procedural placeholder skater (primitive body + board) and all of its poses.
    /// Pure presentation: never touches physics. Swap for a rigged model later behind the same method surface.
    /// </summary>
    public sealed class SkaterVisual : MonoBehaviour
    {
        private Transform _pivot;     // whole-body pose (lean, crouch, bail tumble)
        private Transform _body;
        private Transform _board;
        private Vector3 _bodyBaseScale;
        private Vector3 _boardBasePos;
        private Coroutine _bailRoutine;
        private float _crouch;

        // Renderers recoloured by cosmetics.
        private MeshRenderer _deck, _grip, _cap, _legs, _stripe;
        private readonly List<MeshRenderer> _wheels = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _shirt = new List<MeshRenderer>();

        public Color shirtColor = Palette.Coral;
        public Color pantsColor = Palette.Ink;
        public Color deckColor = Palette.TapeYellow;
        public Color wheelColor = Palette.Cream;
        public Color hatColor = Palette.Teal;
        public Color skinColor = new Color(0.55f, 0.38f, 0.28f);

        public Transform Board => _board;

        public void Build()
        {
            _pivot = new GameObject("Pose").transform;
            _pivot.SetParent(transform, false);

            _body = new GameObject("Body").transform;
            _body.SetParent(_pivot, false);
            _legs = R(PrimitiveMeshes.CreateVisual("Legs", PrimitiveType.Cube, _body, new Vector3(0f, 0.55f, 0f), new Vector3(0.34f, 0.7f, 0.22f), pantsColor));
            _shirt.Add(R(PrimitiveMeshes.CreateVisual("Torso", PrimitiveType.Cube, _body, new Vector3(0f, 1.18f, 0f), new Vector3(0.46f, 0.6f, 0.26f), shirtColor)));
            _stripe = R(PrimitiveMeshes.CreateVisual("ShirtStripe", PrimitiveType.Cube, _body, new Vector3(0f, 1.2f, 0f), new Vector3(0.47f, 0.08f, 0.27f), Palette.Cream));
            PrimitiveMeshes.CreateVisual("Head", PrimitiveType.Sphere, _body, new Vector3(0f, 1.66f, 0f), Vector3.one * 0.3f, skinColor);
            _cap = R(PrimitiveMeshes.CreateVisual("Cap", PrimitiveType.Cube, _body, new Vector3(0f, 1.8f, 0.06f), new Vector3(0.3f, 0.08f, 0.38f), hatColor));
            _shirt.Add(R(PrimitiveMeshes.CreateVisual("ArmL", PrimitiveType.Cube, _body, new Vector3(-0.34f, 1.15f, 0f), new Vector3(0.12f, 0.55f, 0.12f), shirtColor)));
            _shirt.Add(R(PrimitiveMeshes.CreateVisual("ArmR", PrimitiveType.Cube, _body, new Vector3(0.34f, 1.15f, 0f), new Vector3(0.12f, 0.55f, 0.12f), shirtColor)));
            _body.localRotation = Quaternion.Euler(0f, 70f, 0f); // side-on stance
            _bodyBaseScale = _body.localScale;

            _board = new GameObject("Board").transform;
            _board.SetParent(_pivot, false);
            _boardBasePos = new Vector3(0f, 0.12f, 0f);
            _board.localPosition = _boardBasePos;
            _deck = R(PrimitiveMeshes.CreateVisual("Deck", PrimitiveType.Cube, _board, Vector3.zero, new Vector3(0.24f, 0.035f, 0.84f), deckColor));
            _grip = R(PrimitiveMeshes.CreateVisual("Grip", PrimitiveType.Cube, _board, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.005f, 0.8f), Palette.Ink));
            foreach (float z in new[] { -0.28f, 0.28f })
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                var w = PrimitiveMeshes.CreateVisual("Wheel", PrimitiveType.Cylinder, _board, new Vector3(x, -0.06f, z), new Vector3(0.07f, 0.02f, 0.07f), wheelColor);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                _wheels.Add(R(w));
            }
        }

        private static MeshRenderer R(GameObject go) => go.GetComponent<MeshRenderer>();

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
                _cap.sharedMaterial = PlaceholderMaterials.Get(hat.primary);
            }
            var palette = loadout[CosmeticSlot.Palette];
            if (palette != null) _legs.sharedMaterial = PlaceholderMaterials.Get(palette.primary);
        }

        public void SetCrouch(float amount)
        {
            _crouch = Mathf.Clamp01(amount);
            if (_body == null) return;
            _body.localScale = new Vector3(_bodyBaseScale.x, _bodyBaseScale.y * (1f - 0.22f * _crouch), _bodyBaseScale.z);
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
            SetCrouch(Mathf.Max(0.35f, bump * (trick.category == Core.TrickCategory.Grab ? 1f : 0.6f)));
            if (Mathf.Abs(trick.bodySpinDegrees) > 0f)
                _pivot.localRotation = Quaternion.Euler(0f, trick.bodySpinDegrees * p, 0f);
        }

        public void ClearTrickPose()
        {
            if (_board == null) return;
            _board.localRotation = Quaternion.identity;
            _board.localPosition = _boardBasePos;
            _pivot.localRotation = Quaternion.identity;
            SetCrouch(0f);
        }

        /// <summary>Balance lean for grinds (roll) and manuals (pitch onto tail/nose).</summary>
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
        }

        public void ClearBalancePose()
        {
            if (_pivot == null) return;
            _pivot.localRotation = Quaternion.identity;
            _board.localRotation = Quaternion.identity;
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
                yield return null;
            }
            _bailRoutine = null;
        }
    }
}
