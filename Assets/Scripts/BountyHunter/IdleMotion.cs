using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // A gentle looping motion for a UI piece at rest, in one of a few styles, each repeating every
    // seconds. Phase shifts it, in parts of a cycle, so pieces sharing a style move one after another.
    //   Wave: bobs back and forth by bob (parent units), grows by up to pulse and turns by up to sway degrees.
    //   Hop: a little jump bob.y high once a cycle, squashing before and after it by pulse, then rests.
    //   Jelly: a soft wobble once a cycle, wider then taller by pulse and dying away, then rests.
    //   Flutter: stretches wide and narrow by pulse, slowly, like a banner in a breeze.
    //   TickTock: tilts sway degrees one way, then the other, like a clock's tick and tock, each a
    //   quick springy turn and then a hold; one tick and one tock per cycle.
    [Serializable]
    public sealed class IdleMotion
    {
        public enum Style
        {
            Wave,
            Hop,
            Jelly,
            Flutter,
            TickTock,
        }

        [SerializeField] private RectTransform piece;
        [SerializeField] private Style style;
        [SerializeField] private Vector2 bob;
        [SerializeField, Range(0f, 0.3f)] private float pulse;
        [SerializeField, Range(0f, 20f)] private float sway;
        [SerializeField, Min(0.1f)] private float seconds = 2f;
        [SerializeField, Range(0f, 1f)] private float phase;

        // Parts of the cycle the hop and the wobble take; the rest of it is still.
        private const float HopPart = 0.32f;
        private const float JellyPart = 0.3f;

        public RectTransform Piece => piece;

        // Moves the piece around its rest, age seconds into the motion; strength 0 to 1 eases it in.
        public void Apply(Vector2 restPosition, Vector3 restScale, Quaternion restRotation, float age, float strength)
        {
            float cycle = Mathf.Repeat(age / seconds + phase, 1f);
            Vector2 offset = Vector2.zero;
            Vector2 stretch = Vector2.one;
            float turn = 0f;

            switch (style)
            {
                case Style.Hop:
                    Hop(cycle / HopPart, ref offset, ref stretch);
                    break;
                case Style.Jelly:
                    Jelly(cycle / JellyPart, ref stretch);
                    break;
                case Style.TickTock:
                    turn = TickTock(cycle);
                    break;
                case Style.Flutter:
                    float breeze = Mathf.Sin(cycle * Mathf.PI * 2f);
                    stretch = new Vector2(1f + pulse * breeze, 1f - pulse * 0.4f * breeze);
                    break;
                default:
                    float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
                    // The pulse beats from its rest size upward, so the piece never shrinks below it.
                    float beat = 0.5f - 0.5f * Mathf.Cos(cycle * Mathf.PI * 2f);
                    offset = bob * wave;
                    stretch = Vector2.one * (1f + pulse * beat);
                    turn = sway * wave;
                    break;
            }

            piece.anchoredPosition = restPosition + offset * strength;
            Vector2 scale = Vector2.LerpUnclamped(Vector2.one, stretch, strength);
            piece.localScale = new Vector3(restScale.x * scale.x, restScale.y * scale.y, restScale.z);
            piece.localRotation = restRotation * Quaternion.Euler(0f, 0f, turn * strength);
        }

        // u runs 0 to 1 over the hop, past 1 it rests: crouch, jump up and down, land with a squash.
        private void Hop(float u, ref Vector2 offset, ref Vector2 stretch)
        {
            if (u >= 1f) return;

            if (u < 0.2f)
            {
                float crouch = Mathf.Sin(u / 0.2f * Mathf.PI);
                stretch = new Vector2(1f + pulse * 0.6f * crouch, 1f - pulse * crouch);
                return;
            }

            if (u < 0.75f)
            {
                float air = Mathf.Sin((u - 0.2f) / 0.55f * Mathf.PI);
                offset = new Vector2(0f, bob.y * air);
                stretch = new Vector2(1f - pulse * 0.2f * air, 1f + pulse * 0.5f * air);
                return;
            }

            float land = Mathf.Sin((u - 0.75f) / 0.25f * Mathf.PI);
            stretch = new Vector2(1f + pulse * 0.6f * land, 1f - pulse * land);
        }

        // The tilt cycle of the way through: the first half ticks over to +sway, the second tocks
        // back to -sway, each turn taking the first part of its half and springing a little past.
        private float TickTock(float cycle)
        {
            const float turnPart = 0.35f;
            bool tock = cycle >= 0.5f;
            float u = Mathf.Clamp01((tock ? cycle - 0.5f : cycle) * 2f / turnPart);
            float from = tock ? sway : -sway;
            float to = tock ? -sway : sway;
            return Mathf.LerpUnclamped(from, to, EaseOutBack(u));
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.4f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        // u runs 0 to 1 over the wobble, past 1 it rests: three soft swings that die away.
        private void Jelly(float u, ref Vector2 stretch)
        {
            if (u >= 1f) return;

            float swing = Mathf.Sin(u * Mathf.PI * 6f) * (1f - u) * (1f - u);
            stretch = new Vector2(1f + pulse * swing, 1f - pulse * swing);
        }
    }
}
