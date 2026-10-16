using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The sheriff star pinned on the score tag of the player with the most money. The first leader
    // gets it spinning in with a burst of gold; while it stays it sways and glints now and then; when
    // someone takes the lead it jumps off the old tag, flies over in a spinning arc trailing gold and
    // slams down on the new one. A tie keeps it where it is; with no money on the board it is hidden.
    [RequireComponent(typeof(Image), typeof(CanvasGroup))]
    public sealed class LeaderStar : MonoBehaviour
    {
        [SerializeField] private PlayerScoreTag[] tags;
        // Waits this long after a score changes, so the count on the tag finishes first.
        [SerializeField, Min(0f)] private float changeDelay = 0.6f;

        [Header("Idle")]
        [SerializeField, Range(0f, 20f)] private float swayDegrees = 3f;
        [SerializeField, Min(0.2f)] private float swaySeconds = 2.4f;
        [SerializeField, Min(0.5f)] private float glintEverySeconds = 2.6f;

        [Header("Moves")]
        [SerializeField, Min(0.05f)] private float appearSeconds = 0.55f;
        [SerializeField, Min(0.05f)] private float flySeconds = 0.7f;
        // Height of the flight arc, in star sizes.
        [SerializeField, Min(0f)] private float arcHeight = 1.4f;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private const float GlintSeconds = 0.35f;

        private RectTransform rect;
        private RectTransform layer;
        private CanvasGroup group;
        private RectTransform glint;
        private PlayerScoreTag leader;
        private Coroutine moving;
        private float idleTime;
        private float nextGlintAt;

        private void Awake()
        {
            rect = (RectTransform)transform;
            layer = (RectTransform)transform.parent;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            GetComponent<Image>().raycastTarget = false;
            glint = CreateGlint();
            group.alpha = 0f;
        }

        private void OnEnable()
        {
            if (tags == null) return;
            foreach (PlayerScoreTag tag in tags)
            {
                if (tag != null) tag.ScoreChanged += OnScoreChanged;
            }
        }

        private void OnDisable()
        {
            if (tags == null) return;
            foreach (PlayerScoreTag tag in tags)
            {
                if (tag != null) tag.ScoreChanged -= OnScoreChanged;
            }
        }

        private void OnScoreChanged(PlayerScoreTag _)
        {
            CancelInvoke(nameof(Recheck));
            Invoke(nameof(Recheck), changeDelay);
        }

        private void Recheck()
        {
            PlayerScoreTag best = FindLeader();
            if (best == leader) return;

            PlayerScoreTag previous = leader;
            leader = best;
            if (moving != null) StopCoroutine(moving);
            if (best == null) moving = StartCoroutine(VanishRoutine());
            else if (previous == null) moving = StartCoroutine(AppearRoutine());
            else moving = StartCoroutine(FlyRoutine(previous));
            Debug.Log($"[LeaderStar] Leader is now {(best != null ? $"player {best.PlayerNumber}" : "nobody")}", this);
        }

        // The tag with the most money above zero; the current leader keeps the star in a tie.
        private PlayerScoreTag FindLeader()
        {
            PlayerScoreTag best = null;
            if (tags == null) return null;

            foreach (PlayerScoreTag tag in tags)
            {
                if (tag == null || tag.Score <= 0) continue;
                if (best == null || tag.Score > best.Score || (tag.Score == best.Score && tag == leader)) best = tag;
            }

            return best;
        }

        private void Update()
        {
            if (moving != null || leader == null) return;

            // Sways gently on its pin and breathes a little.
            idleTime += Time.deltaTime;
            float phase = idleTime * Mathf.PI * 2f / swaySeconds;
            Spot(leader, out Vector2 position, out float size);
            Pose(position, size, Mathf.Sin(phase) * swayDegrees, Vector2.one * (1f + 0.04f * Mathf.Sin(phase * 2f)), 1f);
            UpdateGlint();
        }

        // A short four-pointed glint on the star's top-left point every few seconds.
        private void UpdateGlint()
        {
            float since = Time.time - nextGlintAt;
            if (since < 0f)
            {
                glint.gameObject.SetActive(false);
                return;
            }

            if (since > GlintSeconds)
            {
                nextGlintAt = Time.time + glintEverySeconds * Random.Range(0.8f, 1.2f);
                return;
            }

            float t = since / GlintSeconds;
            glint.gameObject.SetActive(true);
            glint.anchoredPosition = new Vector2(-0.24f, 0.3f) * rect.sizeDelta.x;
            glint.localScale = Vector3.one * (Mathf.Sin(t * Mathf.PI) * rect.sizeDelta.x * 0.0045f);
            glint.localRotation = Quaternion.Euler(0f, 0f, 45f * t);
        }

        private IEnumerator AppearRoutine()
        {
            Spot(leader, out Vector2 position, out float size);
            bool burst = false;
            for (float time = 0f; time < appearSeconds; time += Time.deltaTime)
            {
                float u = time / appearSeconds;
                Pose(position, size, -200f * (1f - u) * (1f - u), Vector2.one * Overshoot(u), Mathf.Clamp01(u * 4f));
                if (!burst && u > 0.55f)
                {
                    burst = true;
                    PosterSpecks.Sparkle(layer, position, Gold, size);
                }

                yield return null;
            }

            Settle();
        }

        private IEnumerator FlyRoutine(PlayerScoreTag from)
        {
            glint.gameObject.SetActive(false);
            Spot(from, out Vector2 start, out float startSize);

            // Crouches before the jump.
            const float crouchSeconds = 0.14f;
            for (float time = 0f; time < crouchSeconds; time += Time.deltaTime)
            {
                float u = Mathf.Sin(time / crouchSeconds * Mathf.PI * 0.5f);
                Pose(start + Vector2.down * (startSize * 0.06f * u), startSize, 0f, new Vector2(1f + 0.15f * u, 1f - 0.18f * u), 1f);
                yield return null;
            }

            // Spins along an arc toward the new leader, trailing gold.
            float nextTrail = 0f;
            for (float time = 0f; time < flySeconds; time += Time.deltaTime)
            {
                float u = time / flySeconds;
                float eased = u * u * (3f - 2f * u);
                Spot(leader, out Vector2 end, out float endSize);
                float size = Mathf.Lerp(startSize, endSize, eased);
                Vector2 position = Vector2.Lerp(start, end, eased) + Vector2.up * (Mathf.Sin(eased * Mathf.PI) * arcHeight * size);
                float spin = (end.x >= start.x ? -360f : 360f) * eased;
                float swell = 1f + 0.3f * Mathf.Sin(eased * Mathf.PI);
                Pose(position, size, spin, Vector2.one * swell, 1f);
                if (time >= nextTrail)
                {
                    nextTrail = time + 0.06f;
                    PosterSpecks.Trail(layer, position, Gold, size);
                }

                yield return null;
            }

            // Slams down with a squash and a burst.
            Spot(leader, out Vector2 landing, out float landingSize);
            PosterSpecks.Sparkle(layer, landing, Gold, landingSize);
            const float landSeconds = 0.4f;
            for (float time = 0f; time < landSeconds; time += Time.deltaTime)
            {
                float u = time / landSeconds;
                float squash = Mathf.Cos(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u) * 0.25f;
                Pose(landing, landingSize, 0f, new Vector2(1f + squash, 1f - squash), 1f);
                yield return null;
            }

            Settle();
        }

        private IEnumerator VanishRoutine()
        {
            glint.gameObject.SetActive(false);
            const float vanishSeconds = 0.3f;
            Vector3 scale = rect.localScale;
            for (float time = 0f; time < vanishSeconds; time += Time.deltaTime)
            {
                float u = time / vanishSeconds;
                rect.localScale = scale * (1f - u * u);
                group.alpha = 1f - u;
                yield return null;
            }

            group.alpha = 0f;
            moving = null;
        }

        private void Settle()
        {
            idleTime = 0f;
            nextGlintAt = Time.time + 0.4f;
            moving = null;
        }

        // Where the star sits on a tag and how big it is, in the layer's units.
        private void Spot(PlayerScoreTag tag, out Vector2 position, out float size)
        {
            tag.GetBadge(out Vector3 world, out float worldSize);
            position = layer.InverseTransformPoint(world);
            size = worldSize / Mathf.Max(0.000001f, layer.lossyScale.x);
        }

        private void Pose(Vector2 position, float size, float degrees, Vector2 scale, float alpha)
        {
            rect.localPosition = new Vector3(position.x, position.y, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            rect.localScale = new Vector3(scale.x, scale.y, 1f);
            group.alpha = alpha;
        }

        // Two thin bright bars crossed into a sparkle, on top of the star.
        private RectTransform CreateGlint()
        {
            var holder = new GameObject("Glint", typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            AddBar(holder.transform, new Vector2(100f, 14f));
            AddBar(holder.transform, new Vector2(14f, 100f));
            holder.SetActive(false);
            return (RectTransform)holder.transform;
        }

        private static void AddBar(Transform parent, Vector2 size)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var image = bar.GetComponent<Image>();
            image.sprite = PosterSprites.Dot;
            image.color = new Color(1f, 1f, 0.92f, 1f);
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
        }

        // 0 to 1 with a springy overshoot past 1 on the way.
        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 2.2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }
    }
}
