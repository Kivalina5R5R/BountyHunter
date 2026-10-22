using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The WANTED posters on the wall. Each round pins a random few up side by side, kept centred on
    // where the posters sit in the scene, each with a slight random tilt and a fresh random bounty.
    // A caught thief's poster is stamped and torn down and the rest close up, still centred. Posters with no thief
    // tied to them are never picked.
    public sealed class WantedBoard : MonoBehaviour
    {
        [SerializeField] private WantedPoster[] posters;
        // Centre to centre, in the posters' parent units.
        [SerializeField, Min(0f)] private float spacing = 160f;
        [SerializeField, Range(0f, 15f)] private float maxTiltDegrees = 4f;
        // Inclusive range of the random bounty on each poster.
        [SerializeField] private Vector2Int bountyRange = new Vector2Int(10, 15);
        // Gap between posters being pinned up, left to right.
        [SerializeField, Min(0f)] private float pinStagger = 0.22f;
        // The row closes up once a torn-down poster is falling away.
        [SerializeField, Min(0f)] private float closeUpDelay = 1.1f;

        private readonly List<WantedPoster> shown = new List<WantedPoster>();
        private readonly Dictionary<WantedPoster, PosterMotion> motions = new Dictionary<WantedPoster, PosterMotion>();
        private Vector2 centre;

        public IReadOnlyList<WantedPoster> Shown => shown;
        // Gap between posters being pinned up, left to right.
        public float PinStagger => pinStagger;
        // Posters put up: how many, the delay before the first and the time between them (for sounds).
        public event Action<int, float, float> PostersPutUp;

        // Takes the posters off the wall straight away, so the first round drops them in.
        private void Awake()
        {
            if (posters == null) return;

            bool centreRead = false;
            foreach (WantedPoster poster in posters)
            {
                if (poster == null) continue;

                // The posters are stacked in the scene; their spot is the centre of the row.
                if (!centreRead)
                {
                    centre = poster.Rect.anchoredPosition;
                    centreRead = true;
                }

                PosterMotion motion = poster.GetComponent<PosterMotion>();
                if (motion == null) motion = poster.gameObject.AddComponent<PosterMotion>();
                motion.Initialize((RectTransform)transform, poster.Font);
                motion.HideNow();
                motions[poster] = motion;
            }
        }

        // After delay seconds, pins up count posters (fewer when not enough have a thief) and returns them.
        public IReadOnlyList<WantedPoster> PutUp(int count, float delay)
        {
            var previous = new List<WantedPoster>(shown);
            shown.Clear();
            List<WantedPoster> candidates = Candidates();
            while (shown.Count < count && candidates.Count > 0)
            {
                WantedPoster picked = candidates[Random.Range(0, candidates.Count)];
                shown.Add(picked);
                // One poster per thief: drops any other poster of the same thief too.
                candidates.RemoveAll(poster => poster.Thief == picked.Thief);
            }

            foreach (WantedPoster poster in previous)
            {
                if (!shown.Contains(poster)) motions[poster].TakeDown();
            }

            for (int i = 0; i < shown.Count; i++)
            {
                WantedPoster poster = shown[i];
                poster.SetBounty(Random.Range(bountyRange.x, bountyRange.y + 1));
                motions[poster].Appear(Slot(i), Random.Range(-maxTiltDegrees, maxTiltDegrees), delay + i * pinStagger);
            }

            PostersPutUp?.Invoke(shown.Count, delay, pinStagger);
            return shown;
        }

        // Takes every poster off the wall at once, without the tear-down (between stages, while the
        // screen is dark, so the next stage's posters come in on an empty board). Every poster goes,
        // also a caught one still being stamped or falling, which would otherwise stay stuck on the
        // board when the stage is hidden mid-way and turn up over the next stage's posters.
        public void TakeDownNow()
        {
            foreach (PosterMotion motion in motions.Values) motion.HideNow();
            shown.Clear();
        }

        // Stamps the caught thief's poster, tears it down and closes the row up around the centre.
        public void MarkCaught(WantedPoster poster)
        {
            if (!shown.Remove(poster)) return;

            motions[poster].Caught();
            for (int i = 0; i < shown.Count; i++)
            {
                motions[shown[i]].SlideTo(Slot(i), closeUpDelay);
            }
        }

        private Vector2 Slot(int index)
        {
            float offset = (index - (shown.Count - 1) * 0.5f) * spacing;
            return new Vector2(centre.x + offset, centre.y);
        }

        private List<WantedPoster> Candidates()
        {
            var candidates = new List<WantedPoster>();
            foreach (WantedPoster poster in motions.Keys)
            {
                if (poster.Thief != null) candidates.Add(poster);
            }

            return candidates;
        }
    }
}
