using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The cut-in on the wall that opens a mini game, laid out in its prefab (for the suspect lineup:
    // Assets/Prefabs/Transition/Lineup Open Transition.prefab; move, resize, recolour or retime
    // anything there). The pieces come in one after another, each its own way and each a moment
    // after the one before, overlapping so it flows: first the character springs up from below over
    // the game, then the background fades in behind it, the red streak shoots along its zigzag from
    // the bottom left corner (up to the top, down to the bottom, up again to the top right corner;
    // PathReveal), the tapes whip in one by one from the left with a little overshoot, the printing on
    // them sliding along to the right all the while (TapeScroll), and last the logo slams down from
    // big, shaking the whole wall, and a small sign (the camera's LIVE) pops in after it, blinking
    // while the wall is covered. Bits fly about meanwhile (CutInParticles): dust as the character
    // lands, sparks off the streak's tip as it shoots along, tape bits as each tape lands, and a
    // shockwave with a burst of stars and confetti in the logo's colours as it slams down. While the
    // wall stays covered (the mini game set up behind it) everything keeps moving gently: the
    // character bobs and sways, the logo breathes, the tapes flutter and faint stars twinkle all over,
    // on the background but behind everything else (their own layer, backdropParticles), or the
    // background flickers like a torn camera picture instead (GlitchBars).
    // Then it all goes back out, one piece after another: the logo goes in a puff, shrinking away
    // with a twist, the character drops, the tapes whip right off the wall to the right (always far
    // enough to be wholly off it, however wide a tape is or wherever it lies), the streak runs off
    // along its zigzag after its tip and the background fades, showing the mini game. Runs on
    // unscaled time. MapSwitcher drives it: Close until the wall is covered, Open when the mini game
    // is ready.
    public sealed class MinigameCutIn : WallTransition
    {
        // A tape across the wall.
        [Serializable]
        private sealed class Tape
        {
            [SerializeField] private RectTransform piece;

            public RectTransform Piece => piece;
        }

        [Header("Pieces")]
        // Shaken when the logo lands; holds everything else.
        [SerializeField] private RectTransform shaker;
        // Fades in and out.
        [SerializeField] private RectTransform background;
        // Drawn along its zigzag.
        [SerializeField] private PathReveal streakDraw;
        // In the order they whip in.
        [SerializeField] private Tape[] tapes = Array.Empty<Tape>();
        [SerializeField] private RectTransform character;
        [SerializeField] private RectTransform logo;
        // A small sign that pops in after the logo and blinks while the wall is covered (the camera
        // mini game's LIVE); may be empty.
        [SerializeField] private RectTransform badge;

        [Header("Coming in (seconds)")]
        [SerializeField, Min(0f)] private float backgroundAfter = 0.25f;
        [SerializeField, Min(0.05f)] private float backgroundSeconds = 0.3f;
        [SerializeField, Min(0f)] private float streakAfter = 0.4f;
        [SerializeField, Min(0.05f)] private float streakSeconds = 0.42f;
        [SerializeField, Min(0f)] private float tapesAfter = 0.65f;
        [SerializeField, Min(0f)] private float tapeGap = 0.1f;
        [SerializeField, Min(0.05f)] private float tapeSeconds = 0.3f;
        [SerializeField, Min(0f)] private float characterAfter = 0f;
        [SerializeField, Min(0.05f)] private float characterSeconds = 0.4f;
        [SerializeField, Min(0f)] private float logoAfter = 1.2f;
        [SerializeField, Min(0.05f)] private float logoSeconds = 0.22f;
        [SerializeField, Min(0f)] private float badgeAfter = 1.45f;
        [SerializeField, Min(0.05f)] private float badgeSeconds = 0.25f;
        // The sign is lit this long, then dim as long, over and over; and how faint it is when dim.
        [SerializeField, Min(0.05f)] private float badgeBlinkSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float badgeDim = 0.3f;
        // The least time the wall stays covered once everything is in, longer if the mini game is
        // not ready yet.
        [SerializeField, Min(0f)] private float holdSeconds = 4.5f;

        [Header("Going out (seconds)")]
        [SerializeField, Min(0.05f)] private float logoOutSeconds = 0.2f;
        [SerializeField, Min(0f)] private float characterOutAfter = 0.08f;
        [SerializeField, Min(0.05f)] private float characterOutSeconds = 0.3f;
        [SerializeField, Min(0f)] private float tapesOutAfter = 0.14f;
        [SerializeField, Min(0f)] private float tapeOutGap = 0.06f;
        [SerializeField, Min(0.05f)] private float tapeOutSeconds = 0.3f;
        [SerializeField, Min(0f)] private float streakOutAfter = 0.3f;
        [SerializeField, Min(0.05f)] private float streakOutSeconds = 0.4f;
        [SerializeField, Min(0f)] private float backgroundOutAfter = 0.55f;
        [SerializeField, Min(0.05f)] private float backgroundOutSeconds = 0.3f;

        [Header("Motion (wall units, degrees)")]
        // The logo starts this many times its size and slams down to it.
        [SerializeField, Min(1f)] private float logoSlamFrom = 2.4f;
        [SerializeField, Min(0f)] private float landingShake = 14f;
        [SerializeField, Min(0.05f)] private float landingShakeSeconds = 0.3f;
        // The character comes up from this far below its spot.
        [SerializeField, Min(0f)] private float characterRise = 520f;
        [SerializeField, Min(0f)] private float characterBob = 6f;
        [SerializeField, Range(0f, 10f)] private float characterSway = 2.5f;
        [SerializeField, Range(0f, 0.1f)] private float logoBreathe = 0.03f;
        [SerializeField, Min(0f)] private float tapeFlutter = 3f;

        [Header("Bits flying about")]
        // Over everything; may be empty (no bits).
        [SerializeField] private CutInParticles particles;
        // Just over the background, behind everything else: the twinkling stars; empty puts them with
        // the other bits.
        [SerializeField] private CutInParticles backdropParticles;
        // A broken-signal flicker on the background (the camera mini game's, in place of the stars),
        // fading in and out with it; may be empty.
        [SerializeField] private GlitchBars glitch;
        // Sparks off the streak's tip a second as it shoots along.
        [SerializeField, Min(0f)] private float streakSparksPerSecond = 110f;
        // Stars twinkling anywhere on the wall a second while it is covered, and how see-through
        // they are (1 solid), kept faint so they do not take the eye off the logo.
        [SerializeField, Min(0f)] private float twinklesPerSecond = 14f;
        [SerializeField, Range(0f, 1f)] private float twinkleAlpha = 0.4f;
        [SerializeField] private Color[] sparkColours =
        {
            Color.white, new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.35f, 0.3f, 1f),
        };
        [SerializeField] private Color[] tapeColours =
        {
            new Color(0.93f, 1f, 0.1f, 1f), new Color(0.93f, 1f, 0.1f, 1f), new Color(0.08f, 0.08f, 0.1f, 1f),
        };
        [SerializeField] private Color[] dustColours =
        {
            new Color(1f, 1f, 1f, 0.75f), new Color(0.92f, 0.88f, 0.8f, 0.7f),
        };
        // The logo's own colours: teal, purple, yellow and white.
        [SerializeField] private Color[] logoColours =
        {
            new Color(0.3f, 0.92f, 0.8f, 1f), new Color(0.62f, 0.32f, 0.95f, 1f), new Color(1f, 0.9f, 0.2f, 1f), Color.white,
        };

        [Header("Sounds")]
        // Each tape whipping in, the logo slamming down, and everything sweeping back out.
        [SerializeField] private TransitionSound tapesSound = new TransitionSound(0.55f);
        [SerializeField] private TransitionSound slamSound = new TransitionSound(0.8f);
        [SerializeField] private TransitionSound outSound = new TransitionSound(0.5f);

        // Extra room past the wall's edge for a tape leaving it (wall units).
        private const float OffWallMargin = 20f;

        private float wallWidth;
        private Graphic backgroundPicture;
        private float backgroundAlpha = 1f;
        private Vector2[] tapeRests;
        private Vector2 characterRest;
        private Vector2 logoRest;
        private Graphic badgePicture;
        private bool measured;
        private bool holding;
        private float coveredAt;
        private float shakeUntil;
        private AudioSource voice;
        // Sparks and twinkles owed but not yet made (they come a fraction a frame).
        private float sparksOwed;
        private float twinklesOwed;

        // True once the prefab has its pieces (Tools > Bounty Hunter > Build Mini Game Cut-Ins).
        public override bool IsSetUp => shaker != null && background != null && logo != null;

        public override float OpenSeconds => OutLength;

        private float InLength => Mathf.Max(Mathf.Max(Mathf.Max(logoAfter + logoSeconds, backgroundAfter + backgroundSeconds), badge != null ? badgeAfter + badgeSeconds : 0f),
            Mathf.Max(characterAfter + characterSeconds, tapesAfter + tapeGap * Mathf.Max(0, tapes.Length - 1) + tapeSeconds));

        // Plays everything in, until the wall is covered and every piece has landed.
        public override IEnumerator Close()
        {
            gameObject.SetActive(true);
            Measure();
            holding = false;
            voice = TransitionSound.SourceOn(gameObject);
            int tapesHeard = 0;
            int tapesLanded = 0;
            bool characterLanded = false;
            bool slammed = false;
            if (particles != null) particles.Clear();
            if (backdropParticles != null) backdropParticles.Clear();
            if (glitch != null) glitch.Clear();
            float start = Time.unscaledTime;
            while (true)
            {
                float since = Time.unscaledTime - start;
                PoseIn(since);
                StreakSparks(since);
                if (tapesLanded < tapes.Length && since >= tapesAfter + tapeGap * tapesLanded + tapeSeconds * 0.6f)
                {
                    TapeBits(tapesLanded);
                    tapesLanded++;
                }

                if (!characterLanded && since >= characterAfter + characterSeconds * 0.55f)
                {
                    CharacterDust();
                    characterLanded = true;
                }

                if (tapesHeard < tapes.Length && since >= tapesAfter + tapeGap * tapesHeard)
                {
                    tapesSound.Play(voice);
                    tapesHeard++;
                }

                if (!slammed && since >= logoAfter + logoSeconds)
                {
                    slamSound.Play(voice);
                    shakeUntil = Time.unscaledTime + landingShakeSeconds;
                    LogoBurst();
                    slammed = true;
                }

                if (since >= InLength) break;
                yield return null;
            }

            PoseIn(InLength);
            coveredAt = Time.unscaledTime;
            holding = true;
        }

        public override IEnumerator Hold()
        {
            while (Time.unscaledTime - coveredAt < holdSeconds) yield return null;
        }

        // Sends everything back out, showing the mini game, then hides.
        public override IEnumerator Open()
        {
            yield return Hold();
            holding = false;
            outSound.Play(voice);
            if (particles != null) particles.Burst(logoRest, 18, 420f, 16f, 0.4f, dustColours);
            Vector3 logoSize = logo.localScale;
            float start = Time.unscaledTime;
            float length = OutLength;
            while (Time.unscaledTime - start < length)
            {
                PoseOut(Time.unscaledTime - start, logoSize);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private float OutLength => Mathf.Max(Mathf.Max(backgroundOutAfter + backgroundOutSeconds, streakOutAfter + streakOutSeconds),
            Mathf.Max(characterOutAfter + characterOutSeconds, tapesOutAfter + tapeOutGap * Mathf.Max(0, tapes.Length - 1) + tapeOutSeconds));

        private void Update()
        {
            if (shaker != null)
            {
                float shaking = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / landingShakeSeconds);
                shaker.anchoredPosition = shaking > 0f
                    ? new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * (landingShake * shaking)
                    : Vector2.zero;
            }

            if (!holding) return;
            PoseIdle(Time.unscaledTime - coveredAt);
            Twinkles();
        }

        // Sparks fly off the streak's tip while it shoots along its zigzag.
        private void StreakSparks(float since)
        {
            float drawing = (since - streakAfter) / streakSeconds;
            if (particles == null || streakDraw == null || drawing <= 0f || drawing >= 1f) return;
            if (!streakDraw.TipAt(EaseInOutQuad(drawing), out Vector3 tip)) return;

            sparksOwed += streakSparksPerSecond * Time.unscaledDeltaTime;
            int count = Mathf.FloorToInt(sparksOwed);
            sparksOwed -= count;
            if (count > 0) particles.Burst(particles.FromWorld(tip), count, 320f, 9f, 0.45f, sparkColours, 0.15f, 380f);
        }

        // Bits of tape fly off where a tape lands, somewhere across the wall.
        private void TapeBits(int i)
        {
            if (particles == null || tapes[i].Piece == null) return;

            float across = UnityEngine.Random.Range(-0.35f, 0.35f) * wallWidth;
            var at = new Vector2(across, tapeRests[i].y);
            // Flying on the way it came in from the left.
            const float heading = 0f;
            particles.Burst(at, 12, 360f, 10f, 0.5f, tapeColours, 0f, 520f, heading, 120f);
        }

        // Dust puffs out round the character's feet as it lands.
        private void CharacterDust()
        {
            if (particles == null || character == null) return;

            Vector2 feet = characterRest + Vector2.down * (character.rect.height * 0.42f);
            particles.Burst(feet, 16, 220f, 18f, 0.55f, dustColours, 0f, 120f, 90f, 170f);
        }

        // The logo's slam: a shockwave and a burst of stars and confetti in its colours.
        private void LogoBurst()
        {
            if (particles == null) return;

            float width = logo.rect.width;
            particles.Ring(logoRest, width * 1.3f, 0.45f, Color.white);
            particles.Ring(logoRest, width * 0.9f, 0.35f, logoColours.Length > 2 ? logoColours[2] : Color.white);
            particles.Burst(logoRest, 40, 820f, 12f, 0.9f, logoColours, 0.35f, 600f);
        }

        // Now and then a faint star twinkles somewhere on the wall, behind the pieces.
        private void Twinkles()
        {
            CutInParticles layer = backdropParticles != null ? backdropParticles : particles;
            if (layer == null) return;

            twinklesOwed += twinklesPerSecond * Time.unscaledDeltaTime;
            Vector2 half = ((RectTransform)transform).rect.size * 0.48f;
            while (twinklesOwed >= 1f)
            {
                twinklesOwed -= 1f;
                var at = new Vector2(UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y));
                Color colour = UnityEngine.Random.value < 0.6f ? Color.white : logoColours[UnityEngine.Random.Range(0, logoColours.Length)];
                colour.a *= twinkleAlpha;
                layer.Twinkle(at, UnityEngine.Random.Range(18f, 34f), 0.7f, colour);
            }
        }

        private void Measure()
        {
            if (measured) return;

            measured = true;
            wallWidth = ((RectTransform)transform).rect.width;
            backgroundPicture = background.GetComponent<Graphic>();
            if (backgroundPicture != null) backgroundAlpha = backgroundPicture.color.a;
            tapeRests = new Vector2[tapes.Length];
            for (int i = 0; i < tapes.Length; i++) tapeRests[i] = tapes[i].Piece != null ? tapes[i].Piece.anchoredPosition : Vector2.zero;
            if (character != null) characterRest = character.anchoredPosition;
            logoRest = logo.anchoredPosition;
            if (badge != null) badgePicture = badge.GetComponent<Graphic>();
        }

        private void PoseIn(float since)
        {
            FadeBackground(Smooth((since - backgroundAfter) / backgroundSeconds));
            if (streakDraw != null) streakDraw.Show(0f, EaseInOutQuad((since - streakAfter) / streakSeconds));

            for (int i = 0; i < tapes.Length; i++)
            {
                if (tapes[i].Piece == null) continue;
                float t = (since - tapesAfter - tapeGap * i) / tapeSeconds;
                tapes[i].Piece.anchoredPosition = tapeRests[i] + Vector2.left * (OffWall(i, -1f) * (1f - EaseOutBack(t)));
            }

            if (character != null)
            {
                float rise = EaseOutBack((since - characterAfter) / characterSeconds);
                character.anchoredPosition = characterRest + Vector2.down * (characterRise * (1f - rise));
                character.localRotation = Quaternion.identity;
            }

            float slam = Mathf.Clamp01((since - logoAfter) / logoSeconds);
            logo.gameObject.SetActive(since >= logoAfter);
            logo.anchoredPosition = logoRest;
            logo.localScale = Vector3.one * Mathf.Lerp(logoSlamFrom, 1f, slam * slam);
            logo.localRotation = Quaternion.Euler(0f, 0f, -8f * (1f - slam));

            if (badge != null)
            {
                badge.gameObject.SetActive(since >= badgeAfter);
                badge.localScale = Vector3.one * EaseOutBack((since - badgeAfter) / badgeSeconds);
                SetBadgeAlpha(1f);
            }
        }

        // While the wall is covered: the character bobs and sways, the logo breathes, the tapes flutter.
        private void PoseIdle(float time)
        {
            if (character != null)
            {
                character.anchoredPosition = characterRest + Vector2.up * (characterBob * Mathf.Sin(time * 4.2f));
                character.localRotation = Quaternion.Euler(0f, 0f, characterSway * Mathf.Sin(time * 2.1f));
            }

            logo.localScale = Vector3.one * (1f + logoBreathe * Mathf.Sin(time * 5f));
            if (badge != null) SetBadgeAlpha(Mathf.Repeat(time, badgeBlinkSeconds * 2f) < badgeBlinkSeconds ? 1f : badgeDim);
            for (int i = 0; i < tapes.Length; i++)
            {
                if (tapes[i].Piece == null) continue;
                tapes[i].Piece.anchoredPosition = tapeRests[i] + Vector2.up * (tapeFlutter * Mathf.Sin(time * 3.3f + i * 1.7f));
            }
        }

        private void PoseOut(float since, Vector3 logoSize)
        {
            float shrink = EaseInCubic(since / logoOutSeconds);
            logo.localScale = logoSize * (1f - shrink);
            logo.localRotation = Quaternion.Euler(0f, 0f, 25f * shrink);
            if (badge != null) badge.localScale = Vector3.one * (1f - shrink);

            if (character != null)
            {
                float drop = EaseInCubic((since - characterOutAfter) / characterOutSeconds);
                character.anchoredPosition = characterRest + Vector2.down * (characterRise * drop);
            }

            for (int i = 0; i < tapes.Length; i++)
            {
                if (tapes[i].Piece == null) continue;
                float t = EaseInCubic((since - tapesOutAfter - tapeOutGap * i) / tapeOutSeconds);
                tapes[i].Piece.anchoredPosition = tapeRests[i] + Vector2.right * (OffWall(i, 1f) * t);
            }

            // The tail runs a little past the end, so not even the tip stays at the wall's edge.
            if (streakDraw != null) streakDraw.Show(EaseInOutQuad((since - streakOutAfter) / streakOutSeconds) * 1.1f, 1f);
            FadeBackground(1f - Smooth((since - backgroundOutAfter) / backgroundOutSeconds));
        }

        // How far tape i must move from its spot towards side (-1 left, 1 right) to be wholly off the wall.
        private float OffWall(int i, float side)
        {
            return wallWidth * 0.5f + tapes[i].Piece.rect.width * 0.5f - side * tapeRests[i].x + OffWallMargin;
        }

        private void SetBadgeAlpha(float alpha)
        {
            if (badgePicture == null) return;
            Color colour = badgePicture.color;
            colour.a = alpha;
            badgePicture.color = colour;
        }

        private void FadeBackground(float shown)
        {
            if (glitch != null) glitch.Strength = shown;
            if (backgroundPicture == null) return;
            Color colour = backgroundPicture.color;
            colour.a = backgroundAlpha * shown;
            backgroundPicture.color = colour;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Speeds up out of the corner and slows into the end, so the streak shoots along evenly.
        private static float EaseInOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float back = 1f - t;
            return 1f - back * back * back;
        }

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
