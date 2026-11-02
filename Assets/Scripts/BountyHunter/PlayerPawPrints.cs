using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Faint paw prints behind the players while they run (they are the police dogs), left and right
    // in turn and pointing the way they ran, fading away after a moment. Their own look, so they are
    // never mistaken for the thieves' dust, and pale and see-through so they blend into the floor
    // rather than catch the eye. Only a run leaves prints: the speed is smoothed first, so the tags'
    // jitter while someone stands or walks leaves none. Nothing during the intro (character picking);
    // they start once the game is on. One for all the players; PlayerPawPrintsAutoWire adds it.
    public sealed class PlayerPawPrints : MonoBehaviour
    {
        private const float Height = 0.025f;
        // Under the hiding spots' shadows (4), on the floor itself.
        private const int SortingOrder = 3;
        private const int TextureSize = 64;
        private const float FadeInSeconds = 0.06f;

        [SerializeField] private UWBPlayerSpawner players;
        // No prints while it shows (the character picking).
        [SerializeField] private IntroSequence intro;
        // Below this smoothed speed (metres a second) no prints: above a walk, below a run.
        [SerializeField, Min(0f)] private float minimumSpeed = 1.4f;
        // How quickly the speed follows the tag (higher follows faster but lets more jitter through).
        [SerializeField, Min(0.5f)] private float speedSmoothing = 6f;
        // Sizes against the player's floor marker (its plain circle, about 0.67 m across), so the
        // prints keep in proportion to it: a print's width, how far either side of the path the
        // prints land, and the distance run between one print and the next.
        [SerializeField, Range(0.05f, 1f)] private float printVsPlayer = 0.32f;
        [SerializeField, Range(0f, 1f)] private float sideStepVsPlayer = 0.16f;
        [SerializeField, Range(0.2f, 3f)] private float strideVsPlayer = 0.7f;
        [SerializeField, Min(0.1f)] private float lifeSeconds = 1.1f;
        [SerializeField] private Color colour = new Color(1f, 0.95f, 0.85f, 0.25f);

        private sealed class Runner
        {
            public Vector3 LastPosition;
            public Vector3 Velocity;
            public float Travelled;
            public bool LeftFoot;
            // The player's marker width (metres).
            public float Width;
        }

        private sealed class Print
        {
            public SpriteRenderer Renderer;
            public float Age;
        }

        private static Sprite paw;

        private readonly Dictionary<UWBPlayerController, Runner> runners = new Dictionary<UWBPlayerController, Runner>();
        private readonly List<Print> prints = new List<Print>();
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private void Update()
        {
            Fade();
            if (players == null) return;

            bool picking = intro != null && intro.IsShowing;
            float seconds = Mathf.Max(Time.deltaTime, 0.0001f);
            foreach (UWBPlayerController player in players.SpawnedPlayers)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                Follow(player, seconds, !picking);
            }
        }

        // Keeps each player's smoothed speed (also during the intro, so a print never comes from the
        // jump at the start) and leaves a print every stride of a run.
        private void Follow(UWBPlayerController player, float seconds, bool leavePrints)
        {
            Vector3 position = player.transform.position;
            if (!runners.TryGetValue(player, out Runner runner))
            {
                runners[player] = new Runner { LastPosition = position, Width = PlayerMarker.Width(player) };
                return;
            }

            Vector3 step = position - runner.LastPosition;
            runner.LastPosition = position;
            step.y = 0f;
            runner.Velocity = Vector3.Lerp(runner.Velocity, step / seconds, 1f - Mathf.Exp(-speedSmoothing * seconds));
            float speed = runner.Velocity.magnitude;
            if (!leavePrints || speed < minimumSpeed)
            {
                runner.Travelled = 0f;
                return;
            }

            Vector3 direction = runner.Velocity / speed;
            float stride = runner.Width * strideVsPlayer;
            runner.Travelled += step.magnitude;
            while (runner.Travelled >= stride)
            {
                runner.Travelled -= stride;
                runner.LeftFoot = !runner.LeftFoot;
                Leave(position, direction, runner.LeftFoot, runner.Width);
            }
        }

        private void Leave(Vector3 at, Vector3 direction, bool leftFoot, float playerWidth)
        {
            var sideways = new Vector3(direction.z, 0f, -direction.x);
            float side = playerWidth * sideStepVsPlayer;
            Vector3 spot = at + sideways * (leftFoot ? -side : side);
            SpriteRenderer print = spare.Count > 0 ? spare.Pop() : NewPrint();
            print.gameObject.SetActive(true);
            print.transform.position = new Vector3(spot.x, Height, spot.z);
            // Lying flat, toes the way the player ran.
            float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            print.transform.rotation = Quaternion.Euler(90f, heading, 0f);
            print.transform.localScale = Vector3.one * (playerWidth * printVsPlayer);
            SetAlpha(print, 0f);
            prints.Add(new Print { Renderer = print, Age = 0f });
        }

        // Each print comes in quickly and fades out over the rest of its life, then goes back to the spares.
        private void Fade()
        {
            for (int i = prints.Count - 1; i >= 0; i--)
            {
                Print print = prints[i];
                print.Age += Time.deltaTime;
                if (print.Age >= lifeSeconds)
                {
                    print.Renderer.gameObject.SetActive(false);
                    spare.Push(print.Renderer);
                    prints.RemoveAt(i);
                    continue;
                }

                float fadeIn = Mathf.Clamp01(print.Age / FadeInSeconds);
                float fadeOut = 1f - Mathf.Clamp01((print.Age - FadeInSeconds) / (lifeSeconds - FadeInSeconds));
                SetAlpha(print.Renderer, fadeIn * fadeOut * fadeOut);
            }
        }

        private void SetAlpha(SpriteRenderer print, float share)
        {
            Color shown = colour;
            shown.a *= share;
            print.color = shown;
        }

        private SpriteRenderer NewPrint()
        {
            var holder = new GameObject("Paw Print", typeof(SpriteRenderer));
            holder.transform.SetParent(transform, false);
            var print = holder.GetComponent<SpriteRenderer>();
            print.sprite = Paw();
            print.sortingOrder = SortingOrder;
            return print;
        }

        // A soft-edged paw one unit across: a round pad with four toes above it, toes up.
        private static Sprite Paw()
        {
            if (paw != null) return paw;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { name = "Paw Print", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var at = new Vector2((x + 0.5f) / TextureSize, (y + 0.5f) / TextureSize);
                    float cover = Mathf.Max(
                        Blob(at, new Vector2(0.5f, 0.34f), new Vector2(0.24f, 0.19f)),
                        Mathf.Max(
                            Mathf.Max(Blob(at, new Vector2(0.2f, 0.62f), new Vector2(0.085f, 0.105f)), Blob(at, new Vector2(0.39f, 0.76f), new Vector2(0.09f, 0.11f))),
                            Mathf.Max(Blob(at, new Vector2(0.61f, 0.76f), new Vector2(0.09f, 0.11f)), Blob(at, new Vector2(0.8f, 0.62f), new Vector2(0.085f, 0.105f)))));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            paw = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            return paw;
        }

        // 1 inside an oval, fading to 0 over a soft rim.
        private static float Blob(Vector2 at, Vector2 centre, Vector2 radius)
        {
            float dx = (at.x - centre.x) / radius.x;
            float dy = (at.y - centre.y) / radius.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((1.15f - distance) / 0.3f);
        }
    }
}
