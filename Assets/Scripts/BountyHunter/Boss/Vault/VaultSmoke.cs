using UnityEngine;

namespace BountyHunter.Thieves
{
    // A cloud of smoke the boss throws down in the vault: a heap of grey puffs radius across that
    // puffs up, churns and thins away after seconds. Nothing inside it can be seen (Covers). Drawn
    // with the soft dot (PosterSprites) over the boss and the passers-by. Lies flat on the floor.
    public sealed class VaultSmoke : MonoBehaviour
    {
        private const float Height = 0.045f;
        private const int Puffs = 14;
        private const float GrowSeconds = 0.35f;
        private const float ThinSeconds = 0.8f;
        private static readonly Color Grey = new Color(0.72f, 0.72f, 0.78f, 0.92f);

        private SpriteRenderer[] puffs;
        private Vector2[] offsets;
        private float[] sizes;
        private float radius;
        private float seconds;
        private float bornAt;

        public static VaultSmoke Throw(Transform parent, Vector3 at, float radius, float seconds, int sortingOrder)
        {
            var holder = new GameObject("Smoke");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, 0f, 0f));
            var smoke = holder.AddComponent<VaultSmoke>();
            smoke.radius = radius;
            smoke.seconds = seconds;
            smoke.bornAt = Time.time;
            smoke.puffs = new SpriteRenderer[Puffs];
            smoke.offsets = new Vector2[Puffs];
            smoke.sizes = new float[Puffs];
            for (int i = 0; i < Puffs; i++)
            {
                var puff = new GameObject("Puff", typeof(SpriteRenderer));
                puff.transform.SetParent(holder.transform, false);
                SpriteRenderer picture = puff.GetComponent<SpriteRenderer>();
                picture.sprite = PosterSprites.Dot;
                picture.sortingOrder = sortingOrder;
                smoke.puffs[i] = picture;
                smoke.offsets[i] = Random.insideUnitCircle * radius * 0.6f;
                smoke.sizes[i] = radius * Random.Range(0.8f, 1.2f);
            }

            smoke.Show();
            return smoke;
        }

        // Whether point (on the floor) is inside the cloud while it is thick.
        public bool Covers(Vector3 point)
        {
            float age = Time.time - bornAt;
            if (age > seconds) return false;

            Vector3 at = transform.position;
            return Vector2.Distance(new Vector2(point.x, point.z), new Vector2(at.x, at.z)) <= radius;
        }

        private void Update()
        {
            if (Time.time - bornAt >= seconds + ThinSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Show();
        }

        private void Show()
        {
            float age = Time.time - bornAt;
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / GrowSeconds), 3f);
            float thin = 1f - Mathf.Clamp01((age - seconds) / ThinSeconds);
            float size = PosterSprites.Dot.bounds.size.x;
            for (int i = 0; i < puffs.Length; i++)
            {
                // Each puff drifts round its spot and breathes.
                float churn = age * 0.8f + i;
                Vector2 at = offsets[i] * grow + new Vector2(Mathf.Cos(churn), Mathf.Sin(churn * 1.3f)) * radius * 0.08f;
                puffs[i].transform.localPosition = new Vector3(at.x, at.y, 0f);
                float breathe = 1f + 0.08f * Mathf.Sin(churn * 2f);
                puffs[i].transform.localScale = Vector3.one * (sizes[i] * grow * breathe / size);
                Color colour = Grey;
                colour.a *= thin;
                puffs[i].color = colour;
            }
        }
    }
}
