using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A stage spotlight on the result screen's winner: once the winner's score has counted up, a
    // warm beam clunks on from the top of the wall straight down onto the bench (a stutter or two,
    // like an old stage lamp warming up, then on), with a pool of light round the winner's feet and
    // a few motes of dust drifting in it. The beam sways a touch and breathes gently while it stays
    // on. Drawn behind the bench and the winner, so they stand in it. Off again whenever the result
    // comes up anew. Its pictures are drawn here, so there is nothing to import. ResultSpotlightAutoWire
    // adds it to the result prefab.
    [RequireComponent(typeof(ResultScreen))]
    public sealed class WinnerSpotlight : MonoBehaviour
    {
        private const int BeamTextureWidth = 64;
        private const int BeamTextureHeight = 256;
        private const int PoolTextureSize = 128;
        private const int MoteTextureSize = 32;
        // The beam picture's width at its top against its foot (BeamSprite draws it so).
        private const float BeamTopShare = 0.3f;

        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.78f, 1f);
        // How strong the beam and the pool are at full.
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.38f;
        [SerializeField, Range(0f, 1f)] private float poolAlpha = 0.55f;
        // Sizes against the winner's picture width: the beam at its wide foot, and the pool.
        [SerializeField, Min(0.1f)] private float beamFootVsWinner = 1.5f;
        [SerializeField, Min(0.1f)] private float poolVsWinner = 1.7f;
        // The stutter as it comes on: on/off steps (seconds, starting on), then a quick rise to full.
        [SerializeField] private float[] stutter = { 0.06f, 0.09f, 0.05f, 0.14f };
        [SerializeField, Min(0.01f)] private float riseSeconds = 0.25f;
        // The sway either way (degrees) and the breathing (share of full) while it stays on.
        [SerializeField, Range(0f, 5f)] private float swayDegrees = 1.2f;
        [SerializeField, Range(0f, 0.3f)] private float breathe = 0.08f;
        [SerializeField, Min(0)] private int motes = 10;

        private static Sprite beamSprite;
        private static Sprite poolSprite;
        private static Sprite moteSprite;

        private ResultScreen screen;
        private RectTransform beam;
        private Image beamImage;
        private RectTransform pool;
        private Image poolImage;
        private RectTransform[] moteRects;
        private Image[] moteImages;
        private Vector2[] moteSeeds;
        private float strength;
        private float onSince;
        private bool on;
        private Coroutine switching;

        private void Awake()
        {
            screen = GetComponent<ResultScreen>();
            screen.Showing += SwitchOff;
            screen.ScoreCounted += OnScoreCounted;
        }

        private void OnDestroy()
        {
            if (screen == null) return;
            screen.Showing -= SwitchOff;
            screen.ScoreCounted -= OnScoreCounted;
        }

        // The winner's row is the first: its score counted means the winner is known and in place.
        private void OnScoreCounted(int place)
        {
            if (place != 1 || on) return;

            RectTransform winner = screen.Winner;
            if (winner == null) return;

            Build(winner);
            on = true;
            if (switching != null) StopCoroutine(switching);
            switching = StartCoroutine(ComeOn());
        }

        private void SwitchOff()
        {
            on = false;
            strength = 0f;
            if (switching != null) StopCoroutine(switching);
            switching = null;
            Show(0f);
        }

        private IEnumerator ComeOn()
        {
            bool lit = true;
            foreach (float step in stutter)
            {
                strength = lit ? 0.7f : 0f;
                Show(strength);
                yield return new WaitForSeconds(step);
                lit = !lit;
            }

            for (float t = 0f; t < riseSeconds; t += Time.deltaTime)
            {
                strength = Mathf.SmoothStep(0f, 1f, t / riseSeconds);
                Show(strength);
                yield return null;
            }

            strength = 1f;
            onSince = Time.time;
            switching = null;
        }

        private void Update()
        {
            if (!on || beam == null) return;

            float age = Time.time - onSince;
            if (switching == null) Show(strength * (1f - breathe * 0.5f + breathe * 0.5f * Mathf.Sin(age * 2.1f)));
            beam.localRotation = Quaternion.Euler(0f, 0f, swayDegrees * Mathf.Sin(Time.time * 0.7f));
            DriftMotes();
        }

        // Puts the beam from the top of the screen down onto the winner's feet, behind the bench.
        private void Build(RectTransform winner)
        {
            var stand = (RectTransform)winner.parent;
            float width = winner.rect.width * winner.localScale.x;
            Vector2 feet = winner.anchoredPosition;
            float top = TopOfScreen(stand);
            float footWidth = width * beamFootVsWinner;

            if (beam == null)
            {
                beamImage = Picture("Spotlight Beam", stand, BeamSprite());
                beam = beamImage.rectTransform;
                beam.pivot = new Vector2(0.5f, 1f);
                poolImage = Picture("Spotlight Pool", stand, PoolSprite());
                pool = poolImage.rectTransform;
                BuildMotes(stand);
            }

            // Behind everything on the stand (the bench and the winner), the pool just over the beam.
            beam.SetSiblingIndex(0);
            pool.SetSiblingIndex(1);
            beam.anchoredPosition = new Vector2(feet.x, top);
            beam.sizeDelta = new Vector2(footWidth, Mathf.Max(10f, top - feet.y + width * 0.15f));
            pool.anchoredPosition = feet + new Vector2(0f, width * 0.05f);
            pool.sizeDelta = new Vector2(width * poolVsWinner, width * poolVsWinner * 0.28f);

            for (int i = 0; i < moteSeeds.Length; i++) moteSeeds[i] = new Vector2(Random.value, Random.value);
        }

        private void BuildMotes(RectTransform stand)
        {
            moteRects = new RectTransform[motes];
            moteImages = new Image[motes];
            moteSeeds = new Vector2[motes];
            for (int i = 0; i < motes; i++)
            {
                moteImages[i] = Picture("Spotlight Mote", beam, MoteSprite());
                moteRects[i] = moteImages[i].rectTransform;
                float size = Random.Range(4f, 9f);
                moteRects[i].sizeDelta = new Vector2(size, size);
                moteRects[i].anchorMin = moteRects[i].anchorMax = new Vector2(0.5f, 1f);
            }
        }

        // Each mote drifts slowly down and sideways inside the cone, wrapping round, twinkling a little.
        private void DriftMotes()
        {
            if (moteRects == null) return;

            Vector2 size = beam.sizeDelta;
            for (int i = 0; i < moteRects.Length; i++)
            {
                Vector2 seed = moteSeeds[i];
                float down = Mathf.Repeat(seed.y + Time.time * (0.03f + 0.04f * seed.x), 1f);
                // Kept to the lower part of the beam, where the cone is wide, and inside its width there.
                float along = Mathf.Lerp(0.3f, 0.95f, down);
                float halfWidth = Mathf.Lerp(BeamTopShare, 1f, along) * 0.42f * size.x;
                float side = Mathf.Sin(Time.time * (0.4f + seed.x) + seed.y * 6.3f) * halfWidth * (seed.x * 2f - 1f);
                moteRects[i].anchoredPosition = new Vector2(side, -along * size.y);
                float twinkle = 0.5f + 0.5f * Mathf.Sin(Time.time * (1.5f + seed.y * 2f) + seed.x * 9f);
                SetAlpha(moteImages[i], strength * (0.25f + 0.45f * twinkle) * Mathf.Sin(down * Mathf.PI));
            }
        }

        private void Show(float share)
        {
            if (beamImage != null) SetAlpha(beamImage, beamAlpha * share);
            if (poolImage != null) SetAlpha(poolImage, poolAlpha * share);
            if (moteImages == null || share > 0f) return;
            foreach (Image mote in moteImages) SetAlpha(mote, 0f);
        }

        private void SetAlpha(Image picture, float alpha)
        {
            Color shown = colour;
            shown.a = alpha;
            picture.color = shown;
        }

        // The top edge of the screen (the result's root) in the stand's own units.
        private float TopOfScreen(RectTransform stand)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return stand.InverseTransformPoint(corners[1]).y;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            var picture = holder.GetComponent<Image>();
            picture.sprite = sprite;
            picture.raycastTarget = false;
            picture.color = Color.clear;
            return picture;
        }

        // A cone of light, narrow at the top and widening down, soft at its sides, fading out
        // towards the lamp at the top and bright at the foot.
        private static Sprite BeamSprite()
        {
            if (beamSprite != null) return beamSprite;

            var pixels = new Color32[BeamTextureWidth * BeamTextureHeight];
            for (int y = 0; y < BeamTextureHeight; y++)
            {
                // 0 at the foot (bottom row), 1 at the lamp.
                float up = (y + 0.5f) / BeamTextureHeight;
                float halfWidth = Mathf.Lerp(0.5f, 0.5f * BeamTopShare, up);
                float strength = Mathf.Lerp(1f, 0.25f, up * up);
                for (int x = 0; x < BeamTextureWidth; x++)
                {
                    float across = Mathf.Abs((x + 0.5f) / BeamTextureWidth - 0.5f);
                    float edge = Mathf.Clamp01((halfWidth - across) / (halfWidth * 0.45f));
                    pixels[y * BeamTextureWidth + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(edge * edge * strength * 255f));
                }
            }

            beamSprite = MakeSprite("Spotlight Beam", BeamTextureWidth, BeamTextureHeight, pixels);
            return beamSprite;
        }

        // A soft round pool, brightest in the middle.
        private static Sprite PoolSprite()
        {
            if (poolSprite != null) return poolSprite;
            poolSprite = MakeSprite("Spotlight Pool", PoolTextureSize, PoolTextureSize, Blob(PoolTextureSize, 1.2f));
            return poolSprite;
        }

        private static Sprite MoteSprite()
        {
            if (moteSprite != null) return moteSprite;
            moteSprite = MakeSprite("Spotlight Mote", MoteTextureSize, MoteTextureSize, Blob(MoteTextureSize, 2f));
            return moteSprite;
        }

        private static Color32[] Blob(int size, float falloff)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float fade = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * 255f));
                }
            }

            return pixels;
        }

        private static Sprite MakeSprite(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
