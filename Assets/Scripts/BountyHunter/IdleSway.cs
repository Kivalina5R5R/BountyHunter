using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Keeps still pieces of a wall panel gently alive: each listed piece rocks a little either way,
    // drifts up and down and swells slightly, each to its own slow beat, round where it sits in the
    // prefab. Put on the panel's root; leave an amount at 0 for no such motion.
    public sealed class IdleSway : MonoBehaviour
    {
        [Serializable]
        private sealed class Piece
        {
            [SerializeField] private RectTransform target;
            // Degrees either way it rocks.
            [SerializeField, Range(0f, 10f)] private float rock = 1f;
            // How far it drifts up and down (wall units).
            [SerializeField, Min(0f)] private float drift;
            // How much it swells, as a share of its size.
            [SerializeField, Range(0f, 0.2f)] private float swell;
            // Beats a second, and where in its beat it starts (so pieces do not move together).
            [SerializeField, Min(0.01f)] private float speed = 1f;
            [SerializeField] private float phase;

            public RectTransform Target => target;
            public float Rock => rock;
            public float Drift => drift;
            public float Swell => swell;
            public float Speed => speed;
            public float Phase => phase;
        }

        [SerializeField] private Piece[] pieces;

        private Vector2[] rests;
        private Quaternion[] turns;
        private Vector3[] sizes;

        private void Awake()
        {
            rests = new Vector2[pieces.Length];
            turns = new Quaternion[pieces.Length];
            sizes = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                RectTransform target = pieces[i]?.Target;
                if (target == null) continue;

                rests[i] = target.anchoredPosition;
                turns[i] = target.localRotation;
                sizes[i] = target.localScale;
            }
        }

        private void Update()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                Piece piece = pieces[i];
                if (piece?.Target == null) continue;

                float beat = Time.time * piece.Speed * Mathf.PI * 2f + piece.Phase;
                piece.Target.localRotation = turns[i] * Quaternion.Euler(0f, 0f, piece.Rock * Mathf.Sin(beat));
                piece.Target.anchoredPosition = rests[i] + new Vector2(0f, piece.Drift * Mathf.Sin(beat * 0.8f + 1f));
                piece.Target.localScale = sizes[i] * (1f + piece.Swell * (0.5f + 0.5f * Mathf.Sin(beat * 0.6f)));
            }
        }
    }
}
