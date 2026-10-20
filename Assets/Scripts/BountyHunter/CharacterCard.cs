using System;
using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One character's card on the intro floor: the police ID on its folder with a SELECT button under
    // it. A player stepping on makes the card bounce so they know it noticed them; while they stand
    // on it a loading ring fills around it and the button fades out of its
    // way (back again if they step off); once full a red SELECTED stamp slams onto the card. Idle, the card breathes and the button pulses to
    // invite a player over. Hidden at first, it can be revealed: it spins down onto the floor with an
    // overshoot, squashes as it lands and the button pops up. Lies flat on the floor like the other floor art; CharacterSelect drives it.
    public sealed class CharacterCard : MonoBehaviour
    {
        [SerializeField] private string character = "Tedly";
        [SerializeField] private SpriteRenderer card;
        [SerializeField] private SpriteRenderer button;
        [SerializeField] private SpriteRenderer stamp;
        [SerializeField] private SpriteRenderer ringBack;
        // Drawn with the radial fill shader; its _Fill shows the progress.
        [SerializeField] private SpriteRenderer ringFill;

        [Header("Idle")]
        [SerializeField, Range(0f, 0.1f)] private float cardBreathe = 0.015f;
        [SerializeField, Range(0f, 0.2f)] private float buttonPulse = 0.06f;
        [SerializeField, Min(0.2f)] private float pulseSeconds = 1.4f;

        [Header("Step on")]
        // The bounce when a player steps onto the card: how much it swells and how long it springs.
        [SerializeField, Range(0f, 0.6f)] private float stepBounce = 0.28f;
        [SerializeField, Min(0.1f)] private float stepBounceSeconds = 0.55f;
        // It also twists from side to side as it bounces, in degrees, so it catches the eye from above.
        [SerializeField, Range(0f, 30f)] private float stepTwist = 9f;

        [Header("Reveal")]
        // The spin down onto the floor: how long it takes and how far round it turns on the way.
        [SerializeField, Min(0.1f)] private float revealSeconds = 0.55f;
        [SerializeField, Range(0f, 360f)] private float revealSpin = 160f;
        // The squash as it lands, and the button popping up just after.
        [SerializeField, Min(0.1f)] private float landSeconds = 0.55f;
        [SerializeField, Min(0f)] private float buttonDelaySeconds = 0.1f;
        [SerializeField, Min(0.1f)] private float buttonPopSeconds = 0.5f;

        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private MaterialPropertyBlock fillBlock;
        private Vector3 cardScale;
        private Vector3 buttonScale;
        private Vector3 stampScale;
        private Vector3 ringScale;
        private float progress;
        private float bounceAge = float.MaxValue;
        private float time;
        private bool busy;
        private bool revealing;

        // Moments for its sounds (CharacterCardSounds): the card pops out, a player steps on, and the
        // SELECTED stamp lands.
        public event Action Popped;
        public event Action SteppedOn;
        public event Action Stamped;

        public string Character => character;
        // 0 to 1: how far the player standing on it has got (0 when nobody is on it or it is taken).
        public float Progress => Taken ? 0f : progress;
        public bool Taken { get; private set; }
        // False while hidden or still coming out; only a ready card can be picked.
        public bool Ready { get; private set; } = true;
        // Where a player stands to pick this card.
        public Vector3 StandPoint => card != null ? card.transform.position : transform.position;

        private void Awake()
        {
            fillBlock = new MaterialPropertyBlock();
            if (card != null) cardScale = card.transform.localScale;
            if (button != null) buttonScale = button.transform.localScale;
            if (stamp != null)
            {
                stampScale = stamp.transform.localScale;
                stamp.enabled = false;
            }

            if (ringFill != null) ringScale = ringFill.transform.localScale;
            ShowRing(0f);
        }

        // 0 to 1: how far the player standing on it has got; 0 hides the ring.
        public void ShowProgress(float value)
        {
            if (Taken) return;
            float next = Mathf.Clamp01(value);
            // Someone just stepped on (from nothing): bounce to say so.
            if (progress <= 0f && next > 0f)
            {
                bounceAge = 0f;
                SteppedOn?.Invoke();
            }
            progress = next;
            ShowRing(progress);
        }

        // Hides the whole card until Reveal brings it out.
        public void Hide()
        {
            if (Taken || revealing) return;

            Ready = false;
            busy = true;
            progress = 0f;
            SetAlpha(card, 0f);
            SetAlpha(button, 0f);
            SetAlpha(ringBack, 0f);
            SetAlpha(ringFill, 0f);
        }

        // Brings a hidden card out after delay seconds.
        public void Reveal(float delay)
        {
            if (Ready || revealing) return;

            revealing = true;
            StartCoroutine(RevealRoutine(delay));
        }

        // The card is picked: button away, ring flash, stamp down.
        public void Take()
        {
            if (Taken) return;

            Taken = true;
            StartCoroutine(TakeRoutine());
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (busy) return;

            float wave = Mathf.Sin(time * Mathf.PI * 2f / pulseSeconds);
            bounceAge += Time.deltaTime;
            if (card != null)
            {
                // Lifts a little toward the player while they stand on it, after a springy bounce.
                float lift = 1f + 0.05f * progress * progress;
                card.transform.localScale = cardScale * (lift + StepBounce() + (Taken ? 0f : cardBreathe * wave));
                card.transform.localRotation = Quaternion.Euler(0f, 0f, StepTwist());
            }

            if (button != null && !Taken) button.transform.localScale = buttonScale * (1f + buttonPulse * Mathf.Max(0f, wave));
        }

        // Up fast, then a damped spring back: 0 when not bouncing.
        private float StepBounce()
        {
            float t = bounceAge / stepBounceSeconds;
            if (t >= 1f) return 0f;
            return stepBounce * Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * (1f - t);
        }

        // Side to side, a little out of step with the bounce, dying away with it.
        private float StepTwist()
        {
            float t = bounceAge / stepBounceSeconds;
            if (t >= 1f) return 0f;
            return stepTwist * Mathf.Sin(t * Mathf.PI * 3.5f) * (1f - t);
        }

        private void ShowRing(float fill)
        {
            float alpha = Mathf.Clamp01(fill * 8f);
            SetAlpha(ringBack, alpha * 0.8f);
            SetAlpha(ringFill, alpha);
            // The button gets out of the ring's way while it shows.
            SetAlpha(button, 1f - alpha);
            if (ringFill == null) return;

            ringFill.GetPropertyBlock(fillBlock);
            fillBlock.SetFloat(FillId, fill);
            ringFill.SetPropertyBlock(fillBlock);
        }

        private IEnumerator TakeRoutine()
        {
            busy = true;
            ShowRing(1f);

            // The ring flashes out wide as it completes.
            if (button != null) button.enabled = false;
            const float popSeconds = 0.3f;
            for (float t = 0f; t < popSeconds; t += Time.deltaTime)
            {
                float u = t / popSeconds;
                if (ringFill != null) ringFill.transform.localScale = ringScale * (1f + 0.15f * u);
                if (ringBack != null) ringBack.transform.localScale = ringScale * (1f + 0.15f * u);
                SetAlpha(ringFill, 1f - u);
                SetAlpha(ringBack, 0.8f * (1f - u));
                yield return null;
            }

            SetAlpha(ringFill, 0f);
            SetAlpha(ringBack, 0f);

            // The stamp comes down hard and the card jolts under it.
            if (stamp != null)
            {
                stamp.enabled = true;
                Stamped?.Invoke();
                const float slamSeconds = 0.14f;
                for (float t = 0f; t < slamSeconds; t += Time.deltaTime)
                {
                    float u = t / slamSeconds;
                    stamp.transform.localScale = stampScale * Mathf.Lerp(2.4f, 1f, u * u);
                    SetAlpha(stamp, u);
                    yield return null;
                }

                stamp.transform.localScale = stampScale;
                SetAlpha(stamp, 1f);
            }

            const float joltSeconds = 0.25f;
            for (float t = 0f; t < joltSeconds; t += Time.deltaTime)
            {
                float u = t / joltSeconds;
                float squash = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * 0.06f;
                if (card != null) card.transform.localScale = new Vector3(cardScale.x * (1f + squash), cardScale.y * (1f - squash), cardScale.z);
                yield return null;
            }

            if (card != null) card.transform.localScale = cardScale;
            progress = 0f;
            busy = false;
        }

        private IEnumerator RevealRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Popped?.Invoke();

            // Spins down from nothing, overshooting a little before it settles flat.
            for (float t = 0f; t < revealSeconds; t += Time.deltaTime)
            {
                float u = t / revealSeconds;
                float spin = -revealSpin * (1f - EaseOutCubic(u));
                Place(card, cardScale * EaseOutBack(u), spin);
                SetAlpha(card, Mathf.Clamp01(u * 4f));
                yield return null;
            }

            Place(card, cardScale, 0f);
            SetAlpha(card, 1f);

            // Lands: the card squashes and the button pops up.
            float total = Mathf.Max(landSeconds, buttonDelaySeconds + buttonPopSeconds);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                float land = Mathf.Clamp01(t / landSeconds);
                float squash = Mathf.Sin(land * Mathf.PI * 2f) * (1f - land) * 0.07f;
                Place(card, new Vector3(cardScale.x * (1f + squash), cardScale.y * (1f - squash), cardScale.z), 0f);

                float pop = Mathf.Clamp01((t - buttonDelaySeconds) / buttonPopSeconds);
                Place(button, buttonScale * EaseOutElastic(pop), 0f);
                SetAlpha(button, Mathf.Clamp01(pop * 5f));
                yield return null;
            }

            Place(card, cardScale, 0f);
            Place(button, buttonScale, 0f);
            SetAlpha(button, 1f);

            // The idle breathing starts from its rest point so it carries on without a jump.
            time = 0f;
            revealing = false;
            busy = false;
            Ready = true;
        }

        private static void Place(SpriteRenderer sprite, Vector3 scale, float angle)
        {
            if (sprite == null) return;
            sprite.transform.localScale = scale;
            sprite.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static float EaseOutCubic(float t)
        {
            float back = 1f - t;
            return 1f - back * back * back;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        // Springs past 1 and wobbles down onto it.
        private static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (Mathf.PI * 2f / 3f)) + 1f;
        }

        private static void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            if (sprite == null) return;
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }
    }
}
