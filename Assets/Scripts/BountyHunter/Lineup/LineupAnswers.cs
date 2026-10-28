using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Who has locked in which suspect, and after how many clues. A player locks in by standing in a
    // suspect's ring for holdSeconds, and changes their answer the same way on another suspect (the
    // answer then counts as given with the clues up at that moment). A ring counts a player as
    // in from enterRadius and only lets go of them past exitRadius, so a tag shaking on the edge
    // does not start the hold over. A player who is already standing in a ring when the puzzle
    // starts has to step out first, so nobody locks in by accident.
    public sealed class LineupAnswers
    {
        private readonly Func<int, Vector3> standPoint;
        private readonly int rings;
        private readonly float enterRadius;
        private readonly float exitRadius;
        private readonly float holdSeconds;

        private readonly Dictionary<int, Answer> locked = new Dictionary<int, Answer>();
        // The ring each player is in now (-1 none), and how long they have held it.
        private readonly Dictionary<int, int> inRing = new Dictionary<int, int>();
        private readonly Dictionary<int, float> held = new Dictionary<int, float>();
        // Players who have been outside every ring since the puzzle started.
        private readonly HashSet<int> free = new HashSet<int>();
        private readonly float[] fill;

        // suspect: which one they picked; cluesSeen: how many clues were up when they locked in.
        public readonly struct Answer
        {
            public Answer(int suspect, int cluesSeen)
            {
                Suspect = suspect;
                CluesSeen = cluesSeen;
            }

            public int Suspect { get; }
            public int CluesSeen { get; }
        }

        // standPoint: the centre of ring index on the floor.
        public LineupAnswers(Func<int, Vector3> standPoint, int rings, float enterRadius, float exitRadius, float holdSeconds)
        {
            this.standPoint = standPoint;
            this.rings = rings;
            this.enterRadius = enterRadius;
            this.exitRadius = exitRadius;
            this.holdSeconds = holdSeconds;
            fill = new float[rings];
        }

        public IReadOnlyDictionary<int, Answer> Locked => locked;

        // Raised as a player locks in an answer (or changes it), with the player and the suspect.
        public event Action<UWBPlayerController, int> PlayerLocked;

        // Raised the moment a player steps into a ring, with the player and the ring, so the floor can
        // answer the touch before the hold fills.
        public event Action<UWBPlayerController, int> PlayerEntered;

        // 0 to 1: the fullest hold on ring index by a player not yet locked in on it.
        public float Fill(int ring)
        {
            return fill[ring];
        }

        // Follows every player for one frame. cluesSeen: how many clues are up now; while it is 0
        // nobody can lock in yet (the rings still notice who is standing where).
        public void Track(IReadOnlyList<UWBPlayerController> players, float deltaTime, int cluesSeen)
        {
            Array.Clear(fill, 0, fill.Length);
            foreach (UWBPlayerController player in players)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                Follow(player, deltaTime, cluesSeen);
            }
        }

        private void Follow(UWBPlayerController player, float deltaTime, int cluesSeen)
        {
            int id = player.PlayerId;
            int ring = RingOf(id, player.transform.position);
            // Stepping from one ring straight into another starts the hold over.
            bool sameRing = inRing.TryGetValue(id, out int was) && was == ring;
            inRing[id] = ring;
            if (ring >= 0 && !sameRing) PlayerEntered?.Invoke(player, ring);
            if (ring < 0) free.Add(id);

            bool alreadyThere = locked.TryGetValue(id, out Answer answer) && answer.Suspect == ring;
            bool holding = ring >= 0 && free.Contains(id) && cluesSeen > 0 && !alreadyThere;
            float before = sameRing && held.TryGetValue(id, out float time) ? time : 0f;
            float hold = holding ? before + deltaTime : 0f;
            held[id] = hold;
            if (!holding) return;

            fill[ring] = Mathf.Max(fill[ring], hold / holdSeconds);
            if (hold < holdSeconds) return;

            locked[id] = new Answer(ring, cluesSeen);
            held[id] = 0f;
            PlayerLocked?.Invoke(player, ring);
        }

        // The ring the player stands in: the one they were in while still within exitRadius of it,
        // else the first within enterRadius, else -1.
        private int RingOf(int id, Vector3 position)
        {
            if (inRing.TryGetValue(id, out int was) && was >= 0 && Distance(was, position) <= exitRadius) return was;

            for (int ring = 0; ring < rings; ring++)
            {
                if (Distance(ring, position) <= enterRadius) return ring;
            }

            return -1;
        }

        private float Distance(int ring, Vector3 position)
        {
            Vector3 centre = standPoint(ring);
            return new Vector2(position.x - centre.x, position.z - centre.z).magnitude;
        }
    }
}
