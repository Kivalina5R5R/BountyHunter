using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Animates dust puffs on the floor: each grows, drifts, spins slowly and fades, then is removed.
    // Lives apart from the thief so puffs finish fading after it runs on or is caught.
    public sealed class DustPuffs : MonoBehaviour
    {
        // Under the thief sprite (8), above bushes (5).
        private const int SortingOrder = 7;

        private readonly List<Puff> puffs = new List<Puff>();
        private bool ownerGone;

        private sealed class Puff
        {
            public SpriteRenderer Renderer;
            public Vector3 Drift;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public float Spin;
            public float Alpha;
        }

        // parent keeps the hierarchy tidy (the thief's own folder); world placement is unchanged.
        public static DustPuffs Create(string ownerName, Transform parent)
        {
            var puffs = new GameObject($"{ownerName} Dust").AddComponent<DustPuffs>();
            puffs.transform.SetParent(parent, true);
            return puffs;
        }

        // Sizes are world widths; drift is world units per second, slowing as the puff ages.
        public void Spawn(Sprite sprite, Vector3 position, Vector3 drift, float startWidth, float endWidth, float life, float alpha)
        {
            var puffObject = new GameObject("Dust", typeof(SpriteRenderer));
            puffObject.transform.SetParent(transform, false);
            puffObject.transform.position = position;
            puffObject.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            var puffRenderer = puffObject.GetComponent<SpriteRenderer>();
            puffRenderer.sprite = sprite;
            puffRenderer.sortingOrder = SortingOrder;
            puffRenderer.flipX = Random.value < 0.5f;

            float spriteWidth = Mathf.Max(0.0001f, sprite.bounds.size.x);
            var puff = new Puff
            {
                Renderer = puffRenderer,
                Drift = drift,
                Life = Mathf.Max(0.05f, life),
                StartScale = startWidth / spriteWidth,
                EndScale = endWidth / spriteWidth,
                Spin = Random.Range(-60f, 60f),
                Alpha = alpha
            };
            puffs.Add(puff);
            Apply(puff, 0f);
        }

        // Called when the thief is gone; the object removes itself once the last puff fades.
        public void ReleaseWhenDone()
        {
            ownerGone = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.Age += dt;
                float t = puff.Age / puff.Life;
                if (t >= 1f)
                {
                    Destroy(puff.Renderer.gameObject);
                    puffs.RemoveAt(i);
                    continue;
                }

                Transform puffTransform = puff.Renderer.transform;
                puffTransform.position += puff.Drift * dt * (1f - t);
                puffTransform.rotation *= Quaternion.Euler(0f, 0f, puff.Spin * dt);
                Apply(puff, t);
            }

            if (ownerGone && puffs.Count == 0) Destroy(gameObject);
        }

        private static void Apply(Puff puff, float t)
        {
            // Billows out quickly, then slows; fades in fast and out gently.
            float grow = 1f - (1f - t) * (1f - t);
            puff.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(puff.StartScale, puff.EndScale, grow);
            float fadeIn = Mathf.Clamp01(t / 0.12f);
            float fadeOut = 1f - Mathf.SmoothStep(0.35f, 1f, t);
            Color color = puff.Renderer.color;
            color.a = puff.Alpha * fadeIn * fadeOut;
            puff.Renderer.color = color;
        }
    }
}
