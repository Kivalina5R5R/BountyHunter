using System;
using System.Collections;
using Fortal.Display;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Switches between maps with the N key: each map has its own floor painting, hiding spot art
    // (bushes, bins, ...) and the bits thrown out when a thief dives in (leaves, trash). A switch
    // lays out new hiding spots and starts the next stage from round 1 (BountyRounds). The first map is applied before anything spawns, so it decides what Play starts with.
    // The stage's clock running out (StageTimer.TimeUp) moves on to the next map the same way, and N
    // or the clock running out on the last map, once the game is under way, ends the game and brings
    // up the result screen. A map can have a mini game after it (Interlude After, a StageInterlude):
    // the screen fades to black, the floor is cleared (BountyRounds.ClearFloor) and the stage's
    // pieces in hideDuringInterlude are put away, the mini game plays, and after another fade the
    // next map starts. N during a mini game cuts it short; M jumps straight to the next mini game.
    // The finale (the boss chase) plays the same way once the last map is done, but on that map's
    // floor with the stage's pieces left up (the score tags count along), and the result follows it.
    // With a boss transition set, the stage freezes the moment it ends (its clock, thieves and
    // passers-by stop where they are) and the wall plays the boss cut-in instead of the fade.
    [DefaultExecutionOrder(-100)]
    public sealed class MapSwitcher : MonoBehaviour
    {
        [Serializable]
        private sealed class MapTheme
        {
            [SerializeField] private string name = "Map";
            [SerializeField] private Texture2D floor;
            [SerializeField] private Sprite[] hidingSpots;
            // Random turn of each hiding spot either way: 180 for top-down bushes, small for upright bins.
            [SerializeField, Range(0f, 180f)] private float maxTiltDegrees = 180f;
            // Size as a multiple of the player width on the art's narrowest side, picked in this range.
            [SerializeField] private Vector2 sizeVsPlayer = new Vector2(1.15f, 2.3f);
            // Thrown out when a thief dives in or runs out; leave empty for none.
            [SerializeField] private Sprite[] burstSprites;
            // Played once this map's stage is over, before the next map; leave empty for none.
            [SerializeField] private StageInterlude interludeAfter;

            public string Name => name;
            public Texture2D Floor => floor;
            public Sprite[] HidingSpots => hidingSpots;
            public float MaxTiltDegrees => maxTiltDegrees;
            public Vector2 SizeVsPlayer => sizeVsPlayer;
            public Sprite[] BurstSprites => burstSprites;
            public StageInterlude InterludeAfter => interludeAfter;
        }

        [SerializeField] private RawImage floorImage;
        [SerializeField] private BushScatter hidingSpots;
        [SerializeField] private ThiefSpawner thieves;
        // Puts up the posters, spawns their thieves and restarts the timer for every new stage.
        [SerializeField] private BountyRounds rounds;
        // Ends the game when N is pressed on the last map.
        [SerializeField] private ResultSequence result;
        // Each stage lasts as long as its clock; when it runs out the next map starts.
        [SerializeField] private StageTimer timer;
        [SerializeField] private MapTheme[] maps;

        [Header("Mini games between maps")]
        [SerializeField] private FadeCurtain curtain;
        // Put away while a mini game plays (the hiding spots, the game wall with its posters and dogs),
        // back on before the next map fades in.
        [SerializeField] private GameObject[] hideDuringInterlude;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.45f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;
        // Played after the last map, before the result; leave empty to go straight to the result.
        [SerializeField] private StageInterlude finale;
        // The cut-in on the wall into the finale (the floor fades dark under it); leave empty to
        // fade into the finale like a mini game.
        [SerializeField] private BossTransition bossTransition;

        private int current;
        private bool finalePlayed;
        private StageInterlude playing;
        // True while the stage is frozen under the boss cut-in.
        private bool frozen;

        // Which map is on now (0 first), and when the game moves on to another one.
        public int CurrentMap => current;
        public event Action<int> MapSwitched;

        private void Awake()
        {
            if (maps == null || maps.Length == 0) return;
            Apply(maps[0]);
        }

        private void Start()
        {
            if (timer != null) timer.TimeUp += OnTimeUp;
            if (result != null) result.GameEnded += OnGameEnded;
        }

        private void OnDestroy()
        {
            if (timer != null) timer.TimeUp -= OnTimeUp;
            if (result != null) result.GameEnded -= OnGameEnded;
            Unfreeze();
        }

        // The game ended some other way (R) while the finale plays: it winds up at once.
        private void OnGameEnded()
        {
            if (playing != null && playing == finale) finale.Skip();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.mKey.wasPressedThisFrame) JumpToInterlude();
            if (!Keyboard.current.nKey.wasPressedThisFrame) return;
            if (playing != null) playing.Skip();
            else Advance();
        }

        // M, for trying the game out: ends the stage now and plays the next mini game, from this map
        // or the first later map that has one, then carries on from the map after it as usual.
        // Only once the game is under way (the clock lives on the game wall), never during the intro.
        private void JumpToInterlude()
        {
            if (playing != null || (result != null && result.Ended) || maps == null) return;
            if (timer != null && !timer.isActiveAndEnabled) return;

            for (int map = current; map < maps.Length - 1; map++)
            {
                if (maps[map].InterludeAfter == null) continue;

                if (map != current)
                {
                    current = map;
                    Apply(maps[map]);
                }

                Debug.Log($"[MapSwitcher] M: straight to {maps[map].InterludeAfter.name}", this);
                StartCoroutine(PlayThenSwitch(maps[map].InterludeAfter));
                return;
            }

            Debug.Log("[MapSwitcher] M: no mini game left to jump to", this);
        }

        private void OnTimeUp()
        {
            Advance();
        }

        // The next map, or the result once the last map is done.
        private void Advance()
        {
            if (playing != null || (result != null && result.Ended)) return;
            bool lastMap = maps == null || maps.Length < 2 || current == maps.Length - 1;
            if (lastMap && finale != null && !finalePlayed && GameUnderWay())
            {
                StartCoroutine(PlayFinale());
                return;
            }

            if (lastMap && result != null && result.TryEndGame()) return;
            if (maps == null || maps.Length < 2) return;

            StageInterlude interlude = maps[current].InterludeAfter;
            if (interlude != null)
            {
                StartCoroutine(PlayThenSwitch(interlude));
                return;
            }

            current = (current + 1) % maps.Length;
            SwitchTo(maps[current]);
        }

        // The mini game after the current map, between two fades, then the next map.
        private IEnumerator PlayThenSwitch(StageInterlude interlude)
        {
            yield return Enter(interlude);
            yield return interlude.Run();

            // Back to the stage while the screen is covered (by the mini game's own way out, or dark):
            // its pieces come back on first, so the mini game can hand them what it settled (the
            // money won) as it ends.
            WallTransition way = interlude.ExitTransition;
            if (way != null && way.WipesOver && interlude.WallPanel != null)
            {
                yield return WipeOff(interlude, way);
                yield break;
            }

            if (way != null)
            {
                Coroutine floorDark = curtain != null ? StartCoroutine(curtain.CloseFloor(fadeOutSeconds)) : null;
                yield return way.Close();
                if (floorDark != null) yield return floorDark;
            }
            else if (curtain != null)
            {
                yield return curtain.Close(fadeOutSeconds);
            }

            SetStagePieces(true);
            interlude.End();
            playing = null;
            current = (current + 1) % maps.Length;
            SwitchTo(maps[current]);
            if (way != null)
            {
                // The floor stays dark until the way out has shown the new map on the wall.
                yield return way.Open();
                if (curtain != null) yield return curtain.OpenFloor(fadeInSeconds);
            }
            else if (curtain != null)
            {
                yield return curtain.Open(fadeInSeconds);
            }
        }

        // The way out starts wiping the mini game's wall panel off at once, the stage's wall pieces
        // showing behind it; the floor keeps the mini game's look as it fades dark over the first half
        // of the wipe, and only then do the stage's floor pieces come back with the next map. Once the
        // wipe is done the mini game ends (handing over what it settled) and the floor comes back.
        private IEnumerator WipeOff(StageInterlude interlude, WallTransition way)
        {
            Transform wall = interlude.WallPanel.parent;
            SetStagePieces(true, piece => wall != null && piece.transform.IsChildOf(wall));
            Coroutine wipe = StartCoroutine(way.Wipe(interlude.WallPanel));
            float floorDark = way.WipeSeconds > 0f ? way.WipeSeconds * 0.5f : fadeOutSeconds;
            if (curtain != null) yield return curtain.CloseFloor(floorDark);
            SetStagePieces(true);
            current = (current + 1) % maps.Length;
            SwitchTo(maps[current]);
            yield return wipe;
            interlude.End();
            playing = null;
            if (curtain != null) yield return curtain.OpenFloor(fadeInSeconds);
        }

        // The game is past the intro (the clock lives on the game wall).
        private bool GameUnderWay()
        {
            return timer == null || timer.isActiveAndEnabled;
        }

        // The finale, faded or cut in, then the result.
        private IEnumerator PlayFinale()
        {
            playing = finale;
            finalePlayed = true;
            bool cutIn = bossTransition != null && bossTransition.IsSetUp;
            if (cutIn) yield return CoverWithCutIn();
            else if (curtain != null) yield return curtain.Close(fadeOutSeconds);
            if (timer != null) timer.Stop();
            if (rounds != null) rounds.ClearFloor();
            // The stage is gone from the floor, so nothing is left to act; time runs again for the chase.
            Unfreeze();
            // A fault setting it up must not leave the screen dark: it is logged and the game goes on.
            yield return finale.Prepare();
            try
            {
                finale.Begin();
            }
            catch (Exception fault)
            {
                Debug.LogException(fault, this);
            }

            Debug.Log($"[MapSwitcher] {finale.name} plays", this);
            if (cutIn)
            {
                if (curtain != null) StartCoroutine(curtain.OpenFloor(fadeInSeconds));
                yield return bossTransition.Close();
            }
            else if (curtain != null)
            {
                yield return curtain.Open(fadeInSeconds);
            }

            yield return finale.Run();

            // The result fades the screen out itself.
            playing = null;
            if (result != null) result.TryEndGame();
            finale.End();
        }

        // Freezes the stage where it is and plays the boss cut-in on the wall until it covers it, the
        // floor fading dark meanwhile.
        private IEnumerator CoverWithCutIn()
        {
            if (timer != null) timer.Stop();
            Time.timeScale = 0f;
            frozen = true;
            Coroutine floorDark = curtain != null ? StartCoroutine(curtain.CloseFloor(fadeOutSeconds)) : null;
            yield return bossTransition.Open();
            if (floorDark != null) yield return floorDark;
        }

        private void Unfreeze()
        {
            if (!frozen) return;

            Time.timeScale = 1f;
            frozen = false;
        }

        // Fades out of the stage (its clock stopped, its floor cleared and its pieces put away) into
        // the mini game.
        private IEnumerator Enter(StageInterlude interlude)
        {
            playing = interlude;
            if (curtain != null) yield return curtain.Close(fadeOutSeconds);
            if (timer != null) timer.Stop();
            if (rounds != null) rounds.ClearFloor();
            SetStagePieces(false);
            yield return interlude.Prepare();
            interlude.Begin();
            Debug.Log($"[MapSwitcher] {interlude.name} plays", this);
            if (curtain != null) yield return curtain.Open(fadeInSeconds);
        }

        // Shows or hides the stage's pieces, or only those which picks.
        private void SetStagePieces(bool shown, Func<GameObject, bool> which = null)
        {
            if (hideDuringInterlude == null) return;
            foreach (GameObject piece in hideDuringInterlude)
            {
                if (piece != null && (which == null || which(piece))) piece.SetActive(shown);
            }
        }

        private void SwitchTo(MapTheme map)
        {
            if (thieves != null) thieves.RemoveThieves();
            Apply(map);
            if (hidingSpots != null) hidingSpots.Scatter();
            if (rounds != null) rounds.StartStage(current + 1);
            MapSwitched?.Invoke(current);
            Debug.Log($"[MapSwitcher] Switched to {map.Name}", this);
        }

        private void Apply(MapTheme map)
        {
            if (floorImage != null && map.Floor != null)
            {
                floorImage.texture = map.Floor;
                AspectRatioFitter fitter = floorImage.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = (float)map.Floor.width / map.Floor.height;
            }

            if (hidingSpots != null) hidingSpots.UseSprites(map.HidingSpots, map.MaxTiltDegrees, map.SizeVsPlayer);
            if (thieves != null) thieves.UseBurstSprites(map.BurstSprites);
        }
    }
}
