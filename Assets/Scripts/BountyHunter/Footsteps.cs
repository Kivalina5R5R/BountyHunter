using UnityEngine;

namespace BountyHunter.Thieves
{
    // A thief's footfalls while it runs (faster than runSpeed: running away or for a hiding spot):
    // one every stride of ground covered. Strolling makes no sound, and nothing while its picture is
    // hidden (in a hiding spot).
    // GameSfx adds it and plays the sounds, keeping how many sound at once in check.
    public sealed class Footsteps : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float stride = 0.34f;
        // Faster than this (units per second) counts as running; a thief strolls at up to 0.72.
        [SerializeField, Min(0f)] private float runSpeed = 1.0f;

        private GameSfx sounds;
        private SpriteRenderer body;
        private Vector3 lastPosition;
        private float travelled;

        // picture: the thief's sprite, off while it hides.
        public void Initialize(GameSfx player, SpriteRenderer picture)
        {
            sounds = player;
            body = picture;
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            travelled = 0f;
        }

        private void LateUpdate()
        {
            Vector3 step = transform.position - lastPosition;
            lastPosition = transform.position;
            step.y = 0f;
            if (sounds == null || body == null || !body.enabled) return;

            float speed = step.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (speed <= runSpeed)
            {
                travelled = 0f;
                return;
            }

            travelled += step.magnitude;
            if (travelled < stride) return;

            travelled = 0f;
            sounds.PlayRunStep(transform);
        }
    }
}
