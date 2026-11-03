using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The boss in the vault (BossVault), the same chameleon as in the chase (BossBody, its walk and
    // disguise): it makes for the diamond, steering round the players, and stands on it to steal it.
    // It moves at approachSpeed at first, sneakSpeed once it has been caught or thrown its smoke bomb,
    // and disguiseSpeed while it passes for a passer-by. A player who comes within bumpRadius catches
    // it (Bumped; not again for bumpCooldownSeconds); the vault then warps it away (WarpTo) and
    // disguises it. Whether it can be seen is the vault's call (Seen); the disguise always shows, as
    // a passer-by does.
    public sealed class VaultBoss : MonoBehaviour
    {
        private enum Mode
        {
            Waiting,
            Approaching,
            Sneaking
        }

        private const float FadeSeconds = 0.15f;
        // How far round a player it steers, and how hard.
        private const float AvoidRadius = 1.6f;
        private const float AvoidWeight = 1.4f;
        // Close enough to the diamond to stand still on it.
        private const float StandOnDiamond = 0.15f;

        private readonly List<Vector3> playerPositions = new List<Vector3>();
        private readonly List<UWBPlayerController> nearPlayers = new List<UWBPlayerController>();
        private BossBody body;
        private SpawnerPlayerLocator players;
        private VaultBoss.Speeds speeds;
        private Vector3 diamond;
        private Mode mode = Mode.Waiting;
        private float bumpReadyAt;
        private float disguisedUntil;
        private float shown;

        // Its speeds and the bump's reach and push.
        [Serializable]
        public struct Speeds
        {
            public float Approach;
            public float Sneak;
            public float Disguised;
            public float BumpRadius;
            public float BumpCooldownSeconds;
        }

        // A player bumped it.
        public event Action<UWBPlayerController> Bumped;

        public Vector3 Position => body.Position;

        // Passing for a passer-by now.
        public bool Disguised => body.Disguise.IsOn;

        // Sneaking back (after a catch or its smoke bomb), when it can only be seen close to.
        public bool Sneaking => mode == Mode.Sneaking;

        // Whether the players can see it (the vault decides each frame); it fades in and out.
        public bool Seen { get; set; } = true;

        // The boss standing at position, out of sight until Arrive.
        public static VaultBoss Create(Transform parent, Vector3 position, Vector3 diamond, BossSettings settings, BossMove walk, SpawnerPlayerLocator players,
            NpcCrowd passersBy, Vector2 areaMin, Vector2 areaMax, Speeds speeds)
        {
            var root = new GameObject("Vault Boss");
            root.transform.SetParent(parent, false);

            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var bodyPicture = bodyObject.AddComponent<SpriteRenderer>();
            bodyPicture.sortingOrder = settings.SortingOrder;
            CharacterShadow.Add(bodyPicture);

            var boss = root.AddComponent<VaultBoss>();
            var animator = bodyObject.AddComponent<BossAnimator>();
            boss.body = new BossBody(root.transform, animator, BossDisguise.Create(root.transform), settings, walk, players, passersBy, areaMin, areaMax);
            boss.players = players;
            boss.speeds = speeds;
            boss.diamond = diamond;
            boss.body.Position = position;
            boss.body.PlayWalk();
            animator.Body.enabled = false;
            return boss;
        }

        // Shows up and makes for the diamond.
        public void Arrive()
        {
            mode = Mode.Approaching;
            body.Animator.Body.enabled = true;
            shown = 1f;
        }

        // From now on it sneaks (its smoke bomb went off).
        public void Sneak()
        {
            if (mode == Mode.Approaching) mode = Mode.Sneaking;
        }

        // Gone from where it is and there at once (after a catch); it sneaks from there.
        public void WarpTo(Vector3 position)
        {
            body.Position = position;
            if (mode != Mode.Waiting) mode = Mode.Sneaking;
        }

        // Passes for kind (a passer-by) for seconds.
        public void Disguise(NpcWalker kind, float seconds)
        {
            if (kind == null || mode == Mode.Waiting) return;

            body.Disguise.Wear(kind, body.Settings);
            body.Animator.Body.enabled = false;
            disguisedUntil = Time.time + seconds;
        }

        // Stands still where it is (the vault is over).
        public void Freeze()
        {
            mode = Mode.Waiting;
            body.Animator.Paused = true;
        }

        private void Update()
        {
            if (mode == Mode.Waiting) return;

            if (body.Disguise.IsOn && Time.time >= disguisedUntil) TakeOffDisguise();
            CheckBump();
            Move();
            ShowSeen();
        }

        // The nearest player in reach bumps it, unless it was bumped a moment ago.
        private void CheckBump()
        {
            if (Time.time < bumpReadyAt) return;

            players.GetPlayersWithin(body.Position, speeds.BumpRadius, nearPlayers);
            if (nearPlayers.Count == 0) return;

            UWBPlayerController bumper = nearPlayers[0];
            float best = float.MaxValue;
            foreach (UWBPlayerController player in nearPlayers)
            {
                float distance = BossBody.FlatDistance(player.transform.position, body.Position);
                if (distance >= best) continue;
                best = distance;
                bumper = player;
            }

            bumpReadyAt = Time.time + speeds.BumpCooldownSeconds;
            TakeOffDisguise();
            Debug.Log($"[VaultBoss] Player {bumper.PlayerId} catches the boss", this);
            Bumped?.Invoke(bumper);
        }

        private void Move()
        {
            Vector3 velocity = Steering() * Speed();
            body.Step(velocity, false);
            if (body.Disguise.IsOn) body.Disguise.Walk(velocity);
            body.FaceWhereMoving();
            body.PaceWalk(velocity.magnitude);
        }

        private float Speed()
        {
            if (body.Disguise.IsOn) return speeds.Disguised;
            return mode == Mode.Sneaking ? speeds.Sneak : speeds.Approach;
        }

        // Towards the diamond, round the players near it, kept off the floor's edges and corners;
        // standing still once it is on the diamond.
        private Vector3 Steering()
        {
            Vector3 position = body.Position;
            Vector3 toDiamond = diamond - position;
            toDiamond.y = 0f;
            float distance = toDiamond.magnitude;
            if (distance <= StandOnDiamond) return Vector3.zero;

            Vector3 wanted = toDiamond / distance;
            players.GetAll(playerPositions);
            foreach (Vector3 player in playerPositions)
            {
                Vector3 away = position - player;
                away.y = 0f;
                float gap = away.magnitude;
                if (gap >= AvoidRadius || gap < 0.001f) continue;
                wanted += away / gap * (1f - gap / AvoidRadius) * AvoidWeight;
            }

            wanted += body.EdgePush(position) + body.CornerPush(position);
            wanted.y = 0f;
            // Slows into its stop on the diamond.
            float slow = Mathf.Clamp01(distance / 0.5f);
            return wanted.sqrMagnitude > 0.0001f ? wanted.normalized * slow : Vector3.zero;
        }

        private void TakeOffDisguise()
        {
            if (!body.Disguise.IsOn) return;

            body.DropDisguise();
            shown = Seen ? 1f : 0f;
        }

        // Its own picture fades in when it can be seen and out when it cannot; a disguise shows as
        // a passer-by does.
        private void ShowSeen()
        {
            if (body.Disguise.IsOn) return;

            shown = Mathf.MoveTowards(shown, Seen ? 1f : 0f, Time.deltaTime / FadeSeconds);
            body.SetAlpha(shown);
        }
    }
}
