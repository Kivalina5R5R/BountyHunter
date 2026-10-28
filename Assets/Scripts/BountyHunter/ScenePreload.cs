using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Advances one small preparation step per frame on Unity's main thread.
    // Lower priorities run first; an imminent consumer can promote its existing work.
    public sealed class ScenePreload : MonoBehaviour
    {
        public sealed class Ticket
        {
            internal UnityEngine.Object Owner { get; }
            internal IEnumerator Work { get; }
            internal int Priority { get; private set; }
            public bool Completed { get; internal set; }
            public bool Failed { get; internal set; }

            internal Ticket(UnityEngine.Object owner, int priority, IEnumerator work)
            {
                Owner = owner;
                Priority = priority;
                Work = work;
            }

            public void Prioritize() => Priority = -1;
            public void Cancel() => Completed = true;
        }

        private static readonly ProfilerMarker StepMarker = new ProfilerMarker("BountyHunter.PreloadStep");
        private static ScenePreload instance;
        private readonly List<Ticket> pending = new List<Ticket>();

        // Work must yield null between bounded steps, rather than nested coroutines or wait objects.
        public static Ticket Enqueue(UnityEngine.Object owner, int priority, IEnumerator work)
        {
            if (instance == null) instance = new GameObject("Scene Preload").AddComponent<ScenePreload>();
            var ticket = new Ticket(owner, priority, work);
            instance.pending.Add(ticket);
            return ticket;
        }

        private void Update()
        {
            Ticket next = null;
            foreach (Ticket ticket in pending)
                if (next == null || ticket.Priority < next.Priority) next = ticket;
            if (next == null) return;

            try
            {
                using (StepMarker.Auto())
                {
                    if (!next.Completed && next.Owner != null && next.Work.MoveNext()) return;
                }
            }
            catch (Exception fault)
            {
                next.Failed = true;
                Debug.LogException(fault, next.Owner);
            }
            Complete(next);
            pending.Remove(next);
        }

        private static void Complete(Ticket ticket)
        {
            ticket.Completed = true;
            (ticket.Work as IDisposable)?.Dispose();
        }

        private void OnDestroy()
        {
            foreach (Ticket ticket in pending) Complete(ticket);
            pending.Clear();
            if (instance == this) instance = null;
        }
    }
}
