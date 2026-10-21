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
    // thief (scuffle, handcuffs, the bounty's ka-ching), bumping a passer-by (scuffle, a "wrong"
    // buzz, the fine, its grumble), bombs (landing, fizzing fuse, boom, a player hit), posters pinned
    // up, the last ten seconds ticking, the time-up whistle and the switch to the next map.
    // Footsteps share a few voices (more at once are skipped) and are kept quiet; every sound leans
    // left or right with where on the floor it happens. Console lines start with [GameSfx].
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
        [SerializeField, Range(0f, 1f)] private float runVolume = 0.3f;
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
        [SerializeField, Range(0f, 1f)] private float flushVolume = 0.4f;
        [SerializeField, Min(0.01f)] private float flushFadeSeconds = 0.15f;
        [SerializeField, Range(0f, 1f)] private float hideVolume = 0.55f;

        [Header("Catching")]
        [SerializeField] private AudioClip scuffle;
        [SerializeField] private AudioClip cuffs;
        [SerializeField] private AudioClip coin;
        [SerializeField] private AudioClip wrong;
        [SerializeField] private AudioClip npcAngry;
        [SerializeField] private AudioClip fine;
        [SerializeField, Range(0f, 1f)] private float catchVolume = 0.7f;

        [Header("Bombs")]
        [SerializeField] private AudioClip bombDrop;
        [SerializeField] private AudioClip bombFuse;
        [SerializeField] private AudioClip bombBoom;
        [SerializeField] private AudioClip playerHit;
        [SerializeField, Range(0f, 1f)] private float bombVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float fuseVolume = 0.3f;

        [Header("The game's rhythm")]
        [SerializeField] private AudioClip posterPin;
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioClip tickFinal;
        [SerializeField] private AudioClip timeUp;
        [SerializeField] private AudioClip mapWhoosh;
        [SerializeField, Range(0f, 1f)] private float rhythmVolume = 0.6f;
        // The last seconds that tick with the higher sound.
        [SerializeField, Min(0)] private int finalTicks = 3;

        [Header("Speakers")]
        // Each sound comes from the room's left or right speaker as far as it is to that side of
        // the floor; sounds of things that move keep following them while they play.
        [SerializeField] private FloorStereo speakers = new FloorStereo();

        // When things happen in a scuffle, after it starts (CatchBrawl: the X lands at 0.95 s).
        private const float CuffsAt = 0.95f;
        private const float CoinAt = 1.2f;
        private const float WrongAt = 0.25f;
        private const float FineAt = 0.55f;
        private const float AngryAt = 1.0f;
        // A pinned poster lands this long after it starts coming in (PosterMotion.pinSeconds).
        private const float PinLands = 0.16f;

        private struct Pending
        {
            public float At;
            public AudioClip Clip;
            public Vector3 Where;
            public Transform Follow;
            public float Volume;
        }

        private readonly List<AudioSource> voices = new List<AudioSource>();
        // Voices playing the sound of something that moves, and what they follow.
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
            StartOn(source, clip, runner.position, runVolume, runner);
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
                Play(ForMap(stepOn), bush.Center, hideVolume, bush.transform);
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
            voice.panStereo = speakers.PanAt(bush.Center);
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
            Play(ForMap(hideIn), bush.Center, hideVolume, bush.transform);
        }

        // (Hiding spots can walk about, so their sounds follow them.)
        private void OnThiefExited(HidingBush bush, Vector3 side)
        {
            Play(ForMap(hideOut), bush.Center, hideVolume, bush.transform);
            if (bush.PlayerIsStandingOn) Play(flushed, bush.Center, hideVolume, bush.transform);
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
            Vector3 at = thief != null ? thief.transform.position : Vector3.zero;
            Play(scuffle, at, catchVolume);
            Later(CuffsAt, cuffs, at, catchVolume);
            Later(CoinAt, coin, at, catchVolume);
        }

        private void OnNpcBumped(NpcWalker npc, UWBPlayerController player)
        {
            Vector3 at = npc != null ? npc.transform.position : Vector3.zero;
            Play(scuffle, at, catchVolume * 0.7f);
            Later(WrongAt, wrong, at, catchVolume * 0.8f);
            Later(FineAt, fine, at, catchVolume);
            Later(AngryAt, npcAngry, at, catchVolume, npc != null ? npc.transform : null);
        }

        // Bombs ---------------------------------------------------------------------------------------

        private void OnBombDropped(Vector3 at, float fuseSeconds)
        {
            Play(bombDrop, at, bombVolume);
            if (bombFuse == null) return;

            // The fuse fizzes on its own voice until the bomb goes off.
            AudioSource fuse = CreateSource("Fuse");
            fuse.clip = bombFuse;
            fuse.loop = true;
            fuse.volume = fuseVolume;
            fuse.panStereo = speakers.PanAt(at);
            fuse.Play();
            Destroy(fuse.gameObject, Mathf.Max(0.05f, fuseSeconds));
        }

        private void OnBombExploded(Vector3 at)
        {
            Play(bombBoom, at, bombVolume);
        }

        private void OnPlayerBombed(UWBPlayerController player, ThiefController thief)
        {
            Play(playerHit, player != null ? player.transform.position : Vector3.zero, bombVolume);
        }

        // The game's rhythm ---------------------------------------------------------------------------

        private void OnPostersPutUp(int count, float delay, float stagger)
        {
            for (int i = 0; i < count; i++)
            {
                Later(delay + i * stagger + PinLands, posterPin, Vector3.zero, rhythmVolume);
            }
        }

        private void OnWarningSecond(int secondsLeft)
        {
            Play(secondsLeft <= finalTicks ? tickFinal : tick, Vector3.zero, rhythmVolume);
        }

        private void OnTimeUp()
        {
            Play(timeUp, Vector3.zero, rhythmVolume);
        }

        private void OnMapSwitched(int map)
        {
            Play(mapWhoosh, Vector3.zero, rhythmVolume);
        }

        // Playing -------------------------------------------------------------------------------------

        // follow: something that moves, which the sound keeps following; null for a sound that stays put.
        private void Later(float seconds, AudioClip clip, Vector3 at, float volume, Transform follow = null)
        {
            if (clip != null) pending.Add(new Pending { At = Time.time + seconds, Clip = clip, Where = at, Volume = volume, Follow = follow });
        }

        private void PlayPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].At) continue;
                Play(pending[i].Clip, pending[i].Where, pending[i].Volume, pending[i].Follow);
                pending.RemoveAt(i);
            }
        }

        // Plays on the next free voice (or the one that started longest ago when all are busy).
        private void Play(AudioClip clip, Vector3 at, float volume, Transform follow = null)
        {
            if (clip == null) return;

            AudioSource source = voices.Find(candidate => !candidate.isPlaying);
            if (source == null)
            {
                source = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Count;
                source.Stop();
            }

            StartOn(source, clip, at, volume, follow);
        }

        private void StartOn(AudioSource source, AudioClip clip, Vector3 at, float volume, Transform follow)
        {
            Vector3 where = follow != null ? follow.position : at;
            source.panStereo = speakers.PanAt(where);
            if (follow != null) following[source] = follow;
            else following.Remove(source);
            source.PlayOneShot(clip, volume);
        }

        // Keeps the sounds of moving things on their side of the room as they move.
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
