using System;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A stun gun's shot: a ball of electricity flying from the shooter to what it was fired at, in the
    // look of the crackling ring round an armed player (StunAura): a white-hot core in a yellow glow
    // with lightning crackling round it, leaving a short trail of sparks. It homes in on its target as
    // that moves, and on reaching it bursts (StunZap, StunSparks) and calls back, so the hit lands as
    // the shot does. Lies just over the floor, over everything on it. Removes itself.
    public sealed class StunShot : MonoBehaviour
    {
        private const float Height = 0.1f;
        // Metres a second, and the longest a shot may fly before it lands anyway.
        private const float Speed = 11f;
        private const float MostSeconds = 1.2f;
        // Close enough to count as there.
        private const float Reach = 0.12f;
        // Sizes across (metres): the core, its glow and the crackle round it.
        private const float CoreSize = 0.16f;
        private const float GlowSize = 0.55f;
        private const float CrackleSize = 0.6f;
        private const float FlicksPerSecond = 16f;
        private const float SpinPerSecond = 540f;
        // Trail sparks left a second, and how long each lasts.
        private const float TrailPerSecond = 60f;
        private const float TrailSeconds = 0.22f;
        private const float TrailSize = 0.12f;
        // Over the floor, the thieves and the passers-by.
        private const int GlowOrder = 30;
        private const int CrackleOrder = 31;
        private const int CoreOrder = 32;
        private static readonly Color CoreColour = new Color(1f, 0.99f, 0.88f, 1f);

        private sealed class Spark
        {
            public SpriteRenderer Picture;
            public float Born;
        }

        private readonly List<Spark> trail = new List<Spark>();
        private Transform target;
        private Vector3 aimAt;
        private Func<Vector3> aim;
        private Action landed;
        private Sprite[] crackles;
        private Color colour;
        private float zapWidth;
        private SpriteRenderer glow;
        private SpriteRenderer crackle;
        private SpriteRenderer core;
        private float age;
        private float owed;
        private bool done;

        // Fires a shot from from at whatever aim says is the target's middle (asked every frame, so it
        // homes in), glowing colour and crackling through crackles; on reaching it, it bursts
        // zapWidth across and landed runs.
        public static void Fire(Vector3 from, Func<Vector3> aim, Color colour, Sprite[] crackles, float zapWidth, Transform parent, Action landed)
        {
            var holder = new GameObject("Stun Shot");
            holder.transform.SetParent(parent, true);
            holder.transform.SetPositionAndRotation(new Vector3(from.x, Height, from.z), Quaternion.Euler(90f, 0f, 0f));
            var shot = holder.AddComponent<StunShot>();
            shot.aim = aim;
            shot.aimAt = aim();
            shot.landed = landed;
            shot.crackles = crackles ?? new Sprite[0];
            shot.colour = colour;
            shot.zapWidth = zapWidth;
            Color glowColour = colour;
            glowColour.a = 0.6f;
            shot.glow = shot.Dot("Glow", PosterSprites.Dot, GlowSize, GlowOrder, glowColour);
            Sprite firstCrackle = shot.crackles.Length > 0 ? shot.crackles[0] : null;
            shot.crackle = shot.Dot("Crackle", firstCrackle, CrackleSize, CrackleOrder, Color.white);
            shot.core = shot.Dot("Core", PosterSprites.Dot, CoreSize, CoreOrder, CoreColour);
            StunSparks.Play(holder.transform.position, colour, 0.5f, parent);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            FadeTrail();
            if (done)
            {
                if (trail.Count == 0) Destroy(gameObject);
                return;
            }

            // Homes in on where the target is now; a target gone keeps the last place it had.
            Vector3 now = aim != null ? aim() : aimAt;
            if (!float.IsNaN(now.x)) aimAt = now;
            Vector3 here = transform.position;
            Vector3 to = new Vector3(aimAt.x, Height, aimAt.z) - here;
            to.y = 0f;
            float step = Speed * dt;
            if (to.magnitude <= Mathf.Max(Reach, step) || age >= MostSeconds)
            {
                transform.position = new Vector3(aimAt.x, Height, aimAt.z);
                Land();
                return;
            }

            transform.position = here + to.normalized * step;
            LeaveTrail(dt);

            if (crackles.Length > 0) crackle.sprite = crackles[Mathf.FloorToInt(age * FlicksPerSecond) % crackles.Length];
            crackle.transform.localRotation = Quaternion.Euler(0f, 0f, age * SpinPerSecond);
            float pulse = 1f + 0.15f * Mathf.Sin(age * 50f);
            glow.transform.localScale = Vector3.one * (Scale(glow.sprite, GlowSize) * pulse);
        }

        // Bursts at the target and hands the hit over; the trail fades out after.
        private void Land()
        {
            done = true;
            StunZap.Play(transform.position, crackles, zapWidth, transform.parent);
            StunSparks.Play(transform.position, colour, 1f, transform.parent);
            glow.enabled = false;
            crackle.enabled = false;
            core.enabled = false;
            landed?.Invoke();
        }

        private void LeaveTrail(float dt)
        {
            owed += TrailPerSecond * dt;
            while (owed >= 1f)
            {
                owed -= 1f;
                Color sparkColour = UnityEngine.Random.value < 0.5f ? CoreColour : colour;
                SpriteRenderer picture = Dot("Trail", PosterSprites.Dot, TrailSize, GlowOrder, sparkColour);
                // Left behind where the shot is now, a touch to either side.
                picture.transform.SetParent(transform.parent, true);
                picture.transform.position += new Vector3(UnityEngine.Random.Range(-0.04f, 0.04f), 0f, UnityEngine.Random.Range(-0.04f, 0.04f));
                trail.Add(new Spark { Picture = picture, Born = Time.time });
            }
        }

        private void FadeTrail()
        {
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                Spark spark = trail[i];
                float t = (Time.time - spark.Born) / TrailSeconds;
                if (t >= 1f || spark.Picture == null)
                {
                    if (spark.Picture != null) Destroy(spark.Picture.gameObject);
                    trail.RemoveAt(i);
                    continue;
                }

                spark.Picture.transform.localScale = Vector3.one * (Scale(spark.Picture.sprite, TrailSize) * (1f - t));
                Color sparkColour = spark.Picture.color;
                sparkColour.a = 1f - t;
                spark.Picture.color = sparkColour;
            }
        }

        private SpriteRenderer Dot(string name, Sprite sprite, float size, int order, Color tint)
        {
            var piece = new GameObject(name, typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            picture.color = tint;
            piece.transform.localScale = Vector3.one * Scale(sprite, size);
            return picture;
        }

        private static float Scale(Sprite sprite, float size)
        {
            return sprite != null ? size / Mathf.Max(0.0001f, sprite.bounds.size.x) : size;
        }

        private void OnDestroy()
        {
            foreach (Spark spark in trail)
            {
                if (spark.Picture != null) Destroy(spark.Picture.gameObject);
            }
        }
    }
}
