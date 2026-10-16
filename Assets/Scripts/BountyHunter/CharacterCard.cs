using System.Collections;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // One character's card on the intro floor: the police ID on its folder with a SELECT button under
    // it. While a player stands on it a loading ring fills around it and the button fades out of its
    // way (back again if they step off); once full a red SELECTED stamp slams onto the card. Idle, the card breathes and the button pulses to
    // invite a player over. Lies flat on the floor like the other floor art; CharacterSelect drives it.
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

        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private MaterialPropertyBlock fillBlock;
        private Vector3 cardScale;
        private Vector3 buttonScale;
        private Vector3 stampScale;
        private Vector3 ringScale;
        private float progress;
        private float time;
        private bool busy;

        public string Character => character;
        public bool Taken { get; private set; }
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
            progress = Mathf.Clamp01(value);
            ShowRing(progress);
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
            if (card != null)
            {
                // Lifts a little toward the player while they stand on it.
                float lift = 1f + 0.05f * progress * progress;
                card.transform.localScale = cardScale * (lift + (Taken ? 0f : cardBreathe * wave));
            }

            if (button != null && !Taken) button.transform.localScale = buttonScale * (1f + buttonPulse * Mathf.Max(0f, wave));
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

        private static void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            if (sprite == null) return;
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }
    }
}
