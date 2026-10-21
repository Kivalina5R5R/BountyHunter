using UnityEngine;

namespace BountyHunter.Thieves
{
    // Kicks up soft dust puffs at a thief's feet while it runs. Puffs come more often the faster
    // it goes and settle back the way it came. Nothing appears while the thief walks (wandering
    // stays under minimumSpeed), stands still or hides (its sprite is off). Added to each thief by ThiefSpawner; DustPuffs animates them.
    public sealed class DustTrail : MonoBehaviour
    {
        private const float Height = 0.03f;

        // Below this speed (units per second) no dust is kicked up: above a thief's walk (wander
        // speed 0.6, up to 0.72) and its hurried walk onto the floor (1.05), below its run (1.5).
        [SerializeField, Min(0f)] private float minimumSpeed = 1.1f;
        // Distance travelled between puffs at walking pace; running halves it.
        [SerializeField, Min(0.02f)] private float spacing = 0.14f;
        [SerializeField] private Vector2 startWidth = new Vector2(0.16f, 0.22f);
        [SerializeField] private Vector2 endWidth = new Vector2(0.38f, 0.52f);
        [SerializeField] private Vector2 lifeSeconds = new Vector2(0.45f, 0.75f);
        [SerializeField, Range(0f, 1f)] private float alpha = 0.8f;

        private Sprite[] sprites;
        private SpriteRenderer body;
        private DustPuffs puffs;
        private Vector3 lastPosition;
        private float travelled;

        // dustSprites are the puff images; thiefBody is the thief sprite, used to find its feet
        // and to know when it is hidden.
        public void Initialize(Sprite[] dustSprites, SpriteRenderer thiefBody)
        {
            sprites = dustSprites;
            body = thiefBody;
            lastPosition = transform.position;
            puffs = DustPuffs.Create(name, transform.parent);
        }

        private void Update()
        {
            if (puffs == null) return;

            Vector3 position = transform.position;
            Vector3 step = position - lastPosition;
            lastPosition = position;
            step.y = 0f;
            float distance = step.magnitude;
            float speed = distance / Mathf.Max(Time.deltaTime, 0.0001f);
            if (sprites == null || sprites.Length == 0 || body == null || !body.enabled || speed < minimumSpeed)
            {
                travelled = 0f;
                return;
            }

            float interval = spacing / Mathf.Clamp(speed / 0.6f, 1f, 2f);
            travelled += distance;
            while (travelled >= interval)
            {
                travelled -= interval;
                Emit(step / distance, speed);
            }
        }

        private void Emit(Vector3 direction, float speed)
        {
            Sprite sprite = sprites[Random.Range(0, sprites.Length)];
            if (sprite == null) return;

            var sideways = new Vector3(-direction.z, 0f, direction.x);
            var feet = new Vector3(transform.position.x, Height, FeetZ());
            feet += sideways * Random.Range(-0.06f, 0.06f) - direction * 0.04f;

            Vector3 drift = (-direction * Random.Range(0.1f, 0.25f) + sideways * Random.Range(-0.08f, 0.08f)) * Mathf.Clamp(speed, 0.5f, 1.5f);
            puffs.Spawn(sprite, feet, drift,
                Random.Range(startWidth.x, startWidth.y),
                Random.Range(endWidth.x, endWidth.y),
                Random.Range(lifeSeconds.x, lifeSeconds.y),
                alpha);
        }

        // World Z of the feet: the lowest drawn point of the flat sprite (frames may carry empty
        // margins, so the sprite rectangle is not used), raised a little onto the toes.
        private float FeetZ()
        {
            Sprite sprite = body.sprite;
            Vector2[] vertices = sprite != null ? sprite.vertices : null;
            if (vertices == null || vertices.Length == 0) return body.bounds.min.z;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (Vector2 vertex in vertices)
            {
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }

            Vector3 lowest = body.transform.TransformPoint(new Vector3(0f, minY + (maxY - minY) * 0.06f, 0f));
            return lowest.z;
        }

        private void OnDestroy()
        {
            if (puffs != null) puffs.ReleaseWhenDone();
        }
    }
}
