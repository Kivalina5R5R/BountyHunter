using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The cut-in on the wall between the last stage and the boss chase, laid out in
    // Assets/Prefabs/Boss/Boss Transition.prefab (move, resize, recolour or retime anything there):
    // pigeon feathers start falling over the stage, then the boss's moonlit rooftop fades in and the
    // blue streak cracks across it from the right like a bolt of lightning; red dots pop up at random
    // on the streak where it has reached. With the wall covered, the boss pops up in the middle and
    // the pigeons burst out of him one after another, each flying out past its own spot round him
    // (its place in the prefab sets the way it goes) and on off the wall, while the boss's own
    // rooftop is set up behind. The feathers stop coming while the wall is covered and start again
    // once the pigeons are halfway off. Then it all goes back out: the boss shrinks away, the dots
    // pop, the streak wipes back off to the right and the rooftop fades, showing the chase. The
    // feathers keep falling on over the chase a while, the last of them falling out on their own. Runs on unscaled time, so it plays while the game is
    // frozen. MapSwitcher drives it: Open until the wall is covered, Close when the chase is ready.
    // With a BossSpotlight in the prefab, that opens and closes it instead of the streak's strike
    // and the fade: a darkened wall, slashes, the boss's eyes in a band, and the dark lifting off the
    // rooftop and the boss; bands of colour sweeping it all away at the end.
    public sealed class BossTransition : MonoBehaviour
    {
        [Header("Pieces")]
        [SerializeField] private Image background;
        // Filled, horizontal, from the right: the wipe is its fill.
        [SerializeField] private Image streak;
        [SerializeField] private RectTransform boss;
        // Each flies out of the boss towards where it sits here and on off the wall, facing the way
        // it flies (the picture faces left and is turned over on x to face right).
        [SerializeField] private RectTransform[] pigeons;
        [SerializeField] private BossFeatherFall feathers;
        [SerializeField] private BossDotPops dots;
        // The entrance in the way of a character spotlight (slashes, the boss's eyes in a band, the
        // dark lifting off the rooftop; bands of colour sweeping it all away at the end) in place of
        // the streak's strike; may be empty, for the strike.
        [SerializeField] private BossSpotlight spotlight;

        [Header("Opening (seconds)")]
        // Feathers fall over the stage this long before the rooftop starts fading in.
        [SerializeField, Min(0f)] private float feathersFirstSeconds = 1f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.4f;
        // The streak strikes across this long after the rooftop starts fading in, taking wipeSeconds,
        // fast at first and slowing at the end like a bolt.
        [SerializeField, Min(0f)] private float wipeAfter = 0.25f;
        [SerializeField, Min(0.02f)] private float wipeSeconds = 0.15f;
        [SerializeField, Min(0.05f)] private float bossPopSeconds = 0.35f;
        // With the spotlight: once the rooftop shows, the streak runs in from the right all the way
        // across in one go over this long, quick at first and slowing, and the boss pops up the moment
        // it is halfway.
        [SerializeField, Min(0.05f)] private float streakRunSeconds = 0.6f;
        // The pigeons burst out of the boss pigeonGap apart, starting pigeonsAfter he has popped up.
        [SerializeField, Min(0f)] private float pigeonsAfter = 0.1f;
        [SerializeField, Min(0f)] private float pigeonGap = 0.15f;
        // Each pigeon shoots out of the boss to its spot in burstSeconds, then takes leaveSeconds
        // more to drift on off the wall, bobbing up and down as it goes.
        [SerializeField, Min(0.05f)] private float burstSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float leaveSeconds = 3f;
        // The least time the wall stays covered, longer if the chase is not ready yet or a pigeon
        // is still on its way off.
        [SerializeField, Min(0f)] private float holdSeconds = 2.5f;

        [Header("Closing (seconds)")]
        [SerializeField, Min(0.05f)] private float shrinkSeconds = 0.25f;
        // The streak wipes back streakBackAfter the closing starts, the rooftop fading after it.
        [SerializeField, Min(0f)] private float streakBackAfter = 0.1f;
        [SerializeField, Min(0.05f)] private float streakBackSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.3f;
        // Feathers keep coming this long over the chase before they stop.
        [SerializeField, Min(0f)] private float feathersAfterSeconds = 1.5f;

        [Header("Flying")]
        // How far a pigeon drifts on from its spot (wall units, enough to be off the wall), and how
        // much it climbs on top of that on the way.
        [SerializeField, Min(0f)] private float leaveDistance = 1400f;
        [SerializeField, Min(0f)] private float leaveClimb = 150f;
        // How far it bobs up and down as it flies (wall units) and how often a second.
        [SerializeField, Min(0f)] private float bobHeight = 45f;
        [SerializeField, Min(0f)] private float bobsPerSecond = 1.4f;
        // The pigeon's flight, frame by frame (Assets/ART/Transition/Boss Open/PigeonFly, cut from
        // Pigeon_Fly_Loop.mp4 by Tools/PigeonArt), played on every pigeon, each a little further on
        // so they do not flap in step; empty to squash the still picture instead (below). The video
        // is 30 frames a second as cut; a little faster looks livelier.
        [SerializeField] private Sprite[] pigeonFrames = new Sprite[0];
        [SerializeField, Min(1f)] private float pigeonFramesPerSecond = 39f;
        // Wing beats a second (the still picture only).
        [SerializeField, Min(0f)] private float flapsPerSecond = 7f;
        // How much a wing beat squashes the picture up and down (0 to 1).
        [SerializeField, Range(0f, 0.6f)] private float flapSquash = 0.25f;
        // How far it tips its head up as it flies away (degrees).
        [SerializeField, Range(0f, 40f)] private float flyTilt = 15f;
        [SerializeField, Min(0f)] private float bossBob = 5f;

        [Header("Sounds")]
        // Feathers fluttering down over the stage, the streak striking in, the boss's sting as he
        // pops up, the pigeons' wings as they fly in, and everything sweeping back out.
        [SerializeField] private TransitionSound feathersSound = new TransitionSound(0.4f);
        [SerializeField] private TransitionSound strikeSound = new TransitionSound(0.6f);
        [SerializeField] private TransitionSound bossSound = new TransitionSound(0.65f);
        [SerializeField] private TransitionSound pigeonsSound = new TransitionSound(0.45f);
        [SerializeField] private TransitionSound outSound = new TransitionSound(0.45f);

        private enum Stage { Hidden, Opening, Holding, Closing, FeathersOnly }

        private Stage stage = Stage.Hidden;
        private float wipeAt;
        private float coveredAt;
        private float feathersStopAt;
        private float backgroundAlpha;
        private Vector2 bossRest;
        private Vector3 bossSize;
        private Vector2[] pigeonRests;
        // From each pigeon's spot to the boss, where it flies out from.
        private Vector2[] pigeonFromBoss;
        private Vector3[] pigeonSizes;
        private Image[] pigeonPictures;
        private bool measured;
        private AudioSource voice;
        private bool pigeonsHeard;
        private bool skyFeathersBack;
        // The streak runs across on its own (the spotlight's way in), and the boss and pigeons wait
        // this long for it to reach halfway.
        private bool streakFirst;
        private float bossAfter;

        // True once the prefab has its pieces (Tools > Bounty Hunter > Build Boss Transition).
        public bool IsSetUp => background != null && streak != null;

        private float WipeStart => feathersFirstSeconds + wipeAfter;

        // The time from the start until the wall is covered.
        private float CoverAt => feathersFirstSeconds + Mathf.Max(fadeInSeconds, wipeAfter + wipeSeconds);

        private float CloseLength => Mathf.Max(shrinkSeconds, streakBackAfter + streakBackSeconds + fadeSeconds);

        // Plays the cut-in up to the moment the wall is fully covered, and keeps it covered.
        public IEnumerator Open()
        {
            streakFirst = false;
            bossAfter = 0f;
            if (spotlight != null && spotlight.IsSetUp)
            {
                yield return OpenWithSpotlight();
                yield break;
            }

            gameObject.SetActive(true);
            Measure();
            stage = Stage.Opening;
            ShowBoss(0f, 0f);
            for (int i = 0; i < pigeons.Length; i++) pigeons[i].gameObject.SetActive(false);
            StartFeathers();

            bool dotsStarted = false;
            float start = Time.unscaledTime;
            wipeAt = start + WipeStart;
            while (Time.unscaledTime - start < CoverAt)
            {
                float since = Time.unscaledTime - start;
                if (!dotsStarted && since >= WipeStart)
                {
                    if (dots != null) dots.StartPopping();
                    strikeSound.Play(voice);
                    dotsStarted = true;
                }

                SetAlpha(background, backgroundAlpha * Smooth((since - feathersFirstSeconds) / fadeInSeconds));
                PoseStreak(EaseOutCubic((Time.unscaledTime - wipeAt) / wipeSeconds));
                yield return null;
            }

            SetAlpha(background, backgroundAlpha);
            PoseStreak(1f);
            if (!dotsStarted)
            {
                if (dots != null) dots.StartPopping();
                strikeSound.Play(voice);
            }

            bossSound.Play(voice);
            Hold();
        }

        // Waits out the rest of the hold, then sends everything back out the way it came. Returns
        // with the chase showing; the feathers keep falling over it on their own.
        public IEnumerator Close()
        {
            while (stage == Stage.Holding && Time.unscaledTime < coveredAt + HoldLength) yield return null;

            if (spotlight != null && spotlight.IsSetUp)
            {
                stage = Stage.Closing;
                outSound.Play(voice);
                // Behind the sweeping bands everything but the feathers goes; the chase shows as they pass.
                yield return spotlight.PlayOut(ClearAway);
                LeaveFeathers();
                yield break;
            }

            stage = Stage.Closing;
            float held = Time.unscaledTime - coveredAt;
            if (dots != null) dots.PopAll();
            outSound.Play(voice);
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
                if (!pigeonsHeard && now - coveredAt >= PigeonsStart)
                {
                    pigeonsSound.Play(voice);
                    pigeonsHeard = true;
                }

                if (!skyFeathersBack && now - coveredAt >= SkyFeathersAt) FeathersBack();

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

        // The spotlight's way in: the wall darkens, slashes and the boss's eyes go by, and as the band
        // goes the rooftop is put up behind the dark; as it lifts the streak runs in from the right all
        // the way across, the boss popping up as it passes halfway. Feathers fall all the while.
        private IEnumerator OpenWithSpotlight()
        {
            gameObject.SetActive(true);
            Measure();
            stage = Stage.Opening;
            ClearAway();
            StartFeathers();
            yield return spotlight.PlayIn(() =>
            {
                SetAlpha(background, backgroundAlpha);
                // The streak runs across in one go (PoseHolding); the boss pops up as it reaches
                // halfway, where its easing is at 0.5.
                streakFirst = true;
                bossAfter = streakRunSeconds * (1f - Mathf.Pow(0.5f, 1f / 3f));
                PoseStreak(0f);
                if (dots != null) dots.StartPopping();
                strikeSound.Play(voice);
                bossSound.Play(voice);
                Hold();
            });
        }

        // The rooftop, the streak and its dots, the boss and the pigeons off the wall at once.
        private void ClearAway()
        {
            ShowBoss(0f, 0f);
            for (int i = 0; i < pigeons.Length; i++) pigeons[i].gameObject.SetActive(false);
            if (dots != null) dots.Clear();
            streak.fillAmount = 0f;
            SetAlpha(background, 0f);
        }

        // When the pigeons burst out of the boss, from the moment the wall is covered.
        private float PigeonsStart => bossAfter + bossPopSeconds + pigeonsAfter;

        // How long the wall stays covered at least: holdSeconds, or until the last pigeon is nearly
        // off the wall (it finishes as everything goes back out).
        private float HoldLength
        {
            get
            {
                int count = pigeons != null ? pigeons.Length : 0;
                if (count == 0) return holdSeconds;

                float lastGone = PigeonsStart + (count - 1) * pigeonGap + burstSeconds + leaveSeconds * 0.8f;
                return Mathf.Max(holdSeconds, lastGone);
            }
        }

        // Feathers start falling over the stage, before anything else comes onto the wall.
        private void StartFeathers()
        {
            if (feathers != null) feathers.StartFalling();
            feathersSound.Play(voice);
        }

        // The wall is covered: no new feathers come in (those in the air fall on out) until the
        // pigeons are halfway off.
        private void Hold()
        {
            pigeonsHeard = false;
            skyFeathersBack = false;
            if (feathers != null) feathers.StopFalling();
            coveredAt = Time.unscaledTime;
            stage = Stage.Holding;
        }

        // When the feathers start falling again, from the moment the wall is covered: once the last
        // pigeon is halfway through its flight off the wall.
        private float SkyFeathersAt
        {
            get
            {
                int count = pigeons != null ? pigeons.Length : 0;
                if (count == 0) return PigeonsStart;

                return PigeonsStart + (count - 1) * pigeonGap + (burstSeconds + leaveSeconds) * 0.5f;
            }
        }

        private void FeathersBack()
        {
            skyFeathersBack = true;
            if (feathers != null) feathers.StartFalling();
        }

        // Everything but the feathers is off the wall; they keep coming a while, then the last of
        // them fall out and the cut-in switches itself off.
        private void LeaveFeathers()
        {
            // Should the wall be uncovered before the pigeons were halfway off, the feathers still fall.
            if (!skyFeathersBack) FeathersBack();

            ClearAway();
            feathersStopAt = Time.unscaledTime + feathersAfterSeconds;
            stage = Stage.FeathersOnly;
        }

        // Where everything sits in the prefab, which the motion is measured from.
        private void Measure()
        {
            if (measured) return;

            measured = true;
            voice = TransitionSound.SourceOn(gameObject);
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
            pigeonFromBoss = new Vector2[pigeons.Length];
            pigeonSizes = new Vector3[pigeons.Length];
            pigeonPictures = new Image[pigeons.Length];
            for (int i = 0; i < pigeons.Length; i++)
            {
                pigeonRests[i] = pigeons[i].anchoredPosition;
                if (boss != null && pigeons[i].parent != null)
                {
                    Vector3 bossThere = pigeons[i].parent.InverseTransformPoint(boss.position);
                    pigeonFromBoss[i] = (Vector2)(bossThere - pigeons[i].localPosition);
                }
                pigeonSizes[i] = pigeons[i].localScale;
                pigeonPictures[i] = pigeons[i].GetComponent<Image>();
                // The flight's frames are a different shape from the still picture: keep theirs.
                if (pigeonPictures[i] != null && Animated) pigeonPictures[i].preserveAspect = true;
            }
        }

        // The streak share of the way across (0 to 1), and the dots only where it has reached.
        private void PoseStreak(float share)
        {
            streak.fillAmount = share;
            if (dots != null) dots.SetReach(share);
        }

        // The boss pops up and bobs, the pigeons burst out of him one after another and fly away.
        private void PoseHolding(float since)
        {
            if (streakFirst) PoseStreak(EaseOutCubic(since / streakRunSeconds));

            float bossTime = since - bossAfter;
            ShowBoss(EaseOutBack(bossTime / bossPopSeconds), since);
            PosePigeons(since);
        }

        // Everything back out: the boss shrinks away (any pigeon still on the wall flies on), the
        // streak wipes back off to the right, the rooftop fades.
        private void PoseClosing(float time, float held)
        {
            ShowBoss(1f - Smooth(time / shrinkSeconds), held + time);
            PosePigeons(held + time);

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

        // Every pigeon, since seconds after the wall was covered.
        private void PosePigeons(float since)
        {
            for (int i = 0; i < pigeons.Length; i++)
            {
                PosePigeon(i, since - PigeonsStart - i * pigeonGap, since);
            }
        }

        // Pigeon i, out seconds after it bursts out of the boss (not out yet at 0 or less, gone once
        // it has left the wall), flapping time seconds in.
        private void PosePigeon(int i, float sinceOut, float time)
        {
            RectTransform pigeon = pigeons[i];
            bool there = sinceOut > 0f && sinceOut < burstSeconds + leaveSeconds;
            pigeon.gameObject.SetActive(there);
            if (!there) return;

            // Away from the boss through its spot in the prefab, straight up should it sit on him.
            Vector2 away = -pigeonFromBoss[i];
            away = away.sqrMagnitude > 1f ? away.normalized : Vector2.up;
            // The picture faces left, so it is turned over on x to fly right.
            float facing = away.x > 0f ? 1f : -1f;

            // Shoots out of the boss to its spot, then drifts on, slowly at first and picking up,
            // climbing a little and bobbing up and down all the way.
            float burst = EaseOutCubic(sinceOut / burstSeconds);
            float leave = Mathf.Clamp01((sinceOut - burstSeconds) / leaveSeconds);
            float drift = leave * leave * 0.6f + leave * 0.4f;
            float bobPhase = time * bobsPerSecond * Mathf.PI * 2f + i * 1.9f;
            Vector2 bob = Vector2.up * (bobHeight * Mathf.Sin(bobPhase) * burst);
            pigeon.anchoredPosition = pigeonRests[i] + pigeonFromBoss[i] * (1f - burst) + away * (leaveDistance * drift)
                + Vector2.up * (leaveClimb * drift) + bob;
            // Tips its head up rising and down sinking.
            pigeon.localRotation = Quaternion.Euler(0f, 0f, facing * flyTilt * Mathf.Cos(bobPhase));

            // Grows from nothing as it comes out of the boss.
            Vector3 size = pigeonSizes[i] * Smooth(sinceOut / burstSeconds * 1.5f);
            size.x = Mathf.Abs(size.x) * -facing;
            if (Animated && pigeonPictures[i] != null)
            {
                // The flight's own wing beats; each pigeon a third of the way on from the one before.
                int frame = Mathf.FloorToInt(time * pigeonFramesPerSecond + i * pigeonFrames.Length / 3f);
                pigeonPictures[i].sprite = pigeonFrames[(frame % pigeonFrames.Length + pigeonFrames.Length) % pigeonFrames.Length];
            }
            else
            {
                float wing = 0.5f + 0.5f * Mathf.Sin(time * flapsPerSecond * Mathf.PI * 2f + i * 1.3f);
                size.y *= 1f - flapSquash * wing;
            }

            pigeon.localScale = size;
        }

        private bool Animated => pigeonFrames != null && pigeonFrames.Length > 0;

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
