using UnityEngine;

namespace BountyHunter.Thieves
{
    // Makes a hiding bush show what is inside it: a soft rustle while a thief hides, a stronger
    // rustle just before the thief comes out (sneaking out or being flushed by a player), and a
    // swell-and-settle with a burst of leaves on the side a thief jumps in or runs out. The rustle puffs the leaves
    // out and in (wider while shorter, then the other way) with only a slight tilt, so it reads
    // as moving leaves rather than a spinning picture. Added by ThiefSpawner.
    [RequireComponent(typeof(HidingBush))]
    public sealed class BushReaction : MonoBehaviour
    {
        [Header("Soft rustle while a thief hides")]
        // How far the leaves puff out and in, as a fraction of the bush size.
        [SerializeField, Range(0f, 0.2f)] private float calmPuff = 0.025f;
        [SerializeField, Range(0f, 10f)] private float calmTiltDegrees = 1f;
        [SerializeField, Min(0.1f)] private float calmRustlesPerSecond = 1.6f;

        [Header("Stronger rustle before the thief comes out")]
        [SerializeField, Range(0f, 0.2f)] private float urgentPuff = 0.06f;
        [SerializeField, Range(0f, 10f)] private float urgentTiltDegrees = 3f;
        [SerializeField, Min(0.1f)] private float urgentRustlesPerSecond = 4.5f;
        // The rustle builds up over this many seconds before the thief sneaks out.
        [SerializeField, Min(0.1f)] private float urgentWindowSeconds = 1.5f;

        [Header("Swell and leaves when a thief jumps in")]
        [SerializeField, Min(1f)] private float enterSwellScale = 1.18f;
        [SerializeField] private Vector2Int enterLeafCount = new Vector2Int(6, 10);

        [Header("Swell and leaves when a thief runs out")]
        [SerializeField, Min(1f)] private float exitSwellScale = 1.1f;
        [SerializeField] private Vector2Int exitLeafCount = new Vector2Int(4, 8);

        [SerializeField, Min(0.05f)] private float swellSeconds = 0.4f;

        private HidingBush bush;
        private Sprite[] leafSprites;
        private Quaternion restRotation;
        private Vector3 restScale;
        private float swellTime = float.MaxValue;
        private float swellScale = 1f;
        private float shakeAmount;
        private float urgency;
        private float puffPhase;
        private float tiltPhase;
        private float noiseSeed;

        // leaves are the sprites thrown out when a thief jumps in; none means no leaf burst.
        public void Initialize(Sprite[] leaves)
        {
            bush = GetComponent<HidingBush>();
            leafSprites = leaves;
            restRotation = transform.rotation;
            restScale = transform.localScale;
            puffPhase = Random.value * Mathf.PI * 2f;
            tiltPhase = Random.value * Mathf.PI * 2f;
            noiseSeed = Random.value * 100f;
            bush.ThiefEntered += OnThiefEntered;
            bush.ThiefExited += OnThiefExited;
        }

        private void OnDestroy()
        {
            if (bush == null) return;
            bush.ThiefEntered -= OnThiefEntered;
            bush.ThiefExited -= OnThiefExited;
        }

        private void OnThiefEntered(HidingBush _, Vector3 fromSide)
        {
            React(enterSwellScale, enterLeafCount, fromSide);
        }

        private void OnThiefExited(HidingBush _, Vector3 towardSide)
        {
            React(exitSwellScale, exitLeafCount, towardSide);
        }

        private void React(float swell, Vector2Int leafRange, Vector3 side)
        {
            swellTime = 0f;
            swellScale = swell;
            int count = Random.Range(Mathf.Max(0, leafRange.x), Mathf.Max(leafRange.x, leafRange.y) + 1);
            LeafBurst.Play(transform.position, bush.Radius, leafSprites, count, side, transform.parent);
        }

        private void LateUpdate()
        {
            if (bush == null) return;

            float dt = Time.deltaTime;
            // Eases in and out so stepping on or off the bush never snaps the motion.
            shakeAmount = Mathf.MoveTowards(shakeAmount, bush.Occupant != null ? 1f : 0f, dt * 3f);
            urgency = Mathf.MoveTowards(urgency, TargetUrgency(), dt * 2.5f);

            // Phases advance by the current speed, so speeding up stays smooth instead of jumping.
            float rustlesPerSecond = Mathf.Lerp(calmRustlesPerSecond, urgentRustlesPerSecond, urgency);
            puffPhase += dt * rustlesPerSecond * Mathf.PI * 2f;
            tiltPhase += dt * rustlesPerSecond * 0.6f * Mathf.PI * 2f;

            // Slow noise varies each rustle a little, like gusts, instead of an even beat.
            float gust = 0.65f + 0.35f * Mathf.PerlinNoise(noiseSeed, Time.time * 0.8f);
            float puff = Mathf.Lerp(calmPuff, urgentPuff, urgency) * shakeAmount * gust * Mathf.Sin(puffPhase);
            float tilt = Mathf.Lerp(calmTiltDegrees, urgentTiltDegrees, urgency) * shakeAmount * gust * Mathf.Sin(tiltPhase);

            transform.rotation = Quaternion.Euler(0f, tilt, 0f) * restRotation;
            float swell = SwellFactor();
            transform.localScale = new Vector3(
                restScale.x * swell * (1f + puff),
                restScale.y * swell * (1f - puff * 0.8f),
                restScale.z * swell);
        }

        // 0 when the thief is settled, 1 just before it leaves on its own or a player flushes it.
        private float TargetUrgency()
        {
            if (bush.Occupant == null) return 0f;

            float sneak = 1f - Mathf.Clamp01(bush.Occupant.SecondsUntilSneakOut / urgentWindowSeconds);
            return Mathf.Max(bush.FlushProgress, sneak);
        }

        // Swells quickly, then eases back with a small overshoot, like leaves springing back.
        private float SwellFactor()
        {
            if (swellTime >= swellSeconds) return 1f;

            swellTime += Time.deltaTime;
            float t = Mathf.Clamp01(swellTime / swellSeconds);
            if (t < 0.35f) return 1f + (swellScale - 1f) * Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f);

            // Starts at the full swell and dies out, dipping a little below rest on the way.
            float u = (t - 0.35f) / 0.65f;
            return 1f + (swellScale - 1f) * Mathf.Cos(u * Mathf.PI * 1.5f) * (1f - u) * (1f - u);
        }
    }
}
