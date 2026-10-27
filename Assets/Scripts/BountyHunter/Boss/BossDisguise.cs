using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss passing for a passer-by: a copy of that kind's picture (with its walk) in place of the
    // boss's own, but the boss's top hat stays on and the copy flickers in shimmerColor now and then,
    // and the hat pops up bigger with a bounce and a wiggle every few seconds, so a sharp-eyed player
    // can tell it apart. The hat stands upright over the middle of the body,
    // a little above the top of the picture, and follows it as the frames change. Sits on a child of the boss,
    // which moves it.
    public sealed class BossDisguise : MonoBehaviour
    {
        private const float HatLift = 0.01f;
        // The gap between the top of the head and the hat's brim (metres).
        private const float HatGap = 0.03f;
        private const float HatFollowRate = 18f;
        // The give-away: every so often the hat pops up bigger with a bounce and a wiggle, then
        // settles. It swells to HatPopSwell times its size over the first part of HatPopSeconds,
        // hopping HatPopHop metres, and wobbles up to HatPopWobble degrees either way as it settles.
        private const float HatPopSeconds = 0.8f;
        private const float HatPopRise = 0.22f;
        private const float HatPopSwell = 0.45f;
        private const float HatPopHop = 0.05f;
        private const float HatPopWobble = 16f;
        private static readonly Vector2 HatPopEvery = new Vector2(2.2f, 3.4f);
        // The first pop comes this soon after it has put the disguise on.
        private const float FirstHatPop = 0.8f;
        private const float ShimmerSeconds = 0.3f;

        // Each picture's middle x and top y (in the sprite's own units), worked out once.
        private readonly Dictionary<Sprite, Vector2> heads = new Dictionary<Sprite, Vector2>();

        private GameObject look;
        private SpriteRenderer lookBody;
        private ThiefFrameAnimation lookFrames;
        private bool artFacesRight;
        private SpriteRenderer hat;
        private float hatScale;
        private bool hatPlaced;
        // Where the hat would sit, followed smoothly; the pop is added on top.
        private Vector3 hatSpot;
        private float sinceHatPop;
        private float nextHatPop;
        private Color shimmerColor;
        private float shimmerEvery;
        private float sinceShimmer;
        private float alpha = 1f;

        public bool IsOn => look != null;

        public static BossDisguise Create(Transform boss)
        {
            var holder = new GameObject("Disguise");
            holder.transform.SetParent(boss, false);
            return holder.AddComponent<BossDisguise>();
        }

        // Puts on the look of kind (a passer-by prefab); its frames play as the boss moves.
        public void Wear(NpcWalker kind, BossSettings settings)
        {
            TakeOff();
            var frames = kind.GetComponentInChildren<ThiefFrameAnimation>(true);
            GameObject source = frames != null ? frames.gameObject : kind.GetComponentInChildren<SpriteRenderer>(true).gameObject;
            look = Instantiate(source, transform, false);
            look.name = $"{kind.name} Look";
            lookBody = look.GetComponent<SpriteRenderer>();
            lookFrames = look.GetComponent<ThiefFrameAnimation>();
            StandOnFeet();
            artFacesRight = kind.ArtFacesRight;
            shimmerColor = settings.ShimmerColor;
            shimmerEvery = settings.ShimmerEverySeconds;
            sinceShimmer = 0f;
            alpha = 1f;
            if (settings.Hat != null && lookBody != null) PutOnHat(settings.Hat, settings.HatWidth);
        }

        // The passers-by's pictures are centred on their spot, the boss's stand on its feet: the look
        // moves up (towards the wall) until the bottom of its outline is where the boss's feet are.
        private void StandOnFeet()
        {
            if (lookBody == null || lookBody.sprite == null) return;

            float bottom = 0f;
            foreach (Vector2 point in lookBody.sprite.vertices) bottom = Mathf.Min(bottom, point.y);
            Vector3 feet = lookBody.transform.TransformPoint(new Vector3(0f, bottom, 0f));
            look.transform.position += new Vector3(0f, 0f, transform.position.z - feet.z);
        }

        // How solid the look and its hat are (0..1), for fading between the boss and the disguise.
        public void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            ApplyColour();
        }

        public void TakeOff()
        {
            if (look != null) Destroy(look);
            if (hat != null) Destroy(hat.gameObject);
            look = null;
            lookBody = null;
            lookFrames = null;
            hat = null;
        }

        // Turns the look the way it walks and keeps a hopper's frames going (they play while it wants to move).
        public void Walk(Vector3 velocity)
        {
            if (lookBody == null) return;

            if (Mathf.Abs(velocity.x) > 0.01f) lookBody.flipX = velocity.x > 0f != artFacesRight;
            if (lookFrames != null) lookFrames.MoveIntent = velocity.magnitude;
        }

        private void PutOnHat(Sprite hatSprite, float width)
        {
            var hatObject = new GameObject("Top Hat");
            hatObject.transform.SetParent(transform, false);
            hatObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            hat = hatObject.AddComponent<SpriteRenderer>();
            hat.sprite = hatSprite;
            hat.sortingLayerID = lookBody.sortingLayerID;
            hat.sortingOrder = lookBody.sortingOrder + 1;
            hatScale = width / hatSprite.bounds.size.x;
            hatObject.transform.localScale = Vector3.one * hatScale;
            hatPlaced = false;
            sinceHatPop = 0f;
            nextHatPop = FirstHatPop;
            PlaceHat();
        }

        // Brim HatGap over the top of the head, centred on it. The picture lies flat with its top
        // towards the wall, so "above" is +z.
        private void PlaceHat()
        {
            if (hat == null || lookBody == null || lookBody.sprite == null) return;

            Vector2 head = HeadOf(lookBody.sprite);
            if (lookBody.flipX) head.x = -head.x;
            Vector3 top = lookBody.transform.TransformPoint(new Vector3(head.x, head.y, 0f));
            var wanted = new Vector3(top.x, top.y + HatLift, top.z + HatGap);
            hatSpot = hatPlaced ? Vector3.Lerp(hatSpot, wanted, 1f - Mathf.Exp(-HatFollowRate * Time.deltaTime)) : wanted;
            hatPlaced = true;

            Pop(out float swell, out float hop, out float wobble);
            float scale = hatScale * (1f + swell);
            // Grows from its brim, so it swells upwards off the head instead of into it.
            float brimToMiddle = -hat.sprite.bounds.min.y * scale;
            hat.transform.position = hatSpot + new Vector3(0f, 0f, brimToMiddle + hop);
            hat.transform.localScale = Vector3.one * scale;
            hat.transform.rotation = Quaternion.AngleAxis(wobble, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        }

        // The hat's pop now: how much bigger (share), how high it has hopped and how far it leans.
        // It swells quickly with a little overshoot, then shrinks back while wobbling, both easing
        // out to nothing, so it starts and ends at rest.
        private void Pop(out float swell, out float hop, out float wobble)
        {
            swell = 0f;
            hop = 0f;
            wobble = 0f;
            sinceHatPop += Time.deltaTime;
            if (sinceHatPop < nextHatPop) return;

            float t = (sinceHatPop - nextHatPop) / HatPopSeconds;
            if (t >= 1f)
            {
                sinceHatPop = 0f;
                nextHatPop = Random.Range(HatPopEvery.x, HatPopEvery.y);
                return;
            }

            float rising = Mathf.Clamp01(t / HatPopRise);
            // Ease out with an overshoot on the way up, then a smooth fall back.
            float up = 1f + 2.2f * Mathf.Pow(rising - 1f, 3f) + 1.2f * Mathf.Pow(rising - 1f, 2f);
            float down = t <= HatPopRise ? 1f : 1f - Smooth((t - HatPopRise) / (1f - HatPopRise));
            float amount = up * down;
            swell = HatPopSwell * amount;
            hop = HatPopHop * amount;
            wobble = HatPopWobble * Mathf.Sin(t * 3f * 2f * Mathf.PI) * (1f - Smooth(t));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // The middle of the picture's outline from side to side and its highest point (the tight
        // outline, so empty margins do not count; a hat or ear leaning to one side does not pull the
        // top hat off the middle of the body).
        private Vector2 HeadOf(Sprite sprite)
        {
            if (heads.TryGetValue(sprite, out Vector2 head)) return head;

            float top = float.MinValue;
            float left = float.MaxValue;
            float right = float.MinValue;
            foreach (Vector2 point in sprite.vertices)
            {
                top = Mathf.Max(top, point.y);
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
            }

            head = new Vector2((left + right) * 0.5f, top);
            heads[sprite] = head;
            return head;
        }

        private void LateUpdate()
        {
            PlaceHat();
        }

        private void Update()
        {
            if (lookBody == null) return;

            sinceShimmer += Time.deltaTime;
            if (sinceShimmer >= shimmerEvery) sinceShimmer = 0f;
            ApplyColour();
        }

        private void ApplyColour()
        {
            if (lookBody == null) return;

            float glow = sinceShimmer < ShimmerSeconds ? Mathf.Sin(sinceShimmer / ShimmerSeconds * Mathf.PI) : 0f;
            Color colour = Color.Lerp(Color.white, shimmerColor, glow);
            colour.a = alpha;
            lookBody.color = colour;
            if (hat != null) hat.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
