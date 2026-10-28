using System;
using System.Collections;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The intro before the game: the wall shows the police station with the patrol car rushing in
    // and the dogs jumping out (a green-screen video on WallVideo, played once), and the floor shows
    // the menu floor where the players pick their characters. Once every player has picked, a 3, 2, 1
    // counts down in the middle of the floor and the game starts (Enter or Space starts it at once).
    // The game wall, the hiding spots and the rounds wait until then. Turn Play Intro off to skip it.
    // Console lines start with [IntroSequence].
    [DefaultExecutionOrder(-90)]
    public sealed class IntroSequence : MonoBehaviour
    {
        [SerializeField] private bool playIntro = true;
        // The intro's own pictures on the wall, hidden when the game starts.
        [SerializeField] private GameObject introWall;
        // The game's wall (Map/SideDisplay/Play), shown when the game starts.
        [SerializeField] private GameObject playWall;
        [SerializeField] private RawImage floorImage;
        [SerializeField] private Texture introFloor;
        // Game things on the floor kept away during the intro (the hiding spots).
        [SerializeField] private GameObject[] gameFloorPieces;
        // Intro things on the floor, only there during the intro (the character cards).
        [SerializeField] private GameObject[] introFloorPieces;
        [SerializeField] private BountyRounds rounds;
        [SerializeField] private CharacterSelect characterSelect;
        // The intro wall; the character cards come out once it is idling.
        [SerializeField] private IntroShow introShow;
        [SerializeField] private FloorCountdown countdown;
        // Its players are locked in when the game starts, so a tag connecting later stays out.
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        // The wall's score tags, handed to the players by the characters they picked.
        [SerializeField] private CharacterScoreTags scoreTags;
        // Fades the wall and the floor to black and back while the intro gives way to the game.
        [SerializeField] private FadeCurtain curtain;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.45f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;

        private Texture gameFloor;
        private float gameFloorAspect;
        private bool showing;

        private void Awake()
        {
            if (!playIntro)
            {
                if (introWall != null) introWall.SetActive(false);
                SetPieces(introFloorPieces, false);
                return;
            }

            showing = true;
            if (rounds != null) rounds.WaitForIntro();
            if (introWall != null) introWall.SetActive(true);
            if (playWall != null) playWall.SetActive(false);
            SetPieces(gameFloorPieces, false);
            SetPieces(introFloorPieces, true);
            ShowIntroFloor();
            HoldCardsForWall();
            Debug.Log("[IntroSequence] Intro on. Press Enter or Space to start the game.", this);
        }

        private void OnDestroy()
        {
            if (introShow != null) introShow.Idling -= RevealCards;
        }

        private void Start()
        {
            ScenePreload.Enqueue(this, 5, PrepareEffects());
        }

        private IEnumerator PrepareEffects()
        {
            _ = PosterSprites.Dot;
            yield return null;
            _ = PosterSprites.Pin;
            yield return null;
            _ = PosterSprites.Ring;
        }

        // The cards wait hidden until the wall's patrol car has parked and the dogs are idling.
        private void HoldCardsForWall()
        {
            if (introShow == null || characterSelect == null || introShow.IsIdling) return;

            characterSelect.WaitForReveal();
            introShow.Idling += RevealCards;
        }

        private void RevealCards()
        {
            introShow.Idling -= RevealCards;
            if (showing && characterSelect != null) characterSelect.Reveal();
        }

        private void Update()
        {
            if (!showing) return;

            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                StartGame();
                return;
            }

            // Counts down once everyone has picked; a newcomer who has not picked yet stops the count.
            if (characterSelect == null || countdown == null) return;
            bool ready = characterSelect.EveryoneChosen;
            if (ready && !countdown.Running) countdown.Begin(StartGame);
            else if (!ready && countdown.Running) countdown.Cancel();
        }

        // True while the intro is on (until the game starts).
        public bool IsShowing => showing;

        // Raised the moment the intro starts giving way to the game (as the fade to black begins).
        public event Action Leaving;

        // Raised once the game is on: the first round has begun and the picture is fading back in.
        public event Action GameStarted;

        // Leaves the intro and starts the first round, fading through black.
        public void StartGame()
        {
            if (!showing) return;

            showing = false;
            Leaving?.Invoke();
            StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            if (curtain != null) yield return curtain.Close(fadeOutSeconds);

            if (introWall != null) introWall.SetActive(false);
            if (playWall != null) playWall.SetActive(true);
            SetPieces(gameFloorPieces, true);
            SetPieces(introFloorPieces, false);
            RestoreGameFloor();
            LockPlayers();
            if (scoreTags != null && characterSelect != null) scoreTags.Assign(characterSelect.Chosen);
            Debug.Log("[IntroSequence] Game starts", this);

            // The hiding spots lay themselves out in their first Start, a frame after they are
            // switched on; the round waits for that so its thieves can start hidden in them.
            yield return null;
            if (rounds != null) rounds.Begin();
            GameStarted?.Invoke();
            if (curtain != null) yield return curtain.Open(fadeInSeconds);
        }

        // The players who picked a character play; tags that connect from now on are kept out. When
        // the game was started by key with nobody having picked, everyone online plays.
        private void LockPlayers()
        {
            if (playerSpawner == null) return;

            var playing = new List<int>();
            if (characterSelect != null) playing.AddRange(characterSelect.Chosen.Keys);
            if (playing.Count == 0)
            {
                foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                {
                    if (player != null && player.isActiveAndEnabled) playing.Add(player.PlayerId);
                }
            }

            playerSpawner.LockRoster(playing);
        }

        private static void SetPieces(GameObject[] pieces, bool active)
        {
            if (pieces == null) return;
            foreach (GameObject piece in pieces)
            {
                if (piece != null) piece.SetActive(active);
            }
        }

        private void ShowIntroFloor()
        {
            if (floorImage == null || introFloor == null) return;

            gameFloor = floorImage.texture;
            AspectRatioFitter fitter = floorImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                gameFloorAspect = fitter.aspectRatio;
                fitter.aspectRatio = (float)introFloor.width / introFloor.height;
            }

            floorImage.texture = introFloor;
        }

        private void RestoreGameFloor()
        {
            if (floorImage == null || gameFloor == null) return;

            floorImage.texture = gameFloor;
            AspectRatioFitter fitter = floorImage.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = gameFloorAspect;
        }
    }
}
