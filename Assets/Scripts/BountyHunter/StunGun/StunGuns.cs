using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The players' stun guns (stages 2 and 3; StunGunPickups hands them out). Nobody aims: a player
    // carrying shots who keeps a thief in the open within lockRange locks onto it, the nearest one,
    // and a ring fills round it over lockSeconds, then the gun fires by itself. Chasing to keep up is
    // the skill, since the tags never stand still enough to aim. Once locked, the thief may get
    // keepRange away before the lock lets go (so it does not flicker at the edge); out of reach the
    // ring drains at half speed rather than starting over, and turning to another thief keeps half
    // of it. A hit stuns the thief for stunSeconds and whoever reaches it first catches it (not
    // necessarily the shooter). A passer-by standing in the line of fire (within npcInTheWay of it)
    // takes the bolt instead: it gets angry and the shooter pays a fine (BountyRounds, NpcZapped).
    // A player carrying shots has a crackling ring round them. Everything goes when the floor is
    // cleared or a new stage starts.
    public sealed class StunGuns : MonoBehaviour
    {
        // The guns' state for one player.
        private sealed class Gun
        {
            public UWBPlayerController Player;
            public int Shots;
            public ThiefController Target;
            public float Charge;
            public SpriteRenderer HolderRing;
            public StunLockRing LockRing;
        }

        // The crackling ring lies just over the floor, under the player's marker (20).
        private const float FloorHeight = 0.05f;
        private const int HolderOrder = 19;

        [SerializeField] private BountyRounds rounds;
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private NpcCrowd passersBy;
        // A player standing in the mud locks on more slowly; may be empty.
        [SerializeField] private MudPuddles mud;

        [Header("Shots")]
        // Each bolt picked up gives this many shots, never more than mostShotsCarried in all: with one
        // shot left, picking up another tops it up to 2, not 3.
        [SerializeField, Min(1)] private int shotsPerBolt = 2;
        [SerializeField, Min(1)] private int mostShotsCarried = 2;

        [Header("Lock-on")]
        [SerializeField, Min(0.1f)] private float lockRange = 2.5f;
        // How far a locked thief may get before the lock lets go.
        [SerializeField, Min(0.1f)] private float keepRange = 2.9f;
        [SerializeField, Min(0.05f)] private float lockSeconds = 1f;
        [SerializeField, Min(0f)] private float stunSeconds = 3f;
        // A passer-by this close to the line of fire takes the bolt instead.
        [SerializeField, Min(0f)] private float npcInTheWay = 0.35f;

        [Header("Look")]
        // The crackling ring round a player carrying shots (white, tinted the bolt's colour), against
        // the player's marker width.
        [SerializeField] private Sprite holderRing;
        [SerializeField, Min(0.1f)] private float holderRingVsPlayer = 1.35f;
        // The lock-on reticle round the target, filling as the lock builds (drawn with the radial
        // fill material); yellow like the lightning bolt icons, so it is not taken for the picking rings,
        // turning red as the lock completes.
        [SerializeField] private Sprite reticleBack;
        [SerializeField] private Sprite reticleFill;
        [SerializeField] private Material radialFill;
        [SerializeField, Min(0.1f)] private float reticleWidth = 0.8f;
        [SerializeField] private Color reticleColour = new Color(1f, 0.8f, 0.2f, 1f);
        // What it turns to as the lock completes.
        [SerializeField] private Color lockedColour = new Color(1f, 0.18f, 0.14f, 1f);
        // The bolt's glow and sparks and the ring round a player carrying shots: yellow like the
        // lightning bolt icons.
        [SerializeField] private Color boltGlowColour = new Color(1f, 0.8f, 0.18f, 1f);
        // One of the dizzy stars orbiting a stunned thief's head.
        [SerializeField] private Sprite stunStar;


        private readonly Dictionary<UWBPlayerController, Gun> guns = new Dictionary<UWBPlayerController, Gun>();
        private readonly List<UWBPlayerController> gone = new List<UWBPlayerController>();

        // A player picked up a gun (shots now), a lock started building on a thief, a gun fired at a
        // thief, and a bolt hit a passer-by in the way instead (with the shooter).
        public event Action<UWBPlayerController, int> Armed;
        public event Action<UWBPlayerController> LockStarted;
        public event Action<UWBPlayerController, ThiefController> Fired;
        public event Action<NpcWalker, UWBPlayerController> NpcZapped;

        private void OnEnable()
        {
            if (rounds == null) return;
            rounds.StageStarted += OnStageStarted;
            rounds.FloorCleared += DisarmAll;
        }

        private void OnDisable()
        {
            if (rounds != null)
            {
                rounds.StageStarted -= OnStageStarted;
                rounds.FloorCleared -= DisarmAll;
            }

            DisarmAll();
        }

        // Hands the player a gun's shots (added to any they carry, up to mostShotsCarried).
        public void Arm(UWBPlayerController player)
        {
            if (player == null) return;

            if (!guns.TryGetValue(player, out Gun gun))
            {
                gun = new Gun { Player = player };
                guns[player] = gun;
            }

            gun.Shots = Mathf.Min(mostShotsCarried, gun.Shots + shotsPerBolt);
            Debug.Log($"[StunGuns] Player {player.PlayerId} picks up a stun gun: {gun.Shots} shots", this);
            Armed?.Invoke(player, gun.Shots);
        }

        // Takes every gun away (a new stage, a mini game, the end).
        public void DisarmAll()
        {
            foreach (Gun gun in guns.Values) RemoveLook(gun);
            guns.Clear();
        }

        private void OnStageStarted(int stage)
        {
            DisarmAll();
        }

        private void Update()
        {
            gone.Clear();
            foreach (Gun gun in guns.Values)
            {
                if (gun.Player == null || gun.Shots <= 0)
                {
                    gone.Add(gun.Player);
                    continue;
                }

                Aim(gun);
                ShowHolder(gun);
            }

            foreach (UWBPlayerController player in gone)
            {
                if (player is null || !guns.TryGetValue(player, out Gun gun)) continue;
                RemoveLook(gun);
                guns.Remove(player);
            }
        }

        // Builds, keeps or drains the lock, and fires once it is full.
        private void Aim(Gun gun)
        {
            Vector3 at = gun.Player.transform.position;
            if (!CanBeHit(gun.Target)) gun.Target = null;

            ThiefController inReach = gun.Target != null && Flat(at, gun.Target.transform.position) <= keepRange
                ? gun.Target
                : NearestThief(at);
            if (inReach == null)
            {
                gun.Charge = Mathf.Max(0f, gun.Charge - Time.deltaTime / lockSeconds * 0.5f);
                if (gun.Charge <= 0f) gun.Target = null;
            }
            else
            {
                if (inReach != gun.Target)
                {
                    if (gun.Target != null) gun.Charge *= 0.5f;
                    else if (gun.Charge <= 0f) LockStarted?.Invoke(gun.Player);
                    gun.Target = inReach;
                }

                float rate = mud != null ? mud.LockRateAt(gun.Player.transform.position) : 1f;
                gun.Charge += Time.deltaTime / lockSeconds * rate;
                if (gun.Charge >= 1f)
                {
                    Fire(gun);
                    return;
                }
            }

            ShowLock(gun);
        }

        private void Fire(Gun gun)
        {
            UWBPlayerController shooter = gun.Player;
            ThiefController thief = gun.Target;
            Vector3 from = shooter.transform.position;
            Vector3 to = thief.transform.position;
            gun.Shots--;
            gun.Charge = 0f;
            gun.Target = null;
            ShowLock(gun);

            NpcWalker inTheWay = PasserByInTheWay(from, to);
            if (inTheWay != null)
            {
                StunBolt.Play(from, inTheWay.transform.position, boltGlowColour, transform);
                inTheWay.GetAngry(0f);
                Debug.Log($"[StunGuns] Player {shooter.PlayerId}'s bolt hits {inTheWay.DisplayName} in the way", this);
                NpcZapped?.Invoke(inTheWay, shooter);
                return;
            }

            StunBolt.Play(from, to, boltGlowColour, transform);
            if (thief.Stun(stunSeconds) && stunStar != null) StunStars.Play(thief, stunStar, stunSeconds);
            Debug.Log($"[StunGuns] Player {shooter.PlayerId} stuns {thief.name} ({gun.Shots} shots left)", this);
            Fired?.Invoke(shooter, thief);
        }

        private ThiefController NearestThief(Vector3 at)
        {
            if (thieves == null) return null;

            ThiefController nearest = null;
            float best = lockRange;
            foreach (ThiefController thief in thieves.Thieves)
            {
                if (!CanBeHit(thief)) continue;
                float distance = Flat(at, thief.transform.position);
                if (distance > best) continue;
                best = distance;
                nearest = thief;
            }

            return nearest;
        }

        private static bool CanBeHit(ThiefController thief)
        {
            return thief != null && thief.isActiveAndEnabled && thief.State != ThiefState.Caught && !thief.IsHidden && !thief.IsStunned;
        }

        // The passer-by nearest the shooter that stands within npcInTheWay of the line of fire.
        private NpcWalker PasserByInTheWay(Vector3 from, Vector3 to)
        {
            if (passersBy == null) return null;

            Vector3 line = to - from;
            line.y = 0f;
            float length = line.magnitude;
            if (length < 0.01f) return null;

            Vector3 along = line / length;
            NpcWalker nearest = null;
            float nearestAlong = float.MaxValue;
            foreach (NpcWalker walker in passersBy.Walkers)
            {
                if (walker == null || !walker.isActiveAndEnabled || walker.IsComingIn) continue;

                Vector3 offset = walker.transform.position - from;
                offset.y = 0f;
                float distanceAlong = Vector3.Dot(offset, along);
                if (distanceAlong <= 0f || distanceAlong >= length) continue;
                if ((offset - along * distanceAlong).magnitude > npcInTheWay || distanceAlong >= nearestAlong) continue;

                nearestAlong = distanceAlong;
                nearest = walker;
            }

            return nearest;
        }

        // The crackling ring round a player carrying shots: it turns slowly and flickers a little.
        private void ShowHolder(Gun gun)
        {
            if (holderRing == null) return;

            if (gun.HolderRing == null)
            {
                gun.HolderRing = FloorPicture("Stun Gun Ring", holderRing, HolderOrder);
                gun.HolderRing.transform.localScale = Vector3.one * (PlayerMarker.Width(gun.Player) * holderRingVsPlayer / Mathf.Max(0.01f, holderRing.bounds.size.x));
            }

            Vector3 at = gun.Player.transform.position;
            gun.HolderRing.transform.SetPositionAndRotation(new Vector3(at.x, FloorHeight, at.z), Quaternion.Euler(90f, Time.time * 40f, 0f));
            Color colour = boltGlowColour;
            colour.a *= 0.9f * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 6f, gun.Player.PlayerId));
            gun.HolderRing.color = colour;
        }

        private void ShowLock(Gun gun)
        {
            if (reticleBack == null || reticleFill == null) return;

            if (gun.LockRing == null)
            {
                gun.LockRing = StunLockRing.Create(transform, reticleBack, reticleFill, radialFill, reticleWidth, reticleColour, lockedColour);
            }

            if (gun.Target == null || gun.Charge <= 0f) gun.LockRing.Hide();
            else gun.LockRing.Show(BodyCentre(gun.Target), gun.Charge);
        }

        private SpriteRenderer FloorPicture(string name, Sprite sprite, int order)
        {
            var holder = new GameObject(name, typeof(SpriteRenderer));
            holder.transform.SetParent(transform, false);
            var picture = holder.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            return picture;
        }

        private static void RemoveLook(Gun gun)
        {
            if (gun.HolderRing != null) Destroy(gun.HolderRing.gameObject);
            if (gun.LockRing != null) Destroy(gun.LockRing.gameObject);
        }

        // The middle of the thief's drawn body (its picture has empty margins, so its own position is
        // off to one side of what is seen).
        private static Vector3 BodyCentre(ThiefController thief)
        {
            return thief.Body != null ? thief.Body.bounds.center : thief.transform.position;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
