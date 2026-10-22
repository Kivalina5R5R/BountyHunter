using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The gameplay sound effects (Tools/GameplaySounds/make_gameplay_sounds.py makes them). It
    // listens to the game rather than the game calling it: a thief's footfalls while it runs (the
    // same soft paws for every thief; Footsteps, added to each thief as it appears; passers-by and
    // strolling make none), thieves diving into and out of hiding spots (each map's own sound, plus
    // a boing when a player standing on the spot flushes one out), catching a
    // thief (a bright scuffle, handcuffs, the bounty's ka-ching), grabbing a passer-by by mistake (a
    // dull bonk and tumble, then a buzzer as the X slams down), bombs (landing, fizzing fuse, boom, a player hit), posters pinned
    // up, the last ten seconds ticking, the time-up whistle and the switch to the next map.
    // Footsteps share a few voices (more at once are skipped) and are kept quiet; they alone lean
    // left or right with where the thief runs, every other sound comes from both speakers alike.
    // Volumes are set in tiers so the plays stand out: the big moments (the bounty chime, a thief
    // flushed out, the boom, time up) loudest, then
    // feedback (scuffle, the penalty, the final ticks, a bomb landing), then information (hiding,
    // stepping on, ticks, posters), with footfalls, the fuse and the music underneath. Console
    // lines start with [GameSfx].
    public sealed class GameSfx : MonoBehaviour
    {

        [Header("Listens to")]
        [SerializeField] private ThiefSpawner thieves;
        [SerializeField] private NpcCrowd passersBy;
        [SerializeField] private BombSquad bombs;
        [SerializeField] private StageTimer timer;
        [SerializeField] private MapSwitcher maps;
        [SerializeField] private WantedBoard board;

        [Header("Running thieves")]
        // Every thief's footfalls (one picked at random each step).
        [SerializeField] private AudioClip[] thiefSteps;
        [SerializeField, Range(0f, 1f)] private float runVolume = 0.25f;
        // Footfalls sounding at once; more are skipped.
        [SerializeField, Range(1, 8)] private int stepVoices = 3;

        [Header("Hiding spots (one per map, in map order)")]
        [SerializeField] private AudioClip[] hideIn;
        [SerializeField] private AudioClip[] hideOut;
        [SerializeField] private AudioClip flushed;
        // A player stepping onto a hiding spot (one per map, in map order).
        [SerializeField] private AudioClip[] stepOn;
        // A spot stepped on again sooner than this (a player on its edge) makes no new sound.
        [SerializeField, Min(0f)] private float stepOnGapSeconds = 0.5f;
        // While a player stands on a spot with a thief in it: a gentle build-up, played in step with
        // the flush (as long as it takes, 2 s), fading out when they step off.
        [SerializeField] private AudioClip flushBuild;
        [SerializeField, Range(0f, 1f)] private float flushVolume = 0.5f;
        [SerializeField, Min(0.01f)] private float flushFadeSeconds = 0.15f;
        // Thieves going in and out and players stepping on.
        [SerializeField, Range(0f, 1f)] private float hideVolume = 0.5f;
        // The boing of a thief flushed out (a big moment).
        [SerializeField, Range(0f, 1f)] private float flushedVolume = 0.6f;

        [Header("Catching")]
        [SerializeField] private AudioClip scuffle;
        [SerializeField] private AudioClip cuffs;
        [SerializeField] private AudioClip coin;
        // The scuffle and handcuffs of a catch.
        [SerializeField, Range(0f, 1f)] private float catchVolume = 0.6f;
        // The bounty's chime, the loudest part of a catch.
        [SerializeField, Range(0f, 1f)] private float coinVolume = 0.8f;

        [Header("Bumping a passer-by (kept below a catch)")]
        // The bonk and tumble (its own sound, so a mistake is told from a catch at once).
        [SerializeField] private AudioClip bump;
        [SerializeField] private AudioClip wrong;
        [SerializeField, Range(0f, 1f)] private float bumpVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float wrongVolume = 0.5f;

        [Header("Bombs")]
        [SerializeField] private AudioClip bombDrop;
        [SerializeField] private AudioClip bombFuse;
        [SerializeField] private AudioClip bombBoom;
        [SerializeField] private AudioClip playerHit;
        // The boom.
        [SerializeField, Range(0f, 1f)] private float bombVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float dropVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float fuseVolume = 0.35f;

        [Header("The game's rhythm")]
        [SerializeField] private AudioClip posterPin;
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioClip tickFinal;
        [SerializeField] private AudioClip timeUp;
        [SerializeField] private AudioClip mapWhoosh;
        // Posters, ticks and the map whoosh.
        [SerializeField, Range(0f, 1f)] private float rhythmVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float finalTickVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float timeUpVolume = 0.6f;
        // The last seconds that tick with the higher sound.
        [SerializeField, Min(0)] private int finalTicks = 3;

        [Header("Speakers")]
        // A running thief's footfalls come from the room's left or right speaker as far as it is to
        // that side of the floor, following it while they sound. Nothing else is panned.
        [SerializeField] private FloorStereo speakers = new FloorStereo();

        // When things happen in a scuffle, after it starts. The cuffs close and the buzzer sounds as
        // the cloud bursts (with a passer-by, as the X slams down); the bounty follows a moment later.
        private static float CuffsAt => CatchBrawl.StampSeconds;
        private static float CoinAt => CatchBrawl.StampSeconds + 0.2f;
        private static float WrongAt => CatchBrawl.StampSeconds;
        // A pinned poster lands this long after it starts coming in (PosterMotion.pinSeconds).
        private const float PinLands = 0.16f;

        private struct Pending
        {
            public float At;
            public AudioClip Clip;
            public float Volume;
        }

        private readonly List<AudioSource> voices = new List<AudioSource>();
        // Footfall voices sounding now, and the thief each follows.
        private readonly Dictionary<AudioSource, Transform> following = new Dictionary<AudioSource, Transform>();
        private readonly List<AudioSource> doneFollowing = new List<AudioSource>();
        private readonly List<AudioSource> stepSources = new List<AudioSource>();
        private readonly List<Pending> pending = new List<Pending>();
        private readonly HashSet<HidingBush> heardBushes = new HashSet<HidingBush>();
        // Spots a player stands on now, and when each last made its step-on sound.
        private readonly HashSet<HidingBush> stoodOn = new HashSet<HidingBush>();
        private readonly Dictionary<HidingBush, float> lastStepOn = new Dictionary<HidingBush, float>();
        // Each spot's own voice for its flush build-up.
        private readonly Dictionary<HidingBush, AudioSource> flushVoices = new Dictionary<HidingBush, AudioSource>();
        private readonly List<HidingBush> goneSpots = new List<HidingBush>();
        private readonly HashSet<Transform> withSteps = new HashSet<Transform>();
        private int nextVoice;

        private void Awake()
        {
            for (int i = 0; i < 12; i++) voices.Add(CreateSource("Sound"));
            for (int i = 0; i < stepVoices; i++) stepSources.Add(CreateSource("Step"));
        }

        private void OnEnable()
        {
            if (thieves != null) thieves.ThiefCaught += OnThiefCaught;
            if (passersBy != null) passersBy.NpcBumped += OnNpcBumped;
            if (bombs != null)
            {
                bombs.BombDropped += OnBombDropped;
                bombs.BombExploded += OnBombExploded;
                bombs.PlayerBombed += OnPlayerBombed;
            }

            if (timer != null)
            {
                timer.WarningSecond += OnWarningSecond;
                timer.TimeUp += OnTimeUp;
            }

            if (maps != null) maps.MapSwitched += OnMapSwitched;
            if (board != null) board.PostersPutUp += OnPostersPutUp;
        }

        private void OnDisable()
        {
            if (thieves != null) thieves.ThiefCaught -= OnThiefCaught;
            if (passersBy != null) passersBy.NpcBumped -= OnNpcBumped;
            if (bombs != null)
            {
                bombs.BombDropped -= OnBombDropped;
                bombs.BombExploded -= OnBombExploded;
                bombs.PlayerBombed -= OnPlayerBombed;
            }

            if (timer != null)
            {
                timer.WarningSecond -= OnWarningSecond;
                timer.TimeUp -= OnTimeUp;
            }

            if (maps != null) maps.MapSwitched -= OnMapSwitched;
            if (board != null) board.PostersPutUp -= OnPostersPutUp;
            foreach (HidingBush bush in heardBushes)
            {
                if (bush == null) continue;
                bush.ThiefEntered -= OnThiefEntered;
                bush.ThiefExited -= OnThiefExited;
            }

            heardBushes.Clear();
        }

        private void Update()
        {
            ListenToNewcomers();
            WatchStepOns();
            PlayPending();
            FollowMovers();
        }

        // Running thieves ---------------------------------------------------------------------------

        // One footfall of runner, a running thief, following it across the speakers while it sounds.
        public void PlayRunStep(Transform runner)
        {
            AudioClip clip = RandomOf(thiefSteps);
            AudioSource source = stepSources.Find(candidate => !candidate.isPlaying);
            if (clip == null || source == null || runner == null) return;

            source.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
            source.panStereo = speakers.PanAt(runner.position);
            following[source] = runner;
            source.PlayOneShot(clip, runVolume);
        }

        // Thieves and hiding spots appear during the game; their sounds are hooked up here.
        private void ListenToNewcomers()
        {
            if (thieves == null) return;

            foreach (ThiefController thief in thieves.Thieves) AddSteps(thief);
            foreach (HidingBush bush in thieves.Bushes)
            {
                if (bush == null || !heardBushes.Add(bush)) continue;
                bush.ThiefEntered += OnThiefEntered;
                bush.ThiefExited += OnThiefExited;
            }

            heardBushes.RemoveWhere(bush => bush == null);
            withSteps.RemoveWhere(walker => walker == null);
        }

        private void AddSteps(ThiefController thief)
        {
            if (thief == null || !withSteps.Add(thief.transform)) return;

            Footsteps steps = thief.GetComponent<Footsteps>();
            if (steps == null) steps = thief.gameObject.AddComponent<Footsteps>();
            steps.Initialize(this, thief.Body);
        }

        // Hiding spots ------------------------------------------------------------------------------

        // The moment a player steps onto a hiding spot, the spot gives a little (its map's sound);
        // staying on one with a thief inside builds up until it is flushed out.
        private void WatchStepOns()
        {
            DropGoneFlushVoices();
            foreach (HidingBush bush in heardBushes)
            {
                if (bush == null) continue;
                UpdateFlush(bush);
                if (!bush.PlayerIsStandingOn)
                {
                    stoodOn.Remove(bush);
                    continue;
                }

                if (!stoodOn.Add(bush)) continue;
                if (lastStepOn.TryGetValue(bush, out float last) && Time.time - last < stepOnGapSeconds) continue;

                lastStepOn[bush] = Time.time;
                Play(ForMap(stepOn), hideVolume);
            }

            stoodOn.RemoveWhere(bush => bush == null);
            if (lastStepOn.Count > heardBushes.Count * 2) lastStepOn.Clear();
        }

        // The build-up follows the flush: at the point it has reached while a player stands on a spot
        // with a thief in it (picking up there after stepping off and back on), else fading away.
        private void UpdateFlush(HidingBush bush)
        {
            bool building = flushBuild != null && bush.Occupant != null && bush.PlayerIsStandingOn;
            flushVoices.TryGetValue(bush, out AudioSource voice);
            if (!building)
            {
                if (voice == null || !voice.isPlaying) return;
                voice.volume = Mathf.MoveTowards(voice.volume, 0f, flushVolume / flushFadeSeconds * Time.deltaTime);
                if (voice.volume <= 0f) voice.Stop();
                return;
            }

            if (voice == null)
            {
                voice = CreateSource("Flush");
                voice.clip = flushBuild;
                flushVoices[bush] = voice;
            }

            float at = Mathf.Min(bush.FlushProgress * flushBuild.length, flushBuild.length - 0.01f);
            voice.volume = flushVolume;
            if (!voice.isPlaying)
            {
                voice.time = at;
                voice.Play();
            }
            else if (Mathf.Abs(voice.time - at) > 0.2f)
            {
                voice.time = at;
            }
        }

        // Spots of an old layout are gone; so are their voices.
        private void DropGoneFlushVoices()
        {
            goneSpots.Clear();
            foreach (KeyValuePair<HidingBush, AudioSource> pair in flushVoices)
            {
                if (pair.Key == null) goneSpots.Add(pair.Key);
            }

            foreach (HidingBush gone in goneSpots)
            {
                if (flushVoices[gone] != null) Destroy(flushVoices[gone].gameObject);
                flushVoices.Remove(gone);
            }
        }

        private void OnThiefEntered(HidingBush bush, Vector3 side)
        {
            Play(ForMap(hideIn), hideVolume);
        }

        private void OnThiefExited(HidingBush bush, Vector3 side)
        {
            Play(ForMap(hideOut), hideVolume);
            if (bush.PlayerIsStandingOn) Play(flushed, flushedVolume);
        }

        private AudioClip ForMap(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            int map = maps != null ? maps.CurrentMap : 0;
            return clips[Mathf.Clamp(map, 0, clips.Length - 1)];
        }

        // Catching ----------------------------------------------------------------------------------

        private void OnThiefCaught(ThiefController thief, UWBPlayerController catcher)
        {
            Play(scuffle, catchVolume);
            Later(CuffsAt, cuffs, catchVolume);
            Later(CoinAt, coin, coinVolume);
        }

        private void OnNpcBumped(NpcWalker npc, UWBPlayerController player)
        {
            Play(bump, bumpVolume);
            Later(WrongAt, wrong, wrongVolume);
        }

        // Bombs ---------------------------------------------------------------------------------------

        private void OnBombDropped(Vector3 at, float fuseSeconds)
        {
            Play(bombDrop, dropVolume);
            if (bombFuse == null) return;

            // The fuse fizzes on its own voice until the bomb goes off.
            AudioSource fuse = CreateSource("Fuse");
            fuse.clip = bombFuse;
            fuse.loop = true;
            fuse.volume = fuseVolume;
            fuse.Play();
            Destroy(fuse.gameObject, Mathf.Max(0.05f, fuseSeconds));
        }

        private void OnBombExploded(Vector3 at)
        {
            Play(bombBoom, bombVolume);
        }

        private void OnPlayerBombed(UWBPlayerController player, ThiefController thief)
        {
            Play(playerHit, hitVolume);
        }

        // The game's rhythm ---------------------------------------------------------------------------

        private void OnPostersPutUp(int count, float delay, float stagger)
        {
            for (int i = 0; i < count; i++)
            {
                Later(delay + i * stagger + PinLands, posterPin, rhythmVolume);
            }
        }

        private void OnWarningSecond(int secondsLeft)
        {
            bool final = secondsLeft <= finalTicks;
            Play(final ? tickFinal : tick, final ? finalTickVolume : rhythmVolume);
        }

        private void OnTimeUp()
        {
            Play(timeUp, timeUpVolume);
        }

        private void OnMapSwitched(int map)
        {
            Play(mapWhoosh, rhythmVolume);
        }

        // Playing -------------------------------------------------------------------------------------

        private void Later(float seconds, AudioClip clip, float volume)
        {
            if (clip != null) pending.Add(new Pending { At = Time.time + seconds, Clip = clip, Volume = volume });
        }

        private void PlayPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].At) continue;
                Play(pending[i].Clip, pending[i].Volume);
                pending.RemoveAt(i);
            }
        }

        // Plays on the next free voice (or the one that started longest ago when all are busy), from
        // both speakers alike.
        private void Play(AudioClip clip, float volume)
        {
            if (clip == null) return;

            AudioSource source = voices.Find(candidate => !candidate.isPlaying);
            if (source == null)
            {
                source = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Count;
                source.Stop();
            }

            source.PlayOneShot(clip, volume);
        }

        // Keeps each footfall on the side of the room its thief is running on.
        private void FollowMovers()
        {
            doneFollowing.Clear();
            foreach (KeyValuePair<AudioSource, Transform> pair in following)
            {
                if (!pair.Key.isPlaying || pair.Value == null) doneFollowing.Add(pair.Key);
                else pair.Key.panStereo = speakers.PanAt(pair.Value.position);
            }

            foreach (AudioSource source in doneFollowing) following.Remove(source);
        }

        private AudioSource CreateSource(string label)
        {
            var holder = new GameObject(label);
            holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip RandomOf(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0 ? clips[UnityEngine.Random.Range(0, clips.Length)] : null;
        }
    }
}
