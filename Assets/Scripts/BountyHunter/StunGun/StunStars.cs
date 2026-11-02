using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The dizzy stars over a stunned thief's head: a few stars orbiting in a flat ring, each spinning
    // on itself, smaller and dimmer as they pass behind the head; they pop up and fade out as the stun
    // wears off. The head's spot is read once as it starts, so the stars stay put while the thief's
    // breathing frames change. Sits on the thief, so it goes with it when caught.
    public sealed class StunStars : MonoBehaviour
    {
        private const int Count = 4;
        private const float StarWidth = 0.12f;
        // The ring the stars orbit on (metres across the floor and towards the wall), how fast they go
        // round and how fast each spins.
        private const float OrbitX = 0.2f;
        private const float OrbitZ = 0.07f;
        private const float OrbitDegreesPerSecond = 260f;
        private const float SpinDegreesPerSecond = 240f;
        // Just over the top of the head, and lifted off the floor so it draws over the thief.
        private const float AboveHead = 0.03f;
        private const float Lift = 0.06f;
        private const float PopSeconds = 0.2f;
        private const float FadeSeconds = 0.3f;
        // Over the thief (8).
        private const int Order = 12;

        private readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
        private Vector3 head;
        private float starScale;
        private float seconds;
        private float age;

        public static void Play(ThiefController thief, Sprite star, float seconds)
        {
            StunStars old = thief.GetComponentInChildren<StunStars>();
            if (old != null) Destroy(old.gameObject);

            var holder = new GameObject("Stun Stars");
            holder.transform.SetParent(thief.transform, false);
            var effect = holder.AddComponent<StunStars>();
            effect.seconds = seconds;
            effect.head = HeadOf(thief);
            effect.starScale = StarWidth / Mathf.Max(0.01f, star.bounds.size.x);
            for (int i = 0; i < Count; i++)
            {
                var part = new GameObject("Star", typeof(SpriteRenderer));
                part.transform.SetParent(holder.transform, false);
                var picture = part.GetComponent<SpriteRenderer>();
                picture.sprite = star;
                picture.sortingOrder = Order;
                effect.stars.Add(picture);
            }

            effect.Update();
        }

        // The top of the head as the stun starts (the picture stands up towards +z on the floor).
        private static Vector3 HeadOf(ThiefController thief)
        {
            Vector3 at = thief.transform.position;
            if (thief.Body == null) return new Vector3(at.x, at.y + Lift, at.z + 0.5f);

            Bounds outline = thief.Body.bounds;
            return new Vector3(outline.center.x, outline.max.y + Lift, outline.max.z + AboveHead);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }

            float pop = Mathf.Clamp01(age / PopSeconds);
            float grow = pop * (1f + 0.3f * Mathf.Sin(pop * Mathf.PI));
            float fade = Mathf.Clamp01((seconds - age) / FadeSeconds);
            for (int i = 0; i < stars.Count; i++)
            {
                float angle = (age * OrbitDegreesPerSecond + i * 360f / stars.Count) * Mathf.Deg2Rad;
                // Towards the bottom of the floor is in front of the head: bigger and brighter there.
                float front = 0.5f - 0.5f * Mathf.Sin(angle);
                Transform star = stars[i].transform;
                star.position = head + new Vector3(Mathf.Cos(angle) * OrbitX, 0f, Mathf.Sin(angle) * OrbitZ);
                star.rotation = Quaternion.Euler(90f, age * SpinDegreesPerSecond + i * 45f, 0f);
                star.localScale = Vector3.one * (starScale * grow * Mathf.Lerp(0.7f, 1f, front) / Mathf.Max(0.01f, transform.lossyScale.x));
                stars[i].color = new Color(1f, 1f, 1f, fade * Mathf.Lerp(0.65f, 1f, front));
            }
        }
    }
}
