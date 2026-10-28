using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The cut-in on the wall between the last stage and the boss chase, laid out in
    // Assets/Prefabs/Boss/Boss Transition.prefab (move, resize or recolour anything there). Timed on
    // a 2.8 s anime skill cut-in: black ink splats pop up over the stage, a red slash cuts across the
    // wall and tears open into an eye-shaped rift with a white flash, the rift bursts open to fill the
    // wall with the phantom thief's world (two banners slam in crossing each other, white doves
    // burst out and fly away from the boss's emblem, which glows and settles), it holds while the boss's rooftop
    // is set up behind it, then everything is pulled back into the rift, which closes to a slit and
    // fades, showing the rooftop. Runs on unscaled time, so it plays while the game is frozen.
    // MapSwitcher drives it: Open until the wall is covered, Close when the chase is ready.
    public sealed class BossTransition : MonoBehaviour
    {
        [Header("Pieces")]
        // Holds everything; it shakes while the rift tears open.
        [SerializeField] private RectTransform shaker;
        [SerializeField] private Image rift;
        // The world inside the rift, filling the wall once it has burst open.
        [SerializeField] private Image inside;
        [SerializeField] private Image flash;
        [SerializeField] private Image[] inks;
        // Each slides along its own length: in from alternate sides, drifting while it holds.
        [SerializeField] private RectTransform[] banners;
        // Round the emblem, each drawn flying up and turned to fly away from the emblem's middle.
        [FormerlySerializedAs("gloves")]
        [SerializeField] private RectTransform[] doves;
        [SerializeField] private RectTransform emblem;
        [SerializeField] private Image emblemGlow;

        [Header("Timing (seconds, from the reference cut-in at 25 fps)")]
        // Ink splats pop up one after another from inkAt, inkGap apart.
        [SerializeField, Min(0f)] private float inkAt = 0.08f;
        [SerializeField, Min(0f)] private float inkGap = 0.06f;
        // The slash cuts across from slashAt for slashSeconds (frames 5-9).
        [SerializeField, Min(0f)] private float slashAt = 0.2f;
        [SerializeField, Min(0.01f)] private float slashSeconds = 0.2f;
        // The slash tears open into the rift (frames 10-13).
        [SerializeField, Min(0.01f)] private float openSeconds = 0.16f;
        // The rift bursts open over the whole wall (frames 14-15), with a white flash either side.
        [SerializeField, Min(0.01f)] private float burstSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.12f;
        // The least time the wall stays covered (frames 16-54 plus a second, so the boss reads),
        // longer if the chase is not ready yet.
        [SerializeField, Min(0f)] private float holdSeconds = 2.56f;
        // Everything is pulled back into the rift (frames 55-57), which then closes and fades (58-67).
        [SerializeField, Min(0.01f)] private float collapseSeconds = 0.2f;
        [SerializeField, Min(0.01f)] private float closeSeconds = 0.32f;

        [Header("Motion")]
        // How much bigger than its own size the rift grows to cover the wall.
        [SerializeField, Min(1f)] private float riftCoverScale = 7f;
        // How thin the slash is, as a share of the rift's height.
        [SerializeField, Range(0.01f, 0.5f)] private float slashThickness = 0.05f;
        [SerializeField, Min(0f)] private float shake = 10f;
        // How far off the wall a banner starts and leaves (wall units; more than half the banner and
        // half the wall together), how fast it slams in, and how
        // fast it drifts while holding.
        [SerializeField, Min(0f)] private float bannerSlide = 3600f;
        [SerializeField, Min(0.01f)] private float bannerInSeconds = 0.12f;
        [SerializeField] private float bannerDrift = 45f;
        [SerializeField, Min(0.01f)] private float emblemPopSeconds = 0.28f;
        [FormerlySerializedAs("glovePopSeconds")]
        [SerializeField, Min(0.01f)] private float dovePopSeconds = 0.16f;
        [FormerlySerializedAs("gloveGap")]
        [SerializeField, Min(0f)] private float doveGap = 0.03f;
        // How far each dove tilts either way as it flaps (degrees), and how fast it flies away from
        // the emblem while the wall holds (wall units a second).
        [FormerlySerializedAs("gloveWiggle")]
        [SerializeField, Range(0f, 30f)] private float doveTilt = 7f;
        [SerializeField, Min(0f)] private float doveFlight = 40f;
        // The glow behind the emblem: bright as it lands, settling to glowRest and pulsing gently.
        [SerializeField, Range(0f, 1f)] private float glowRest = 0.45f;
        [SerializeField, Min(0.01f)] private float glowSettleSeconds = 0.5f;

        private enum Stage { Hidden, Opening, Holding, Closing }

        private Stage stage = Stage.Hidden;
        private float coveredAt;
        private Vector2 riftRest;
        private Vector3 riftSize;
        private Vector2[] bannerRests;
        private Vector3[] doveSizes;
        private Quaternion[] doveTurns;
        private Vector2[] doveRests;
        private Vector3[] inkSizes;
        private Vector3 emblemSize;
        private bool measured;

        // True once the prefab has its rift (Tools > Bounty Hunter > Build Boss Transition).
        public bool IsSetUp => rift != null && shaker != null;

        // The time from the start until the wall is covered.
        private float CoverAt => slashAt + slashSeconds + openSeconds + burstSeconds;

        // Plays the cut-in up to the moment the wall is fully covered, and keeps it covered.
        public IEnumerator Open()
        {
            gameObject.SetActive(true);
            Measure();
            stage = Stage.Opening;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < CoverAt)
            {
                PoseOpening(Time.unscaledTime - start);
                yield return null;
            }

            PoseOpening(CoverAt);
            coveredAt = Time.unscaledTime;
            stage = Stage.Holding;
        }

        // Waits out the rest of the hold, then pulls everything back into the rift and closes it.
        public IEnumerator Close()
        {
            while (stage == Stage.Holding && Time.unscaledTime < coveredAt + holdSeconds) yield return null;

            stage = Stage.Closing;
            float hold = Time.unscaledTime - coveredAt;
            float start = Time.unscaledTime;
            float length = collapseSeconds + closeSeconds;
            while (Time.unscaledTime - start < length)
            {
                PoseClosing(Time.unscaledTime - start, hold);
                yield return null;
            }

            Hide();
        }

        public void Hide()
        {
            stage = Stage.Hidden;
            if (measured) PoseClosing(collapseSeconds + closeSeconds, 0f);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (stage == Stage.Holding) PoseHolding(Time.unscaledTime - coveredAt);
        }

        // Where everything sits in the prefab, which the motion is measured from.
        private void Measure()
        {
            if (measured) return;

            measured = true;
            riftRest = rift.rectTransform.anchoredPosition;
            riftSize = rift.rectTransform.localScale;
            bannerRests = new Vector2[banners.Length];
            for (int i = 0; i < banners.Length; i++) bannerRests[i] = banners[i].anchoredPosition;
            doveSizes = new Vector3[doves.Length];
            doveTurns = new Quaternion[doves.Length];
            doveRests = new Vector2[doves.Length];
            for (int i = 0; i < doves.Length; i++)
            {
                doveSizes[i] = doves[i].localScale;
                doveTurns[i] = doves[i].localRotation;
                doveRests[i] = doves[i].anchoredPosition;
            }

            inkSizes = new Vector3[inks.Length];
            for (int i = 0; i < inks.Length; i++) inkSizes[i] = inks[i].rectTransform.localScale;
            emblemSize = emblem != null ? emblem.localScale : Vector3.one;
        }

        // From the stage still showing to the wall covered: ink, slash, tear, flash, burst.
        private void PoseOpening(float time)
        {
            for (int i = 0; i < inks.Length; i++)
            {
                float u = (time - inkAt - i * inkGap) / 0.15f;
                inks[i].gameObject.SetActive(u > 0f);
                inks[i].rectTransform.localScale = inkSizes[i] * EaseOutBack(u);
            }

            float slashEnd = slashAt + slashSeconds;
            float openEnd = slashEnd + openSeconds;
            rift.gameObject.SetActive(time >= slashAt);
            float across = Smooth((time - slashAt) / slashSeconds);
            float tear = Mathf.Lerp(slashThickness, 1f, EaseOutBack((time - slashEnd) / openSeconds));
            float burst = Smooth((time - openEnd) / burstSeconds);
            float grow = Mathf.Lerp(1f, riftCoverScale, burst * burst);
            rift.rectTransform.localScale = Vector3.Scale(riftSize, new Vector3(across * grow, tear * grow, 1f));
            rift.rectTransform.anchoredPosition = riftRest;
            SetAlpha(rift, 1f);

            SetVisible(inside, burst);
            SetVisible(flash, 1f - Mathf.Abs(time - openEnd) / flashSeconds);
            Shake(time >= slashAt ? shake : 0f);
            ShowCover(false);
        }

        // The phantom thief's world over the whole wall: banners slam in and drift, the emblem pops
        // and its glow settles, the doves burst out and fly away, tilting as they flap.
        private void PoseHolding(float since)
        {
            Shake(since < 0.15f ? shake * (1f - since / 0.15f) : 0f);
            // The flash carries on fading from where the burst left it.
            SetVisible(flash, 1f - (since + burstSeconds) / flashSeconds);
            ShowCover(true);
            for (int i = 0; i < banners.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float slide = (1f - EaseOut(since / bannerInSeconds)) * bannerSlide * side;
                banners[i].anchoredPosition = bannerRests[i] + Along(banners[i], slide + bannerDrift * since * -side);
            }

            if (emblem != null) emblem.localScale = emblemSize * EaseOutBack(since / emblemPopSeconds);
            if (emblemGlow != null)
            {
                float settle = Smooth(since / glowSettleSeconds);
                float pulse = 0.08f * Mathf.Sin(since * 6f) * settle;
                SetVisible(emblemGlow, Mathf.Lerp(1f, glowRest, settle) + pulse);
            }

            for (int i = 0; i < doves.Length; i++)
            {
                float flying = Mathf.Max(0f, since - i * doveGap);
                float pop = EaseOutBack(flying / dovePopSeconds);
                float tilt = doveTilt * Mathf.Sin(since * 7f + i * 1.7f);
                doves[i].localScale = doveSizes[i] * pop;
                doves[i].localRotation = doveTurns[i] * Quaternion.Euler(0f, 0f, tilt);
                doves[i].anchoredPosition = doveRests[i] + Away(doveRests[i]) * doveFlight * flying;
            }
        }

        // Banners fly off, the emblem and doves shrink away and the world fades back into the rift,
        // which shrinks to its own size, closes to a slit and fades.
        private void PoseClosing(float time, float held)
        {
            float collapse = Smooth(time / collapseSeconds);
            float close = Mathf.Clamp01((time - collapseSeconds) / closeSeconds);
            Shake(0f);
            SetVisible(flash, 0f);
            for (int i = 0; i < banners.Length; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 drifted = bannerRests[i] + Along(banners[i], bannerDrift * held * side);
                banners[i].anchoredPosition = drifted + Along(banners[i], collapse * collapse * bannerSlide * side);
            }

            if (emblem != null) emblem.localScale = emblemSize * (1f - collapse);
            if (emblemGlow != null) SetVisible(emblemGlow, glowRest * (1f - collapse));
            for (int i = 0; i < doves.Length; i++) doves[i].localScale = doveSizes[i] * (1f - collapse);
            SetVisible(inside, 1f - collapse);
            foreach (Image ink in inks) ink.gameObject.SetActive(false);

            float grow = Mathf.Lerp(riftCoverScale, 1f, collapse);
            float shut = Mathf.Lerp(1f, slashThickness, Smooth(close / 0.4f));
            float thin = Mathf.Lerp(1f, 0.85f, close);
            rift.gameObject.SetActive(close < 1f);
            rift.rectTransform.localScale = Vector3.Scale(riftSize, new Vector3(grow * thin, grow * shut, 1f));
            SetAlpha(rift, 1f - Smooth((close - 0.4f) / 0.6f));
            if (close >= 1f) ShowCover(false);
        }

        // Switches the banners, the emblem and the doves on or off together.
        private void ShowCover(bool shown)
        {
            foreach (RectTransform banner in banners) banner.gameObject.SetActive(shown);
            foreach (RectTransform dove in doves) dove.gameObject.SetActive(shown);
            if (emblem != null) emblem.gameObject.SetActive(shown);
            if (emblemGlow != null) emblemGlow.gameObject.SetActive(shown);
        }

        private void Shake(float amount)
        {
            if (shaker == null) return;
            shaker.anchoredPosition = amount > 0f ? Random.insideUnitCircle * amount : Vector2.zero;
        }

        // The way out from the middle of the wall through point.
        private static Vector2 Away(Vector2 point)
        {
            return point.sqrMagnitude > 0.01f ? point.normalized : Vector2.up;
        }

        // distance along the piece's own length, in its parent's units.
        private static Vector2 Along(RectTransform piece, float distance)
        {
            return (Vector2)(piece.localRotation * Vector3.right) * distance;
        }

        // Shows the picture at alpha (clamped), switching it off when fully clear.
        private static void SetVisible(Image picture, float alpha)
        {
            if (picture == null) return;

            alpha = Mathf.Clamp01(alpha);
            picture.gameObject.SetActive(alpha > 0f);
            SetAlpha(picture, alpha);
        }

        private static void SetAlpha(Image picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float EaseOut(float u)
        {
            u = Mathf.Clamp01(u);
            return 1f - (1f - u) * (1f - u) * (1f - u);
        }

        private static float EaseOutBack(float u)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(u) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
