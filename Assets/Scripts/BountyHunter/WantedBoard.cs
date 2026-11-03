using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The WANTED posters on the wall. Each round pins a random few up side by side, kept centred on
    // where the posters sit in the scene, each with a slight random tilt and a fresh random bounty.
    // More can be pinned up mid-round (AddPosters, the market's loudspeaker), up to mostShown; a row
    // longer than fullSizeUpTo posters shrinks its posters and their spacing to fit the width that
    // many take, and grows back as it gets shorter. One poster per thief while there are enough kinds
    // of thief; past that a thief's poster may show twice (a spare copy is made for it at the start).
    // A caught thief's poster is stamped and torn down and the rest close up, still centred. Posters
    // with no thief tied to them are never picked.
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
        // Most posters on the wall at once, and how many fit at full size.
        [SerializeField, Min(1)] private int mostShown = 5;
        [SerializeField, Min(1)] private int fullSizeUpTo = 3;

        private readonly List<WantedPoster> shown = new List<WantedPoster>();
        private readonly Dictionary<WantedPoster, PosterMotion> motions = new Dictionary<WantedPoster, PosterMotion>();
        private Vector2 centre;
        private float posterWidth;
        // The share of full size the row's posters and spacing have now.
        private float size = 1f;

        public IReadOnlyList<WantedPoster> Shown => shown;
        // Gap between posters being pinned up, left to right.
        public float PinStagger => pinStagger;
        public int MostShown => mostShown;
        // Posters put up: how many, the delay before the first and the time between them (for sounds).
        public event Action<int, float, float> PostersPutUp;

        // Takes the posters off the wall straight away, so the first round drops them in, and makes
        // the spare copies needed to show mostShown posters.
        private void Awake()
        {
            if (posters == null) return;

            var all = new List<WantedPoster>();
            var kinds = new List<ThiefController>();
            foreach (WantedPoster poster in posters)
            {
                if (poster == null) continue;

                all.Add(poster);
                if (poster.Thief != null && !kinds.Contains(poster.Thief)) kinds.Add(poster.Thief);
            }

            if (all.Count == 0) return;

            // The posters are stacked in the scene; their spot is the centre of the row.
            centre = all[0].Rect.anchoredPosition;
            // Copied before any is made ready, so a copy carries no pin or stamp of its own.
            all.AddRange(Spares(all, kinds.Count));
            foreach (WantedPoster poster in all) Ready(poster);
        }

        // Copies of the posters so more thieves than there are kinds can be wanted at once.
        private List<WantedPoster> Spares(List<WantedPoster> originals, int kinds)
        {
            var spares = new List<WantedPoster>();
            List<WantedPoster> withThief = originals.FindAll(poster => poster.Thief != null);
            for (int i = 0; i < mostShown - kinds && withThief.Count > 0; i++)
            {
                WantedPoster original = withThief[i % withThief.Count];
                WantedPoster spare = Instantiate(original, original.transform.parent);
                spare.name = original.name + " (spare)";
                spares.Add(spare);
            }

            return spares;
        }

        private void Ready(WantedPoster poster)
        {
            PosterMotion motion = poster.GetComponent<PosterMotion>();
            if (motion == null) motion = poster.gameObject.AddComponent<PosterMotion>();
            motion.Initialize((RectTransform)transform, poster.Font);
            motion.HideNow();
            motions[poster] = motion;
            posterWidth = Mathf.Max(posterWidth, motion.FullWidth);
        }

        // After delay seconds, pins up count posters (fewer when not enough have a thief) and returns them.
        public IReadOnlyList<WantedPoster> PutUp(int count, float delay)
        {
            var previous = new List<WantedPoster>(shown);
            shown.Clear();
            shown.AddRange(Pick(Mathf.Min(count, mostShown)));

            foreach (WantedPoster poster in previous)
            {
                if (!shown.Contains(poster)) motions[poster].TakeDown();
            }

            Fit();
            for (int i = 0; i < shown.Count; i++)
            {
                WantedPoster poster = shown[i];
                poster.SetBounty(Random.Range(bountyRange.x, bountyRange.y + 1));
                motions[poster].Appear(Slot(i), Random.Range(-maxTiltDegrees, maxTiltDegrees), delay + i * pinStagger);
            }

            PostersPutUp?.Invoke(shown.Count, delay, pinStagger);
            return shown;
        }

        // Pins up to count more posters at the end of the row (never past mostShown), the row shrinking
        // to fit and sliding over; returns the new ones.
        public IReadOnlyList<WantedPoster> AddPosters(int count, float delay)
        {
            List<WantedPoster> added = Pick(Mathf.Min(count, mostShown - shown.Count));
            if (added.Count == 0) return added;

            int first = shown.Count;
            shown.AddRange(added);
            Fit();
            for (int i = 0; i < first; i++) motions[shown[i]].SlideTo(Slot(i), 0f);

            for (int i = 0; i < added.Count; i++)
            {
                WantedPoster poster = added[i];
                poster.SetBounty(Random.Range(bountyRange.x, bountyRange.y + 1));
                motions[poster].Appear(Slot(first + i), Random.Range(-maxTiltDegrees, maxTiltDegrees), delay + i * pinStagger);
            }

            PostersPutUp?.Invoke(added.Count, delay, pinStagger);
            return added;
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
            Fit();
            for (int i = 0; i < shown.Count; i++)
            {
                motions[shown[i]].SlideTo(Slot(i), closeUpDelay);
            }
        }

        // Sizes the row's posters so it is no wider than fullSizeUpTo posters at full size.
        private void Fit()
        {
            float Width(int count) => Mathf.Max(0, count - 1) * spacing + posterWidth;
            size = shown.Count <= fullSizeUpTo ? 1f : Width(fullSizeUpTo) / Mathf.Max(0.0001f, Width(shown.Count));
            foreach (WantedPoster poster in shown) motions[poster].SetSize(size);
        }

        private Vector2 Slot(int index)
        {
            float offset = (index - (shown.Count - 1) * 0.5f) * spacing * size;
            return new Vector2(centre.x + offset, centre.y);
        }

        // Up to count posters not on the wall, at random: one per thief not wanted yet first, then
        // the spare copies of thieves already up.
        private List<WantedPoster> Pick(int count)
        {
            var picked = new List<WantedPoster>();
            var free = new List<WantedPoster>();
            foreach (WantedPoster poster in motions.Keys)
            {
                if (poster.Thief != null && !shown.Contains(poster)) free.Add(poster);
            }

            while (picked.Count < count && free.Count > 0)
            {
                List<WantedPoster> fresh = free.FindAll(poster => !IsUp(poster.Thief, picked));
                List<WantedPoster> from = fresh.Count > 0 ? fresh : free;
                WantedPoster poster = from[Random.Range(0, from.Count)];
                picked.Add(poster);
                free.Remove(poster);
            }

            return picked;
        }

        // Whether a poster of this thief is on the wall or about to go up.
        private bool IsUp(ThiefController thief, List<WantedPoster> picked)
        {
            return shown.Exists(poster => poster.Thief == thief) || picked.Exists(poster => poster.Thief == thief);
        }
    }
}
