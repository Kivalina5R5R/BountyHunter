using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The cut-in on the wall between the last stage and the boss chase, laid out in
    // Assets/Prefabs/Boss/Boss Transition.prefab (move, resize, recolour or retime anything there):
    // pigeon feathers start falling over the stage, then the boss's moonlit rooftop fades in and the
    // blue streak cracks across it from the right like a bolt of lightning; red dots pop up at random
    // on the streak where it has reached. With the wall covered, the boss pops
    // up in the middle and the pigeons fly in round him one after another, hovering there flapping,
    // while the boss's own rooftop is set up behind. Then it all goes back out: the boss shrinks away,
    // the pigeons fly off, the dots pop, the streak wipes back off to the right and the rooftop fades,
    // showing the chase. The feathers keep falling through all of it and on over the chase a while,
    // the last of them falling out on their own. Runs on unscaled time, so it plays while the game is
    // frozen. MapSwitcher drives it: Open until the wall is covered, Close when the chase is ready.
    public sealed class BossTransition : MonoBehaviour
    {
        [Header("Pieces")]
        [SerializeField] private Image background;
        // Filled, horizontal, from the right: the wipe is its fill.
        [SerializeField] private Image streak;
        [SerializeField] private RectTransform boss;
        // Each faces the way its picture is turned (the picture faces left; a pigeon with its x scale
        // below zero faces right) and flies in from behind.
        [SerializeField] private RectTransform[] pigeons;
        [SerializeField] private BossFeatherFall feathers;
        [SerializeField] private BossDotPops dots;

        [Header("Opening (seconds)")]
        // Feathers fall over the stage this long before the rooftop starts fading in.
        [SerializeField, Min(0f)] private float feathersFirstSeconds = 1f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.4f;
        // The streak strikes across this long after the rooftop starts fading in, taking wipeSeconds,
        // fast at first and slowing at the end like a bolt.
        [SerializeField, Min(0f)] private float wipeAfter = 0.25f;
        [SerializeField, Min(0.02f)] private float wipeSeconds = 0.15f;
        [SerializeField, Min(0.05f)] private float bossPopSeconds = 0.35f;
        // The pigeons fly in pigeonGap apart, starting pigeonsAfter the boss starts.
        [SerializeField, Min(0f)] private float pigeonsAfter = 0.1f;
        [SerializeField, Min(0f)] private float pigeonGap = 0.15f;
        [SerializeField, Min(0.05f)] private float flyInSeconds = 0.6f;
        // The least time the wall stays covered, longer if the chase is not ready yet.
        [SerializeField, Min(0f)] private float holdSeconds = 2.5f;

        [Header("Closing (seconds)")]
        [SerializeField, Min(0.05f)] private float shrinkSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float flyOffSeconds = 0.5f;
        // The streak wipes back streakBackAfter the closing starts, the rooftop fading after it.
        [SerializeField, Min(0f)] private float streakBackAfter = 0.1f;
        [SerializeField, Min(0.05f)] private float streakBackSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.3f;
        // Feathers keep coming this long over the chase before they stop.
        [SerializeField, Min(0f)] private float feathersAfterSeconds = 1.5f;

        [Header("Flying")]
        // Where a pigeon flies in from, from its spot: this far behind it and this far up.
        [SerializeField] private Vector2 flyInFrom = new Vector2(900f, 300f);
        // Where it flies off to, from its spot: this far ahead and this far up.
        [SerializeField] private Vector2 flyOffTo = new Vector2(350f, 650f);
        // Wing beats a second while flying in or off, and while hovering.
        [SerializeField, Min(0f)] private float flapsPerSecond = 7f;
        [SerializeField, Min(0f)] private float hoverFlapsPerSecond = 4f;
        // How much a wing beat squashes the picture up and down (0 to 1).
        [SerializeField, Range(0f, 0.6f)] private float flapSquash = 0.25f;
        // How far a hovering pigeon rises and sinks (wall units) and how often a second.
        [SerializeField, Min(0f)] private float hoverHeight = 10f;
        [SerializeField, Min(0f)] private float hoversPerSecond = 1.1f;
        // How far it tips its head down when swooping in and up when flying off (degrees).
        [SerializeField, Range(0f, 40f)] private float flyTilt = 15f;
        [SerializeField, Min(0f)] private float bossBob = 5f;

        private enum Stage { Hidden, Opening, Holding, Closing, FeathersOnly }

        private Stage stage = Stage.Hidden;
        private float wipeAt;
        private float coveredAt;
        private float feathersStopAt;
        private float backgroundAlpha;
        private Vector2 bossRest;
        private Vector3 bossSize;
        private Vector2[] pigeonRests;
        private Vector3[] pigeonSizes;
        private bool measured;

        // True once the prefab has its pieces (Tools > Bounty Hunter > Build Boss Transition).
        public bool IsSetUp => background != null && streak != null;

        private float WipeStart => feathersFirstSeconds + wipeAfter;

        // The time from the start until the wall is covered.
        private float CoverAt => feathersFirstSeconds + Mathf.Max(fadeInSeconds, wipeAfter + wipeSeconds);

        private float CloseLength => Mathf.Max(Mathf.Max(shrinkSeconds, flyOffSeconds), streakBackAfter + streakBackSeconds + fadeSeconds);

        // Plays the cut-in up to the moment the wall is fully covered, and keeps it covered.
        public IEnumerator Open()
        {
            gameObject.SetActive(true);
            Measure();
            stage = Stage.Opening;
            ShowBoss(0f, 0f);
            for (int i = 0; i < pigeons.Length; i++) pigeons[i].gameObject.SetActive(false);
            if (feathers != null) feathers.StartFalling();

            bool dotsStarted = false;
            float start = Time.unscaledTime;
            wipeAt = start + WipeStart;
            while (Time.unscaledTime - start < CoverAt)
            {
                float since = Time.unscaledTime - start;
                if (!dotsStarted && since >= WipeStart && dots != null)
                {
                    dots.StartPopping();
                    dotsStarted = true;
                }

                SetAlpha(background, backgroundAlpha * Smooth((since - feathersFirstSeconds) / fadeInSeconds));
                PoseStreak(EaseOutCubic((Time.unscaledTime - wipeAt) / wipeSeconds));
                yield return null;
            }

            SetAlpha(background, backgroundAlpha);
            PoseStreak(1f);
            if (dots != null && !dotsStarted) dots.StartPopping();
            coveredAt = Time.unscaledTime;
            stage = Stage.Holding;
        }

        // Waits out the rest of the hold, then sends everything back out the way it came. Returns
        // with the chase showing; the feathers keep falling over it on their own.
        public IEnumerator Close()
        {
            while (stage == Stage.Holding && Time.unscaledTime < coveredAt + holdSeconds) yield return null;

            stage = Stage.Closing;
            float held = Time.unscaledTime - coveredAt;
            if (dots != null) dots.PopAll();
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < CloseLength)
            {
                PoseClosing(Time.unscaledTime - start, held);
                yield return null;
            }

            LeaveFeathers();
        }

        public void Hide()
        {
            stage = Stage.Hidden;
            if (feathers != null) feathers.Clear();
            if (dots != null) dots.Clear();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (stage == Stage.Holding)
            {
                PoseHolding(now - coveredAt);
                return;
            }

            if (stage != Stage.FeathersOnly) return;

            if (feathers == null)
            {
                Hide();
                return;
            }

            if (now >= feathersStopAt) feathers.StopFalling();
            if (now >= feathersStopAt && feathers.IsEmpty) Hide();
        }

        // Everything but the feathers is off the wall; they keep coming a while, then the last of
        // them fall out and the cut-in switches itself off.
        private void LeaveFeathers()
        {
            ShowBoss(0f, 0f);
            for (int i = 0; i < pigeons.Length; i++) pigeons[i].gameObject.SetActive(false);
            if (dots != null) dots.Clear();
            streak.fillAmount = 0f;
            SetAlpha(background, 0f);
            feathersStopAt = Time.unscaledTime + feathersAfterSeconds;
            stage = Stage.FeathersOnly;
        }

        // Where everything sits in the prefab, which the motion is measured from.
        private void Measure()
        {
            if (measured) return;

            measured = true;
            // The rooftop fades rather than wipes, so it is always drawn whole.
            background.type = Image.Type.Simple;
            backgroundAlpha = background.color.a;
            if (boss != null)
            {
                bossRest = boss.anchoredPosition;
                bossSize = boss.localScale;
            }

            pigeons ??= new RectTransform[0];
            pigeonRests = new Vector2[pigeons.Length];
            pigeonSizes = new Vector3[pigeons.Length];
            for (int i = 0; i < pigeons.Length; i++)
            {
                pigeonRests[i] = pigeons[i].anchoredPosition;
                pigeonSizes[i] = pigeons[i].localScale;
            }
        }

        // The streak share of the way across (0 to 1), and the dots only where it has reached.
        private void PoseStreak(float share)
        {
            streak.fillAmount = share;
            if (dots != null) dots.SetReach(share);
        }

        // The boss pops up and bobs, the pigeons fly in one after another and hover.
        private void PoseHolding(float since)
        {
            ShowBoss(EaseOutBack(since / bossPopSeconds), since);
            for (int i = 0; i < pigeons.Length; i++)
            {
                float flying = (since - pigeonsAfter - i * pigeonGap) / flyInSeconds;
                PosePigeon(i, flying, 0f, since);
            }
        }

        // Everything back out: the boss shrinks away, the pigeons fly off, the streak wipes back off
        // to the right, the rooftop fades.
        private void PoseClosing(float time, float held)
        {
            ShowBoss(1f - Smooth(time / shrinkSeconds), held + time);
            for (int i = 0; i < pigeons.Length; i++) PosePigeon(i, 1f, time / flyOffSeconds, held + time);

            PoseStreak(1f - Smooth((time - streakBackAfter) / streakBackSeconds));
            float fade = Smooth((time - streakBackAfter - streakBackSeconds) / fadeSeconds);
            SetAlpha(background, backgroundAlpha * (1f - fade));
        }

        // The boss at size share of its own (0 hides it), bobbing time seconds in.
        private void ShowBoss(float size, float time)
        {
            if (boss == null) return;

            boss.gameObject.SetActive(size > 0f);
            boss.localScale = bossSize * size;
            boss.anchoredPosition = bossRest + new Vector2(0f, bossBob * Mathf.Sin(time * 2.5f));
        }

        // Pigeon i: flyingIn 0 to 1 on its way in (0 or less, not here yet), flyingOff 0 to 1 on its
        // way off (1 or more, gone), hovering and flapping time seconds in.
        private void PosePigeon(int i, float flyingIn, float flyingOff, float time)
        {
            RectTransform pigeon = pigeons[i];
            bool there = flyingIn > 0f && flyingOff < 1f;
            pigeon.gameObject.SetActive(there);
            if (!there) return;

            // The picture faces left, so a pigeon turned over on x faces right.
            float facing = pigeonSizes[i].x < 0f ? 1f : -1f;
            float beat = time * Mathf.PI * 2f + i * 1.7f;
            Vector2 hover = new Vector2(0f, hoverHeight * Mathf.Sin(beat * hoversPerSecond));

            float inU = Mathf.Clamp01(flyingIn);
            float offU = Mathf.Clamp01(flyingOff);
            // Swoops in from behind and above: across fast, settling down slower, so it curves in.
            Vector2 from = new Vector2(-facing * flyInFrom.x, flyInFrom.y);
            Vector2 swoop = new Vector2(from.x * (1f - EaseOutCubic(inU)), from.y * (1f - Smooth(inU)));
            // Takes off ahead and up, picking up speed.
            Vector2 takeOff = new Vector2(facing * flyOffTo.x * offU * offU, flyOffTo.y * offU * offU);
            pigeon.anchoredPosition = pigeonRests[i] + swoop + takeOff + hover;

            // Head down coming in, up going off, level while hovering.
            float tilt = flyTilt * (offU - (1f - inU));
            pigeon.localRotation = Quaternion.Euler(0f, 0f, facing * tilt);

            bool moving = inU < 1f || offU > 0f;
            float flaps = moving ? flapsPerSecond : hoverFlapsPerSecond;
            float wing = 0.5f + 0.5f * Mathf.Sin(time * flaps * Mathf.PI * 2f + i * 1.3f);
            float grow = Smooth(inU * 3f);
            Vector3 size = pigeonSizes[i] * grow;
            size.y *= 1f - flapSquash * wing;
            pigeon.localScale = size;
        }

        private static void SetAlpha(Graphic picture, float alpha)
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

        private static float EaseOutCubic(float u)
        {
            float rest = 1f - Mathf.Clamp01(u);
            return 1f - rest * rest * rest;
        }

        private static float EaseOutBack(float u)
        {
            if (u <= 0f) return 0f;

            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(u) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
