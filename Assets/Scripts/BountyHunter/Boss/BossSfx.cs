using System.Collections;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The boss chase's sounds (Tools/BossSounds/make_boss_sounds.py, Assets/Audio/Boss): the feathers
    // blowing in, the boss puffing up out of its smoke, its smoke bomb (lit, then thrown), its water gun (pumped, fired,
    // the warning circles swirling, the water striking and a player soaked), its cape (wrapped,
    // passing for a passer-by, thrown off), a touch that counts with the bounty's chime, the catch
    // (the gameplay's scuffle and handcuffs under a stinger of its own) or its getaway, and the
    // police helicopter's rotor far off each time it flies past on the wall. Its footfalls and the
    // hiding spots are GameSfx's. Like GameSfx it listens to the game rather than the game calling it;
    // BossSfxAutoWire adds it to the chase and fills it in when the editor loads.
    public sealed class BossSfx : MonoBehaviour
    {
        [Header("Listens to")]
        [SerializeField] private BossChase chase;

        [Header("The entrance")]
        [SerializeField] private AudioClip gust;
        [SerializeField, Range(0f, 1f)] private float gustVolume = 0.4f;
        [SerializeField] private AudioClip appear;
        [SerializeField, Range(0f, 1f)] private float appearVolume = 0.6f;

        [Header("Its moves")]
        // Lit as it takes the bomb out to throw; a warp away from a touch goes straight to the burst.
        [SerializeField] private AudioClip smokeLit;
        [SerializeField, Range(0f, 1f)] private float smokeLitVolume = 0.5f;
        [SerializeField] private AudioClip smoke;
        [SerializeField, Range(0f, 1f)] private float smokeVolume = 0.6f;
        [SerializeField] private AudioClip gunPump;
        [SerializeField] private AudioClip gunFire;
        [SerializeField, Range(0f, 1f)] private float gunVolume = 0.55f;
        // The circles swirl and the water strikes once a volley, however many circles there are.
        [SerializeField] private AudioClip rainWarn;
        [SerializeField, Range(0f, 1f)] private float rainWarnVolume = 0.4f;
        [SerializeField] private AudioClip rainStrike;
        [SerializeField, Range(0f, 1f)] private float rainStrikeVolume = 0.65f;
        [SerializeField] private AudioClip soaked;
        [SerializeField, Range(0f, 1f)] private float soakedVolume = 0.7f;
        [SerializeField] private AudioClip cape;
        [SerializeField] private AudioClip disguised;
        [SerializeField] private AudioClip reveal;
        [SerializeField, Range(0f, 1f)] private float capeVolume = 0.55f;

        [Header("The chase's end")]
        [SerializeField] private AudioClip hit;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;
        // The bounty's chime, shared with the thieves.
        [SerializeField] private AudioClip coin;
        [SerializeField, Range(0f, 1f)] private float coinVolume = 0.8f;
        // Shared with the thieves' catch; the cuffs and the stinger come as the scuffle's cloud bursts.
        [SerializeField] private AudioClip scuffle;
        [SerializeField] private AudioClip cuffs;
        [SerializeField] private AudioClip caught;
        [SerializeField, Range(0f, 1f)] private float caughtVolume = 0.8f;
        [SerializeField] private AudioClip escape;
        [SerializeField, Range(0f, 1f)] private float escapeVolume = 0.6f;

        [Header("The rooftop")]
        // A loop, swelling in and out with each pass of the helicopter (far off, so quiet).
        [SerializeField] private AudioClip helicopter;
        [SerializeField, Range(0f, 1f)] private float helicopterVolume = 0.25f;

        // The same clip starting again within this long (several at once) is not played again.
        [SerializeField, Min(0f)] private float sameSoundGapSeconds = 0.08f;
        [SerializeField, Range(1, 8)] private int voiceCount = 6;

        private SfxVoices voices;
        private AudioSource rotor;
        private BossController boss;
        private BossHelicopter flying;
        private float passStartedAt;
        private float passSeconds;

        private void Awake()
        {
            voices = new SfxVoices(transform, voiceCount, sameSoundGapSeconds);
            var holder = new GameObject("Helicopter");
            holder.transform.SetParent(transform, false);
            rotor = holder.AddComponent<AudioSource>();
            rotor.playOnAwake = false;
            rotor.spatialBlend = 0f;
            rotor.loop = true;
        }

        private void OnEnable()
        {
            if (chase == null) return;

            chase.BossOnFloor += OnBossOnFloor;
            chase.HelicopterFlying += OnHelicopterFlying;
            chase.EntranceStarted += OnEntranceStarted;
            chase.BossCaught += OnBossCaught;
            chase.BossRunningOff += OnBossRunningOff;
        }

        private void OnDisable()
        {
            if (chase != null)
            {
                chase.BossOnFloor -= OnBossOnFloor;
                chase.HelicopterFlying -= OnHelicopterFlying;
                chase.EntranceStarted -= OnEntranceStarted;
                chase.BossCaught -= OnBossCaught;
                chase.BossRunningOff -= OnBossRunningOff;
            }

            StopListeningToBoss();
            StopListeningToHelicopter();
            rotor.Stop();
        }

        // The rotor swells in and out over each pass, and stops with the rooftop.
        private void Update()
        {
            if (flying == null)
            {
                if (rotor.isPlaying) rotor.Stop();
                return;
            }

            float across = passSeconds > 0f ? (Time.time - passStartedAt) / passSeconds : 1f;
            if (across >= 1f)
            {
                if (rotor.isPlaying) rotor.Stop();
                return;
            }

            rotor.volume = helicopterVolume * Mathf.Sin(Mathf.PI * across);
        }

        private void OnBossOnFloor(BossController next)
        {
            StopListeningToBoss();
            boss = next;
            BossMoments moments = boss.Moments;
            moments.SmokeLit += OnSmokeLit;
            moments.SmokeBurst += OnSmokeBurst;
            moments.Appeared += OnAppeared;
            moments.GunRaised += OnGunRaised;
            moments.WaterFired += OnWaterFired;
            moments.WaterStruck += OnWaterStruck;
            moments.CapeWrapped += OnCapeWrapped;
            moments.Disguised += OnDisguised;
            moments.CapeThrownOff += OnCapeThrownOff;
            boss.Touched += OnTouched;
            boss.Shot += OnShot;
        }

        // The boss of the last chase, which may be destroyed by now (its events still let go).
        private void StopListeningToBoss()
        {
            if (ReferenceEquals(boss, null)) return;

            BossMoments moments = boss.Moments;
            moments.SmokeLit -= OnSmokeLit;
            moments.SmokeBurst -= OnSmokeBurst;
            moments.Appeared -= OnAppeared;
            moments.GunRaised -= OnGunRaised;
            moments.WaterFired -= OnWaterFired;
            moments.WaterStruck -= OnWaterStruck;
            moments.CapeWrapped -= OnCapeWrapped;
            moments.Disguised -= OnDisguised;
            moments.CapeThrownOff -= OnCapeThrownOff;
            boss.Touched -= OnTouched;
            boss.Shot -= OnShot;
            boss = null;
        }

        private void OnHelicopterFlying(BossHelicopter next)
        {
            StopListeningToHelicopter();
            flying = next;
            flying.PassStarted += OnPassStarted;
        }

        private void StopListeningToHelicopter()
        {
            if (ReferenceEquals(flying, null)) return;

            flying.PassStarted -= OnPassStarted;
            flying = null;
        }

        private void OnPassStarted(float seconds)
        {
            passStartedAt = Time.time;
            passSeconds = seconds;
            if (helicopter == null) return;

            rotor.clip = helicopter;
            rotor.volume = 0f;
            // Somewhere in the loop, so each pass does not start on the same beat.
            rotor.time = Random.Range(0f, helicopter.length);
            if (!rotor.isPlaying) rotor.Play();
        }

        private void OnEntranceStarted() => Play(gust, gustVolume);

        private void OnAppeared() => Play(appear, appearVolume);

        private void OnSmokeLit() => Play(smokeLit, smokeLitVolume);

        private void OnSmokeBurst() => Play(smoke, smokeVolume);

        private void OnGunRaised() => Play(gunPump, gunVolume);

        private void OnWaterFired()
        {
            Play(gunFire, gunVolume);
            Play(rainWarn, rainWarnVolume);
        }

        private void OnWaterStruck() => Play(rainStrike, rainStrikeVolume);

        private void OnShot(UWBPlayerController player) => Play(soaked, soakedVolume);

        private void OnCapeWrapped() => Play(cape, capeVolume);

        private void OnDisguised() => Play(disguised, capeVolume);

        private void OnCapeThrownOff() => Play(reveal, capeVolume);

        private void OnTouched(UWBPlayerController player)
        {
            Play(hit, hitVolume);
            StartCoroutine(PlayLater(coin, coinVolume, 0.15f));
        }

        private void OnBossCaught()
        {
            Play(scuffle, caughtVolume);
            StartCoroutine(PlayLater(cuffs, caughtVolume, CatchBrawl.StampSeconds));
            StartCoroutine(PlayLater(caught, caughtVolume, CatchBrawl.StampSeconds));
        }

        private void OnBossRunningOff() => Play(escape, escapeVolume);

        private IEnumerator PlayLater(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            Play(clip, volume);
        }

        private void Play(AudioClip clip, float volume)
        {
            voices?.Play(clip, volume);
        }
    }
}
