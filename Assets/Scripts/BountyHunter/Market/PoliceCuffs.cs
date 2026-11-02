using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The market's handcuffs (stage 3): a player who reaches a thief cuffs it rather than catching it
    // outright (handcuffs slam down over its head in a burst, CuffBurst, so the player sees it took),
    // and it trails behind them until they lead it to one of the police cars
    // parked mid-way along the floor's left and right ends. Only a delivered thief counts
    // (Delivered: BountyRounds pays its bounty and stamps its poster); the car bounces to take it. A player leads at most
    // handsPerPlayer thieves at once; with their hands full a thief slips past them. A cuffed thief
    // breaks free and runs after escortSeconds, and every thief a player leads breaks free when a
    // bomb gets them. Another player with a hand free who walks across the line between a thief and
    // whoever leads it (within cutReach of it) takes that thief over, once per thief, and its time starts again. Should the stage's clock
    // run out while thieves are still being led, their leaders are paid half (Stranded). In the last
    // warnSeconds before a thief breaks free its handcuffs flash red and shake. Gone when
    // the floor is cleared or another stage starts.
    public sealed class PoliceCuffs : MonoBehaviour, IThiefCuffs
    {
        // One cuffed thief and who is leading it.
        private sealed class Cuffed
        {
            public ThiefController Thief;
            public UWBPlayerController Leader;
            public float Since;
            public bool TakenOver;
            public SpriteRenderer Handcuffs;
        }

        private const float Height = 0.03f;
        // The cars and their rings on the floor (with the floor switches, 6); the handcuffs over the
        // thieves (8), under the players' markers (20).
        private const int RingOrder = 5;
        private const int CarOrder = 6;
        private const int HandcuffsOrder = 9;
        // The handcuffs slam down this much bigger and settle over this long as a thief is cuffed.
        private const float SlamSize = 2.4f;
        private const float SlamSeconds = 0.3f;
        // A car taking thieves bounces: how far it squashes and stretches, and for how long.
        private const float BounceAmount = 0.22f;
        private const float BounceSeconds = 0.7f;

        [SerializeField] private BountyRounds rounds;
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private UWBPlayerSpawner players;
        // A bomb gets a player: every thief they lead breaks free; may be empty.
        [SerializeField] private BombSquad bombs;
        // The stage's clock running out pays half for the thieves still being led; may be empty.
        [SerializeField] private StageTimer timer;
        [SerializeField] private int stage = 3;

        [Header("Leading")]
        [SerializeField, Min(1)] private int handsPerPlayer = 2;
        // How far behind its leader (the player, or the thief in front) a cuffed thief trails.
        [SerializeField, Min(0.1f)] private float leash = 0.6f;
        [SerializeField, Min(1f)] private float escortSeconds = 12f;
        [SerializeField, Min(0.05f)] private float cutReach = 0.28f;

        [Header("Police cars")]
        // Where the cars stand (x and world z) and how close a leader comes to hand their thieves over.
        [SerializeField] private Vector2[] carSpots = { new Vector2(-4.6f, 0f), new Vector2(4.6f, 0f) };
        [SerializeField, Min(0.1f)] private float carRadius = 0.85f;

        [Header("Look")]
        // The police car seen from above, nose up, this long along the floor's depth.
        [SerializeField] private Sprite policeCar;
        [SerializeField, Min(0.1f)] private float carLength = 1.6f;
        // The ring round a car showing how close to come; it glows while anyone leads a thief.
        [SerializeField] private Sprite carRing;
        [SerializeField] private Color carRingColour = new Color(0.35f, 0.65f, 1f, 1f);
        // The siren light on the car's roof, flashing red and blue while anyone leads a thief.
        [SerializeField] private Color sirenRed = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color sirenBlue = new Color(0.2f, 0.45f, 1f, 1f);
        // The handcuffs over a cuffed thief's head, this wide on the floor.
        [SerializeField] private Sprite handcuffs;
        [SerializeField, Min(0.05f)] private float handcuffsWidth = 0.32f;
        // The burst round a thief as it is cuffed (CuffBurst), this wide.
        [SerializeField, Min(0.1f)] private float burstWidth = 1.4f;
        [SerializeField] private Color burstColour = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float warnSeconds = 3f;
        [SerializeField] private Color warnColour = new Color(1f, 0.3f, 0.25f, 1f);

        private readonly List<Cuffed> led = new List<Cuffed>();
        private readonly List<GameObject> cars = new List<GameObject>();
        // When each car last took thieves (it bounces), the same order as cars.
        private readonly List<float> carBouncedAt = new List<float>();
        private readonly List<SpriteRenderer> carRings = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> sirens = new List<SpriteRenderer>();
        private readonly List<ThiefController> handedOver = new List<ThiefController>();
        private bool inUse;

        // A player cuffed a thief.
        public event Action<ThiefController, UWBPlayerController> ThiefCuffed;
        // A player handed thieves over at a police car (which ones).
        public event Action<UWBPlayerController, IReadOnlyList<ThiefController>> Delivered;
        // The stage's clock ran out while a player was still leading these thieves.
        public event Action<UWBPlayerController, IReadOnlyList<ThiefController>> Stranded;

        private void OnEnable()
        {
            if (rounds != null)
            {
                rounds.StageStarted += OnStageStarted;
                rounds.FloorCleared += Clear;
            }

            if (thieves != null) thieves.ThiefSpawned += OnThiefSpawned;
            if (bombs != null) bombs.PlayerBombed += OnPlayerBombed;
            if (timer != null) timer.TimeUp += OnTimeUp;
        }

        private void OnDisable()
        {
            if (rounds != null)
            {
                rounds.StageStarted -= OnStageStarted;
                rounds.FloorCleared -= Clear;
            }

            if (thieves != null) thieves.ThiefSpawned -= OnThiefSpawned;
            if (bombs != null) bombs.PlayerBombed -= OnPlayerBombed;
            if (timer != null) timer.TimeUp -= OnTimeUp;
            Clear();
        }

        // The round's first thieves are already out when the stage starts, so they get the cuffs too.
        private void OnStageStarted(int started)
        {
            Clear();
            if (started != stage) return;

            inUse = true;
            PutCarsDown();
            if (thieves == null) return;

            foreach (ThiefController thief in thieves.Thieves)
            {
                if (thief != null) thief.UseCuffs(this);
            }
        }

        private void OnThiefSpawned(ThiefController thief)
        {
            if (inUse) thief.UseCuffs(this);
        }

        public bool TryCuff(ThiefController thief, float reach)
        {
            if (!inUse)
            {
                thief.UseCuffs(null);
                return false;
            }

            if (!TryGetFreeHand(thief.transform.position, reach, out UWBPlayerController leader)) return false;

            thief.Cuff();
            led.Add(new Cuffed
            {
                Thief = thief,
                Leader = leader,
                Since = Time.time,
                Handcuffs = Part(transform, "Handcuffs", handcuffs, HandcuffsOrder),
            });
            CuffBurst.Play(thief.transform.position, burstWidth, burstColour, transform);
            ThiefCuffed?.Invoke(thief, leader);
            Debug.Log($"[PoliceCuffs] Player {leader.PlayerId} cuffs {thief.name} ({Count(leader)}/{handsPerPlayer})", this);
            return true;
        }

        private void Update()
        {
            if (!inUse) return;

            for (int i = led.Count - 1; i >= 0; i--)
            {
                Cuffed cuffed = led[i];
                if (cuffed.Thief == null) Drop(i);
                else if (!Playing(cuffed.Leader)) Free(i, cuffed.Thief.transform.position);
                else if (Time.time - cuffed.Since >= escortSeconds) Free(i, cuffed.Leader.transform.position);
            }

            Lead();
            TakeOverCutLines();
            HandOverAtCars();
            ShowCars();
        }

        // Each thief trails behind the one in front of it, the first behind its player.
        private void Lead()
        {
            foreach (Cuffed cuffed in led)
            {
                cuffed.Thief.TrailBehind(InFrontOf(cuffed), leash);
                ShowHandcuffs(cuffed);
            }
        }

        // A player with a hand free who walks between a thief and whoever leads it takes that thief over.
        private void TakeOverCutLines()
        {
            if (players == null) return;

            for (int i = 0; i < led.Count; i++)
            {
                Cuffed cuffed = led[i];
                if (cuffed.TakenOver) continue;

                Vector3 from = InFrontOf(cuffed);
                Vector3 to = cuffed.Thief.transform.position;
                foreach (UWBPlayerController player in players.SpawnedPlayers)
                {
                    if (!Playing(player) || player == cuffed.Leader || Count(player) >= handsPerPlayer) continue;
                    // Bumping into whoever leads it is not cutting in.
                    Vector3 at = player.transform.position;
                    if (Flat(at, from) < cutReach || DistanceToSegment(at, from, to) > cutReach) continue;

                    Debug.Log($"[PoliceCuffs] Player {player.PlayerId} cuts in and takes {cuffed.Thief.name} from player {cuffed.Leader.PlayerId}", this);
                    cuffed.Leader = player;
                    cuffed.Since = Time.time;
                    cuffed.TakenOver = true;
                    CuffBurst.Play(cuffed.Thief.transform.position, burstWidth, burstColour, transform);
                    ThiefCuffed?.Invoke(cuffed.Thief, player);
                    // Last in the new leader's line.
                    led.RemoveAt(i);
                    led.Add(cuffed);
                    i--;
                    break;
                }
            }
        }

        // A leader near a police car hands over every thief they lead.
        private void HandOverAtCars()
        {
            for (int i = 0; i < led.Count; i++)
            {
                UWBPlayerController leader = led[i].Leader;
                if (!TryGetCar(leader.transform.position, out int car)) continue;

                handedOver.Clear();
                for (int j = led.Count - 1; j >= 0; j--)
                {
                    if (led[j].Leader != leader) continue;
                    handedOver.Insert(0, led[j].Thief);
                    Drop(j);
                }

                Debug.Log($"[PoliceCuffs] Player {leader.PlayerId} hands {handedOver.Count} thief(s) over at the police car", this);
                Delivered?.Invoke(leader, handedOver);
                foreach (ThiefController thief in handedOver) thief.Deliver();
                Bounce(car);
                i = -1;
            }
        }

        private void OnPlayerBombed(UWBPlayerController player, ThiefController bomber)
        {
            for (int i = led.Count - 1; i >= 0; i--)
            {
                if (led[i].Leader == player) Free(i, player.transform.position);
            }
        }

        // The clock ran out: each leader is paid half for the thieves still being led, and the
        // cuffs stay as they are until the floor is cleared.
        private void OnTimeUp()
        {
            if (!inUse) return;

            inUse = false;
            var leaders = new List<UWBPlayerController>();
            foreach (Cuffed cuffed in led)
            {
                if (cuffed.Thief != null && Playing(cuffed.Leader) && !leaders.Contains(cuffed.Leader)) leaders.Add(cuffed.Leader);
            }

            foreach (UWBPlayerController leader in leaders)
            {
                var stranded = new List<ThiefController>();
                foreach (Cuffed cuffed in led)
                {
                    if (cuffed.Leader == leader && cuffed.Thief != null) stranded.Add(cuffed.Thief);
                }

                Debug.Log($"[PoliceCuffs] Time is up with player {leader.PlayerId} still leading {stranded.Count} thief(s)", this);
                Stranded?.Invoke(leader, stranded);
            }
        }

        private void Clear()
        {
            inUse = false;
            for (int i = led.Count - 1; i >= 0; i--) Drop(i);
            foreach (GameObject car in cars)
            {
                if (car != null) Destroy(car);
            }

            cars.Clear();
            carBouncedAt.Clear();
            carRings.Clear();
            sirens.Clear();
        }

        // Lets the thief go, running from threat.
        private void Free(int index, Vector3 threat)
        {
            ThiefController thief = led[index].Thief;
            Drop(index);
            if (thief != null) thief.BreakFree(threat);
        }

        // Takes the handcuffs away and forgets the thief.
        private void Drop(int index)
        {
            Cuffed cuffed = led[index];
            if (cuffed.Handcuffs != null) Destroy(cuffed.Handcuffs.gameObject);
            led.RemoveAt(index);
        }

        // The player nearest the point within reach who has a hand free.
        private bool TryGetFreeHand(Vector3 point, float reach, out UWBPlayerController nearest)
        {
            nearest = null;
            if (players == null) return false;

            float best = reach;
            foreach (UWBPlayerController player in players.SpawnedPlayers)
            {
                if (!Playing(player) || Count(player) >= handsPerPlayer) continue;

                float distance = Flat(player.transform.position, point);
                if (distance > best) continue;
                best = distance;
                nearest = player;
            }

            return nearest != null;
        }

        private int Count(UWBPlayerController leader)
        {
            int count = 0;
            foreach (Cuffed cuffed in led)
            {
                if (cuffed.Leader == leader) count++;
            }

            return count;
        }

        // What the thief trails behind: the thief before it in its leader's line, or the leader.
        private Vector3 InFrontOf(Cuffed cuffed)
        {
            Vector3 front = cuffed.Leader.transform.position;
            foreach (Cuffed other in led)
            {
                if (other == cuffed) break;
                if (other.Leader == cuffed.Leader && other.Thief != null) front = other.Thief.transform.position;
            }

            return front;
        }

        // The car whose hand-over ring the point is in.
        private bool TryGetCar(Vector3 point, out int car)
        {
            for (car = 0; car < carSpots.Length; car++)
            {
                Vector2 spot = carSpots[car];
                if (Flat(point, new Vector3(spot.x, 0f, spot.y)) <= carRadius) return true;
            }

            car = -1;
            return false;
        }

        // The car answers a hand-over: it bounces, its siren flashes and a burst goes off round it.
        private void Bounce(int car)
        {
            if (car < 0 || car >= cars.Count) return;

            carBouncedAt[car] = Time.time;
            CuffBurst.Play(cars[car].transform.position, carLength * 1.2f, carRingColour, transform);
        }

        private static bool Playing(UWBPlayerController player)
        {
            return player != null && player.isActiveAndEnabled;
        }

        // The handcuffs bob over the thief's head: they slam down big as it is cuffed (or taken over)
        // and settle, and flash red and shake as its time runs out.
        private void ShowHandcuffs(Cuffed cuffed)
        {
            if (cuffed.Handcuffs == null || cuffed.Handcuffs.sprite == null) return;

            float held = Time.time - cuffed.Since;
            float left = escortSeconds - held;
            float warning = warnSeconds > 0f ? Mathf.Clamp01(1f - left / warnSeconds) : 0f;
            bool flash = warning > 0f && Mathf.Repeat(Time.time * (3f + 5f * warning), 1f) < 0.5f;
            float slam = held < SlamSeconds ? Mathf.LerpUnclamped(SlamSize, 1f, EaseOutBack(held / SlamSeconds)) : 1f;

            Vector3 at = cuffed.Thief.transform.position;
            SpriteRenderer body = cuffed.Thief.Body;
            float top = body != null ? body.bounds.max.z : at.z + 0.4f;
            float bob = 0.03f * Mathf.Sin(Time.time * 4f);
            float shake = warning > 0f ? 18f * warning * Mathf.Sin(Time.time * 40f) : 0f;
            Transform picture = cuffed.Handcuffs.transform;
            picture.SetPositionAndRotation(new Vector3(at.x, Height + 0.02f, top + bob), Quaternion.Euler(90f, 0f, shake));
            picture.localScale = Vector3.one * (slam * handcuffsWidth / Mathf.Max(0.0001f, cuffed.Handcuffs.sprite.bounds.size.x));
            cuffed.Handcuffs.color = flash ? warnColour : Color.white;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        // A car on each spot, its ring round it, and its siren light on its roof; the right-hand car
        // faces the other way, so both look parked along the floor's ends.
        private void PutCarsDown()
        {
            for (int i = 0; i < carSpots.Length; i++)
            {
                Vector2 spot = carSpots[i];
                var car = new GameObject("Police Car");
                car.transform.SetParent(transform, false);
                car.transform.SetPositionAndRotation(new Vector3(spot.x, Height, spot.y), Quaternion.Euler(90f, 0f, spot.x > 0f ? 180f : 0f));
                cars.Add(car);
                carBouncedAt.Add(float.MinValue);

                SpriteRenderer ring = Part(car.transform, "Ring", carRing != null ? carRing : PosterSprites.Ring, RingOrder);
                ring.transform.localScale = Vector3.one * (carRadius * 2f / Mathf.Max(0.0001f, ring.sprite.bounds.size.x));
                carRings.Add(ring);

                if (policeCar != null)
                {
                    SpriteRenderer body = Part(car.transform, "Car", policeCar, CarOrder);
                    body.transform.localScale = Vector3.one * (carLength / Mathf.Max(0.0001f, policeCar.bounds.size.y));
                }

                // Hiding spots creeping away with a thief inside steer round the car.
                KeepBushesOff(car.transform.position);

                SpriteRenderer siren = Part(car.transform, "Siren", PosterSprites.Dot, CarOrder + 1);
                siren.transform.localScale = Vector3.one * (carLength * 0.55f / Mathf.Max(0.0001f, PosterSprites.Dot.bounds.size.x));
                sirens.Add(siren);
            }
        }

        private void KeepBushesOff(Vector3 at)
        {
            if (thieves == null) return;

            float reach = Mathf.Max(carRadius, carLength * 0.6f);
            foreach (HidingBush bush in thieves.Bushes)
            {
                if (bush != null && bush.TryGetComponent(out BushWalker walker)) walker.KeepClearOf(at, reach);
            }
        }

        // The rings and sirens come alive while anyone leads a thief, so the players know where to go;
        // a car that has just taken thieves squashes and stretches and flashes its siren.
        private void ShowCars()
        {
            for (int i = 0; i < cars.Count; i++)
            {
                float t = (Time.time - carBouncedAt[i]) / BounceSeconds;
                float wobble = t < 1f ? BounceAmount * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * (1f - t) : 0f;
                cars[i].transform.localScale = new Vector3(1f + wobble, 1f - wobble, 1f);
            }

            bool leading = led.Count > 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f);
            Color ringColour = carRingColour;
            ringColour.a = leading ? 0.55f + 0.45f * pulse : 0.3f;
            foreach (SpriteRenderer ring in carRings) ring.color = ringColour;

            bool red = Mathf.Repeat(Time.time * 2.5f, 1f) < 0.5f;
            Color sirenColour = red ? sirenRed : sirenBlue;
            for (int i = 0; i < sirens.Count; i++)
            {
                bool bouncing = Time.time - carBouncedAt[i] < BounceSeconds * 2f;
                sirenColour.a = leading || bouncing ? 0.85f : 0f;
                sirens[i].color = sirenColour;
            }
        }

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var part = new GameObject(name, typeof(SpriteRenderer));
            part.transform.SetParent(parent, false);
            var picture = part.GetComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            return picture;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var p = new Vector2(point.x, point.z);
            var start = new Vector2(a.x, a.z);
            var end = new Vector2(b.x, b.z);
            Vector2 line = end - start;
            float t = line.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - start, line) / line.sqrMagnitude) : 0f;
            return Vector2.Distance(p, start + line * t);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.65f, 1f, 1f);
            foreach (Vector2 spot in carSpots) Gizmos.DrawWireSphere(new Vector3(spot.x, Height, spot.y), carRadius);
        }
#endif
    }
}
