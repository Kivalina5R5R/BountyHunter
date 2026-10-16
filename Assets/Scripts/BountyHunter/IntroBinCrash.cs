using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The bin in the intro: the same blurred bin that lies in the front corner, stood upright in front
    // of where the patrol car parks. When the sliding car reaches it (impactAtSeconds into the arrival
    // video) it is knocked flying, tumbling toward the camera, and lands exactly as the bin lying in
    // the corner, so the two swap without a seam. The hit flashes with a
    // shock ring, kicks up dust and scatters trash, and the whole wall shakes for a moment.
    // Lives on the intro wall picture; positions are in parts of it ((0, 0) bottom left).
    public sealed class IntroBinCrash : MonoBehaviour
    {
        [SerializeField] private WallVideo arrival;
        [SerializeField] private RectTransform standingBin;
        // The same bin lying in the front corner, shown once the flying bin gets there.
        [SerializeField] private RawImage landedBin;
        // Extra whole turns while it flies, on top of tipping over from standing to lying.
        [SerializeField, Min(0)] private int extraTurns = 1;
        [SerializeField] private Sprite[] trash;
        [SerializeField] private Sprite[] puffs;
        [SerializeField] private Sprite[] stars;

        [Header("Timing")]
        // When the car's bumper reaches the bin in the arrival video.
        [SerializeField, Min(0f)] private float impactAtSeconds = 1.48f;
        [SerializeField, Min(0.1f)] private float flightSeconds = 0.55f;
        // How high the bin flies, in wall heights.
        [SerializeField, Min(0f)] private float flightArc = 0.3f;

        [Header("Shake")]
        // Shakes the wall through the ambience when there is one, so it adds to the camera drift.
        [SerializeField] private IntroAmbience ambience;
        [SerializeField, Min(0f)] private float shakeStrength = 0.008f;
        [SerializeField, Min(0.05f)] private float shakeSeconds = 0.3f;

        private enum Motion
        {
            // Thrown out, falling under gravity, spinning.
            Thrown,
            // Rolls out along the ground, growing and fading.
            Puff,
            // Pops in big and shrinks away.
            Pop
        }

        private sealed class Piece
        {
            public Image Image;
            public Motion Motion;
            public float Start;
            public float Duration;
            public Vector2 From;
            public Vector2 Velocity;
            public float Size;
            public float Spin;
            public float Yaw;
        }

        private const float Gravity = 2.6f;
        private static readonly Color DustColor = new Color(0.92f, 0.92f, 0.95f, 0.85f);
        private static readonly Color FlashColor = new Color(1f, 0.96f, 0.8f, 1f);

        private readonly List<Piece> pieces = new List<Piece>();
        private RectTransform wall;
        private bool crashed;
        private float sinceCrash;

        private void Awake()
        {
            wall = (RectTransform)transform;
            SetAlpha(landedBin, 0f);
        }

        private void Update()
        {
            if (!crashed && arrival != null && arrival.IsPlaying && arrival.PlaybackTime >= impactAtSeconds) Crash();
            if (!crashed) return;

            sinceCrash += Time.deltaTime;
            foreach (Piece piece in pieces)
            {
                Apply(piece, (sinceCrash - piece.Start) / piece.Duration);
            }
        }

        private void Crash()
        {
            crashed = true;
            if (standingBin == null) return;

            // The bumper hits the bin's lower right side.
            Vector2 hit = Local(standingBin, new Vector2(1f, 0.35f));
            float height = wall.rect.height;
            AddFlash(hit, height);
            AddDust(Local(standingBin, new Vector2(0.6f, 0f)), height);
            AddTrash(hit, height);
            StartCoroutine(Fly(height));
            if (ambience != null) ambience.Shake(shakeStrength * height, shakeSeconds);
            else StartCoroutine(Shake(height));
        }

        // Tumbles up and over toward the camera, growing, and ends exactly where, how big and how
        // turned the bin in the corner is; then that one takes its place.
        private IEnumerator Fly(float height)
        {
            var landed = landedBin != null ? (RectTransform)landedBin.transform : null;
            Vector2 start = standingBin.anchoredPosition;
            Vector2 end = start + (landed != null ? Centre(landed) - Centre(standingBin) : new Vector2(-0.4f, -0.2f) * height);
            float grow = landed != null ? landed.rect.height / standingBin.rect.height : 2.5f;
            // Rolling to the left turns it counter-clockwise: from standing up to lying as it lies there.
            float startTurn = Mathf.DeltaAngle(0f, standingBin.localEulerAngles.z);
            float endTurn = (landed != null ? Mathf.DeltaAngle(0f, landed.localEulerAngles.z) : 0f) + 360f * extraTurns;
            for (float time = 0f; time < flightSeconds; time += Time.deltaTime)
            {
                float u = time / flightSeconds;
                standingBin.anchoredPosition = Vector2.Lerp(start, end, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * flightArc * height);
                // Fast spin at the hit, slowing as it lands.
                standingBin.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startTurn, endTurn, u * (2f - u)));
                standingBin.localScale = Vector3.one * Mathf.Lerp(1f, grow, u * u);
                yield return null;
            }

            SetAlpha(standingBin.GetComponent<RawImage>(), 0f);
            SetAlpha(landedBin, 1f);
            if (landedBin != null) StartCoroutine(Land((RectTransform)landedBin.transform, height));
        }

        // A little squash as it hits the ground, with a puff of dust.
        private IEnumerator Land(RectTransform bin, float height)
        {
            AddDust(Local(bin, new Vector2(0.5f, 0.35f)), height * 1.6f, sinceCrash);
            const float landSeconds = 0.3f;
            for (float time = 0f; time < landSeconds; time += Time.deltaTime)
            {
                float u = time / landSeconds;
                float squash = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u) * 0.08f;
                bin.localScale = new Vector3(1f + squash, 1f - squash, 1f);
                yield return null;
            }

            bin.localScale = Vector3.one;
        }

        private IEnumerator Shake(float height)
        {
            var group = transform.parent as RectTransform;
            if (group == null) yield break;

            Vector3 home = group.localPosition;
            for (float time = 0f; time < shakeSeconds; time += Time.deltaTime)
            {
                float fade = 1f - time / shakeSeconds;
                group.localPosition = home + (Vector3)(Random.insideUnitCircle * (shakeStrength * height * fade * fade));
                yield return null;
            }

            group.localPosition = home;
        }

        private void AddFlash(Vector2 at, float height)
        {
            Add(PosterSprites.Dot, FlashColor, Motion.Pop, 0f, 0.16f, at, Vector2.zero, height * 0.5f, 0f);
            Add(PosterSprites.Ring, FlashColor, Motion.Pop, 0f, 0.3f, at, Vector2.zero, height * 0.7f, 0f);
            if (stars == null || stars.Length == 0) return;

            for (int i = 0; i < 3; i++)
            {
                Vector2 spot = at + Random.insideUnitCircle * height * 0.12f;
                Add(stars[Random.Range(0, stars.Length)], Color.white, Motion.Pop, Random.Range(0f, 0.12f), 0.3f, spot, Vector2.zero, height * Random.Range(0.08f, 0.12f), Random.Range(-200f, 200f));
            }
        }

        // Dust rolling out to both sides along the ground; mostly away from the car (to the left).
        private void AddDust(Vector2 ground, float height, float start = 0f)
        {
            if (puffs == null || puffs.Length == 0) return;

            for (int i = 0; i < 6; i++)
            {
                float side = i < 4 ? -1f : 1f;
                var velocity = new Vector2(side * Random.Range(0.25f, 0.6f), Random.Range(0.02f, 0.12f)) * height;
                Add(puffs[Random.Range(0, puffs.Length)], DustColor, Motion.Puff, start + Random.Range(0f, 0.08f), Random.Range(0.45f, 0.7f),
                    ground, velocity, height * Random.Range(0.08f, 0.13f), Random.Range(-90f, 90f));
            }
        }

        // Trash spraying out of the bin, away from the car and up.
        private void AddTrash(Vector2 at, float height)
        {
            if (trash == null || trash.Length == 0) return;

            for (int i = 0; i < 9; i++)
            {
                var velocity = new Vector2(Random.Range(-1.3f, -0.2f), Random.Range(0.6f, 1.4f)) * height;
                Add(trash[Random.Range(0, trash.Length)], Color.white, Motion.Thrown, Random.Range(0f, 0.06f), Random.Range(0.7f, 1f),
                    at, velocity, height * Random.Range(0.05f, 0.09f), Random.Range(-720f, 720f));
            }
        }

        private void Add(Sprite sprite, Color color, Motion motion, float start, float duration, Vector2 from, Vector2 velocity, float size, float spin)
        {
            if (sprite == null) return;

            var pieceObject = new GameObject(motion.ToString(), typeof(RectTransform), typeof(Image));
            pieceObject.transform.SetParent(transform, false);
            var image = pieceObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            var piece = new Piece { Image = image, Motion = motion, Start = start, Duration = duration, From = from, Velocity = velocity, Size = 1f, Spin = spin, Yaw = Random.Range(0f, 360f) };
            pieces.Add(piece);
            Apply(piece, -1f);
        }

        // t below 0 is not started yet, above 1 is finished; both are hidden.
        private void Apply(Piece piece, float t)
        {
            bool visible = t >= 0f && t <= 1f;
            piece.Image.enabled = visible;
            if (!visible) return;

            float seconds = t * piece.Duration;
            Vector2 position = piece.From;
            float scale = 1f;
            float alpha = piece.Image.color.a;
            switch (piece.Motion)
            {
                case Motion.Thrown:
                    position += piece.Velocity * seconds + Vector2.down * (0.5f * Gravity * wall.rect.height * seconds * seconds);
                    alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                    break;
                case Motion.Puff:
                    position += piece.Velocity * (1f - (1f - t) * (1f - t)) * piece.Duration;
                    scale = Mathf.Lerp(0.5f, 1.4f, t);
                    alpha = DustColor.a * (t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f);
                    break;
                case Motion.Pop:
                    scale = t < 0.3f ? Mathf.Lerp(0.3f, 1.15f, t / 0.3f) : Mathf.Lerp(1.15f, 0.8f, (t - 0.3f) / 0.7f);
                    alpha = 1f - t * t;
                    break;
            }

            RectTransform rect = piece.Image.rectTransform;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, piece.Yaw + piece.Spin * seconds);
            rect.localScale = Vector3.one * scale;
            Color color = piece.Image.color;
            color.a = alpha;
            piece.Image.color = color;
        }

        // A point of a child rect, given in parts of it, in the wall's own units (centre origin).
        private Vector2 Local(RectTransform child, Vector2 part)
        {
            Rect rect = child.rect;
            Vector3 point = child.TransformPoint(new Vector3(rect.xMin + rect.width * part.x, rect.yMin + rect.height * part.y, 0f));
            return wall.InverseTransformPoint(point);
        }

        private Vector2 Centre(RectTransform child)
        {
            return Local(child, new Vector2(0.5f, 0.5f));
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
