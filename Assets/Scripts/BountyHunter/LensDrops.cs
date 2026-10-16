using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Raindrops landing on the camera's glass in front of the intro wall: beads of water pop up here
    // and there and swell a touch; after a while each either fades or runs down the glass, faster
    // and faster, turning into a sliding teardrop with a wavy wet trail behind it that dries away.
    // Pictures keep their own shapes. Stretch it over the wall; sizes are in parts of its height.
    public sealed class LensDrops : MonoBehaviour
    {
        // A resting bead, a running teardrop and the trail it leaves (drawn by Codex).
        [SerializeField] private Texture drop;
        [SerializeField] private Texture slidingDrop;
        [SerializeField] private Texture trail;
        [SerializeField, Range(1, 60)] private int most = 12;
        // Seconds between new drops landing, picked in this range.
        [SerializeField] private Vector2 landEvery = new Vector2(0.35f, 1.1f);
        // Width of a resting drop.
        [SerializeField] private Vector2 size = new Vector2(0.016f, 0.036f);
        [SerializeField] private Vector2 lifeSeconds = new Vector2(2.5f, 6f);
        // Chance a drop runs down the glass instead of just fading.
        [SerializeField, Range(0f, 1f)] private float runChance = 0.4f;
        // Kept low so the drops stay in the background of what the eye follows.
        [SerializeField, Range(0f, 1f)] private float opacity = 0.6f;

        private const float SwellSeconds = 0.25f;
        private const float FadeSeconds = 0.6f;
        // How long a run takes to change the bead into a teardrop.
        private const float ShapeSeconds = 0.25f;

        private sealed class Bead
        {
            public RawImage Picture;
            public RawImage Running;
            public RawImage Trail;
            public Vector2 Start;
            public float Size;
            public float Tilt;
            public float Age;
            public float Life;
            // When it starts to run, or never.
            public float RunAt;
        }

        private readonly List<Bead> beads = new List<Bead>();
        private readonly Stack<Bead> spare = new Stack<Bead>();
        private RectTransform area;
        private float nextLanding;

        private void Awake()
        {
            area = (RectTransform)transform;
        }

        private void Update()
        {
            nextLanding -= Time.deltaTime;
            if (nextLanding <= 0f && beads.Count < most)
            {
                Land();
                nextLanding = Random.Range(landEvery.x, landEvery.y);
            }

            for (int i = beads.Count - 1; i >= 0; i--)
            {
                Bead bead = beads[i];
                bead.Age += Time.deltaTime;
                if (bead.Age >= bead.Life)
                {
                    Dry(bead);
                    beads.RemoveAt(i);
                    continue;
                }

                Pose(bead);
            }
        }

        private void Land()
        {
            Bead bead = spare.Count > 0 ? spare.Pop() : Create();
            bead.Start = new Vector2(Random.value, Random.Range(0.15f, 1f));
            bead.Size = Random.Range(size.x, size.y);
            bead.Tilt = Random.Range(-15f, 15f);
            bead.Age = 0f;
            bead.Life = Random.Range(lifeSeconds.x, lifeSeconds.y);
            bead.RunAt = Random.value < runChance ? bead.Life * Random.Range(0.25f, 0.5f) : float.MaxValue;
            bead.Picture.gameObject.SetActive(true);
            beads.Add(bead);
            Pose(bead);
        }

        // Trail first, so the drops sit on top of it.
        private Bead Create()
        {
            return new Bead
            {
                Trail = CreatePicture("Trail", trail),
                Running = CreatePicture("Running Drop", slidingDrop),
                Picture = CreatePicture("Drop", drop)
            };
        }

        private RawImage CreatePicture(string pictureName, Texture texture)
        {
            var pictureObject = new GameObject(pictureName, typeof(RectTransform), typeof(RawImage));
            pictureObject.transform.SetParent(transform, false);
            var picture = pictureObject.GetComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            var rect = picture.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            pictureObject.SetActive(false);
            return picture;
        }

        private void Pose(Bead bead)
        {
            Rect rect = area.rect;
            float height = rect.height;
            float width = bead.Size * height * Mathf.SmoothStep(0.6f, 1f, bead.Age / SwellSeconds);
            float alpha = opacity * Mathf.Clamp01((bead.Life - bead.Age) / FadeSeconds) * Mathf.Clamp01(bead.Age / 0.08f);

            // Running: gathers speed down the glass.
            float run = 0f;
            float runTime = bead.Age - bead.RunAt;
            if (runTime > 0f) run = 0.25f * runTime * runTime + 0.05f * runTime;

            var start = new Vector2(bead.Start.x * rect.width, bead.Start.y * height);
            Vector2 centre = start + Vector2.down * (run * height);

            // The bead turns into a teardrop as it starts to run.
            float shape = runTime > 0f ? Mathf.Clamp01(runTime / ShapeSeconds) : 0f;
            Place(bead.Picture, centre, width, bead.Tilt * (1f - shape), alpha * (1f - shape));
            // The teardrop is tall for its width, so it is drawn narrower to stay about as big.
            Place(bead.Running, centre, width * 0.55f, 0f, alpha * shape);

            bool running = run > 0f;
            bead.Trail.gameObject.SetActive(running);
            if (!running) return;

            // The trail runs from where the drop landed down to it; the picture is not stretched,
            // only as much of it is shown as the trail is long (its newest end at the drop).
            float trailLength = run * height;
            float trailWidth = width * 0.3f;
            float pictureLength = trail != null ? trailWidth * trail.height / Mathf.Max(1f, trail.width) : trailLength;
            RectTransform trailRect = bead.Trail.rectTransform;
            trailRect.anchoredPosition = centre + Vector2.up * (trailLength * 0.5f);
            trailRect.sizeDelta = new Vector2(trailWidth, trailLength);
            bead.Trail.uvRect = new Rect(0f, 0f, 1f, Mathf.Min(1f, trailLength / pictureLength));
            bead.Trail.color = new Color(1f, 1f, 1f, alpha * 0.8f);
        }

        // Shows a picture at its own shape, width wide, centred on centre.
        private static void Place(RawImage picture, Vector2 centre, float width, float tilt, float alpha)
        {
            bool visible = alpha > 0.001f;
            picture.gameObject.SetActive(visible);
            if (!visible) return;

            Texture texture = picture.texture;
            float aspect = texture != null ? texture.height / (float)Mathf.Max(1, texture.width) : 1f;
            RectTransform rect = picture.rectTransform;
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(width, width * aspect);
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            picture.color = new Color(1f, 1f, 1f, alpha);
        }

        private void Dry(Bead bead)
        {
            bead.Picture.gameObject.SetActive(false);
            bead.Running.gameObject.SetActive(false);
            bead.Trail.gameObject.SetActive(false);
            spare.Push(bead);
        }
    }
}
