using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The park's mud (stage 2): a few puddles (count, picked at random) laid at random spots at the
    // stage's start, clear of the hiding spots, the police tape crate and each other. A thief in the mud runs at thiefSpeedInMud of its
    // speed, so chasing one in is worth it; a player standing in it locks a stun gun on at
    // lockSpeedInMud of the usual pace (StunGuns asks LockRateAt), so shooting from the mud is slow.
    // Mud helps either side; the players choose between chasing and shooting. Gone when the floor is
    // cleared.
    public sealed class MudPuddles : MonoBehaviour
    {
        // One puddle: where and how big its mud is (half widths across and along the floor), never
        // turned, so it lines up with the floor painting.
        private struct Puddle
        {
            public Vector3 Centre;
            public Vector2 HalfSize;
            public GameObject Picture;
        }

        [SerializeField] private BountyRounds rounds;
        // The thieves (slowed) and their hiding spots (kept clear of).
        [SerializeField] private ThiefSpawner thieves;
        // The police tape crate, kept clear of; may be empty.
        [SerializeField] private ParkPoliceTape policeTape;
        [SerializeField] private int stage = 2;
        [SerializeField] private Vector2Int puddleCount = new Vector2Int(4, 6);
        // A puddle picture's width (metres; its depth follows the picture's shape).
        [SerializeField, Min(0.2f)] private float width = 1.5f;
        [SerializeField, Range(0.1f, 1f)] private float thiefSpeedInMud = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float lockSpeedInMud = 0.5f;
        // Where puddles may lie (x and world z), and how far apart from what they keep clear of.
        [SerializeField] private Vector2 areaMin = new Vector2(-4.2f, -1.6f);
        [SerializeField] private Vector2 areaMax = new Vector2(4.2f, 1.6f);
        [SerializeField, Min(0f)] private float gap = 0.4f;
        [SerializeField] private Sprite mud;

        private const float Height = 0.015f;
        // Under the paw prints (3) and every shadow, on the floor itself.
        private const int Order = 2;
        private const int SpotTries = 60;
        // The picture's edge fades out into the floor (Tools/ParkArt/soften_mud.py): the mud that
        // slows is about this share of its width and depth.
        private static readonly Vector2 MudShare = new Vector2(0.82f, 0.66f);

        private readonly List<Puddle> puddles = new List<Puddle>();
        // Laid a frame after the stage starts, once the crate has found its spot.
        private bool layNextFrame;

        // The puddles were laid for the stage (their centres).
        public event Action<IReadOnlyList<Vector3>> Laid;

        private void OnEnable()
        {
            if (rounds == null) return;
            rounds.StageStarted += OnStageStarted;
            rounds.FloorCleared += Clear;
        }

        private void OnDisable()
        {
            if (rounds != null)
            {
                rounds.StageStarted -= OnStageStarted;
                rounds.FloorCleared -= Clear;
            }

            Clear();
        }

        // How fast a stun gun locks on for a player standing here (1 off the mud).
        public float LockRateAt(Vector3 position)
        {
            return InMud(position) ? lockSpeedInMud : 1f;
        }

        public bool InMud(Vector3 position)
        {
            foreach (Puddle puddle in puddles)
            {
                if (Inside(puddle, position)) return true;
            }

            return false;
        }

        private void OnStageStarted(int started)
        {
            Clear();
            layNextFrame = started == stage;
        }

        private void Update()
        {
            if (layNextFrame)
            {
                layNextFrame = false;
                Lay();
            }

            if (thieves == null || puddles.Count == 0) return;
            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief != null) thief.SpeedFactor = InMud(thief.transform.position) ? thiefSpeedInMud : 1f;
            }
        }

        private void Lay()
        {
            var centres = new List<Vector3>();
            int count = Random.Range(puddleCount.x, Mathf.Max(puddleCount.x, puddleCount.y) + 1);
            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpot(out Puddle puddle)) continue;

                puddle.Picture = Picture(puddle);
                puddles.Add(puddle);
                centres.Add(puddle.Centre);
            }

            Debug.Log($"[MudPuddles] {puddles.Count} mud puddles laid", this);
            Laid?.Invoke(centres);
        }

        private void Clear()
        {
            layNextFrame = false;
            foreach (Puddle puddle in puddles)
            {
                if (puddle.Picture != null) Destroy(puddle.Picture);
            }

            puddles.Clear();
            if (thieves == null) return;
            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief != null) thief.SpeedFactor = 1f;
            }
        }

        private bool TryFindSpot(out Puddle puddle)
        {
            float aspect = mud != null ? mud.bounds.size.y / Mathf.Max(0.01f, mud.bounds.size.x) : 0.5f;
            var half = Vector2.Scale(new Vector2(width * 0.5f, width * aspect * 0.5f), MudShare);
            for (int attempt = 0; attempt < SpotTries; attempt++)
            {
                puddle = new Puddle
                {
                    Centre = new Vector3(Random.Range(areaMin.x + half.x, areaMax.x - half.x), 0f, Random.Range(areaMin.y + half.y, areaMax.y - half.y)),
                    HalfSize = half,
                };
                if (IsClear(puddle)) return true;
            }

            puddle = default;
            return false;
        }

        // Clear of the hiding spots, the crate and the other puddles (taking each puddle as the circle
        // round its longer side, which is enough to keep them apart).
        private bool IsClear(Puddle puddle)
        {
            float reachOut = Mathf.Max(puddle.HalfSize.x, puddle.HalfSize.y);
            if (thieves != null)
            {
                foreach (HidingBush bush in thieves.Bushes)
                {
                    if (bush != null && Flat(puddle.Centre, bush.Center) < reachOut + bush.Radius + gap) return false;
                }
            }

            if (policeTape != null)
            {
                foreach (Vector3 crate in policeTape.Spots)
                {
                    if (Flat(puddle.Centre, crate) < reachOut + gap + 0.6f) return false;
                }
            }

            foreach (Puddle other in puddles)
            {
                if (Flat(puddle.Centre, other.Centre) < reachOut + Mathf.Max(other.HalfSize.x, other.HalfSize.y) + gap) return false;
            }

            return true;
        }

        // Inside the puddle's oval.
        private static bool Inside(Puddle puddle, Vector3 position)
        {
            Vector3 offset = position - puddle.Centre;
            float x = offset.x / puddle.HalfSize.x;
            float z = offset.z / puddle.HalfSize.y;
            return x * x + z * z <= 1f;
        }

        private GameObject Picture(Puddle puddle)
        {
            var holder = new GameObject("Mud Puddle", typeof(SpriteRenderer));
            holder.transform.SetParent(transform, false);
            holder.transform.SetPositionAndRotation(new Vector3(puddle.Centre.x, Height, puddle.Centre.z), Quaternion.Euler(90f, 0f, 0f));
            var picture = holder.GetComponent<SpriteRenderer>();
            picture.sprite = mud;
            picture.sortingOrder = Order;
            picture.flipX = Random.value < 0.5f;
            if (mud != null)
            {
                Vector3 drawn = mud.bounds.size;
                Vector2 size = new Vector2(puddle.HalfSize.x * 2f / MudShare.x, puddle.HalfSize.y * 2f / MudShare.y);
                holder.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, drawn.x), size.y / Mathf.Max(0.01f, drawn.y), 1f);
            }

            return holder;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
