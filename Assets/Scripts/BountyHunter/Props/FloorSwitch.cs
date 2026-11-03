using System;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Something on the floor a player sets off by standing on it (a sprinkler valve, a crate stack, a
    // switch): a ring round it in the look of the players' own floor rings (Tools/ParkArt) springs out
    // as a player steps on and fills while anyone stands within its radius; full, it goes off
    // (Triggered, with the player nearest it) and rests for its cooldown. Stepping off does not start
    // it over: the ring drains slowly instead, since the tags never hold perfectly still, and shrinks
    // away once it is empty. While it rests the prop is dimmed; ready again, it pops. While it waits
    // to be used it draws the players in: a glow breathing under it and stars twinkling round it
    // (FloorSwitchLure), a gleam sweeping across the prop now and then, and now and then it swells
    // and shakes. It answers the player too: a springy bounce with a twist as someone steps on, then
    // while they stand it lifts a little and rocks gently, more as the ring fills, and it settles
    // back as they leave. It springs up as it is made; Vanish shrinks it away in a burst of stars and
    // removes it. Its owner makes it and listens to it.
    public sealed class FloorSwitch : MonoBehaviour
    {
        private const float Height = 0.035f;
        // Over the hiding spots (5), under the thieves (8); the ring just over the prop.
        private const int PropOrder = 6;
        private const int RingOrder = 7;
        // How fast the ring drains when nobody stands on it, against how fast it fills.
        private const float DrainShare = 0.5f;
        private const float PopSeconds = 0.3f;
        // The bounce as a player steps on: how much it swells and twists (degrees), and for how long.
        private const float StepBounce = 0.22f;
        private const float StepTwist = 10f;
        private const float StepSeconds = 0.5f;
        // While someone stands on it: how much it lifts at a full ring, how far it rocks either way
        // (degrees) and how fast, and how quickly the rock eases in and out.
        private const float HoldLift = 0.08f;
        private const float HoldRock = 4f;
        private const float RocksPerSecond = 1.8f;
        private const float RockEaseSeconds = 0.3f;
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly Color RestingColour = new Color(0.6f, 0.6f, 0.6f, 0.75f);
        // The back ring keeps its own cream colour, a little see-through.
        private static readonly Color BackColour = new Color(1f, 1f, 1f, 0.85f);
        // How long the ring takes to spring out or shrink away.
        private const float RingSeconds = 0.25f;
        // While it waits to be used: every so often it swells a little and shakes (how often, how
        // long, how much bigger, how far it shakes in degrees).
        private const float AttentionEverySeconds = 2.4f;
        private const float AttentionSeconds = 0.6f;
        private const float AttentionSwell = 0.18f;
        private const float AttentionDegrees = 10f;
        // How long it takes to spring up as it is made, and to shrink away (Vanish).
        private const float AppearSeconds = 0.4f;
        private const float VanishSeconds = 0.6f;
        // The gleam: how often it sweeps across, how long one sweep takes, how wide the band is
        // (share of the prop) and how far it slants (degrees). Over the prop, inside its outline.
        private const float GleamEverySeconds = 2.6f;
        private const float GleamSeconds = 0.55f;
        private const float GleamWidth = 0.22f;
        private const float GleamSlant = 25f;
        private const int GleamOrder = 7;
        private const int GleamTextureSize = 64;
        private static readonly Color GleamColour = new Color(1f, 1f, 0.92f, 0.75f);
        private static Sprite gleamSprite;

        private UWBPlayerSpawner players;
        private SpriteRenderer prop;
        private SpriteRenderer ringBack;
        private SpriteRenderer ringFill;
        private MaterialPropertyBlock block;
        private Color colour;
        private float radius;

        // How far round it a player sets it off (and its ring reaches).
        public float Radius => radius;
        private float holdSeconds;
        private float cooldownSeconds;
        private float propScale;
        private float progress;
        private float restUntil;
        private float readyAt = float.MinValue;
        private bool someoneWasOn;
        private float steppedOnAt = float.MinValue;
        private float rockAmount;
        private float rockPhase;
        // The ring's own size, how far it is shown (0 hidden, 1 up), and what draws players in.
        private Vector3 ringScale = Vector3.one;
        private float ringShown;
        private FloorSwitchLure lure;
        private SpriteRenderer gleam;
        private float bornAt;
        private float vanishedAt = float.MaxValue;
        // Where this switch is in its calls for attention, so switches differ.
        private float attentionPhase;

        // A player stood on it long enough (the one nearest it).
        public event Action<FloorSwitch, UWBPlayerController> Triggered;

        public bool Resting => Time.time < restUntil;

        // ringBack (may be null, for none) is drawn as it is; ringFill is tinted colour and revealed
        // round as it fills. The stars round it are drawn from sparkles (may be empty).
        public static FloorSwitch Create(Transform parent, string name, Vector3 at, Sprite propSprite, float propWidth, Sprite ringBack, Sprite ringFill,
            Material radialFill, Color colour, float radius, float holdSeconds, float cooldownSeconds, UWBPlayerSpawner players, Sprite[] sparkles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(at.x, Height, at.z), Quaternion.Euler(90f, 0f, 0f));
            var floorSwitch = root.AddComponent<FloorSwitch>();
            floorSwitch.players = players;
            floorSwitch.colour = colour;
            floorSwitch.radius = radius;
            floorSwitch.holdSeconds = Mathf.Max(0.05f, holdSeconds);
            floorSwitch.cooldownSeconds = cooldownSeconds;
            floorSwitch.block = new MaterialPropertyBlock();
            floorSwitch.prop = Part(root.transform, "Prop", propSprite, PropOrder);
            floorSwitch.propScale = propSprite != null ? propWidth / Mathf.Max(0.01f, propSprite.bounds.size.x) : 1f;
            if (ringBack != null) floorSwitch.ringBack = Part(root.transform, "Ring Back", ringBack, RingOrder);
            floorSwitch.ringFill = Part(root.transform, "Ring Fill", ringFill, RingOrder);
            if (radialFill != null) floorSwitch.ringFill.sharedMaterial = radialFill;
            Sprite outer = ringBack != null ? ringBack : ringFill;
            if (outer != null)
            {
                // The ring's outer edge on the switch's radius.
                floorSwitch.ringScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, outer.bounds.size.x));
            }

            floorSwitch.gleam = Gleam(floorSwitch.prop);
            floorSwitch.lure = FloorSwitchLure.Create(root.transform, propWidth, sparkles);
            floorSwitch.bornAt = Time.time;
            floorSwitch.attentionPhase = UnityEngine.Random.Range(0f, AttentionEverySeconds);
            floorSwitch.Show();
            return floorSwitch;
        }

        // Draws the prop with material (such as one that recolours it) instead of the default.
        public void UsePropMaterial(Material material)
        {
            if (prop != null && material != null) prop.sharedMaterial = material;
        }

        // Shrinks away in a burst of stars, then removes itself; it takes no more players meanwhile.
        public void Vanish()
        {
            if (Vanishing) return;

            vanishedAt = Time.time;
            progress = 0f;
            if (lure != null) lure.Burst(10);
            if (ringBack != null) ringBack.gameObject.SetActive(false);
            if (ringFill != null) ringFill.gameObject.SetActive(false);
        }

        private bool Vanishing => vanishedAt < float.MaxValue;

        private void Update()
        {
            if (Vanishing)
            {
                float gone = (Time.time - vanishedAt) / VanishSeconds;
                if (gone >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                // A quick swell, then down to nothing with a spin.
                float shrink = gone < 0.25f ? 1f + 0.2f * Mathf.Sin(gone / 0.25f * Mathf.PI * 0.5f) : 1.2f * (1f - Mathf.SmoothStep(0f, 1f, (gone - 0.25f) / 0.45f));
                prop.transform.localScale = Vector3.one * (propScale * Mathf.Max(0f, shrink));
                prop.transform.localRotation = Quaternion.Euler(0f, 0f, 180f * gone * gone);
                if (gleam != null) gleam.enabled = false;
                return;
            }

            if (Resting)
            {
                Show();
                return;
            }

            // Just rested: it pops as it is ready again.
            if (restUntil > 0f && readyAt < restUntil) readyAt = restUntil;
            bool someoneOn = NearestPlayer(out UWBPlayerController player);
            Answer(someoneOn);
            float step = Time.deltaTime / holdSeconds;
            progress = someoneOn ? progress + step : Mathf.Max(0f, progress - step * DrainShare);
            if (progress >= 1f)
            {
                progress = 0f;
                restUntil = Time.time + cooldownSeconds;
                Show();
                Triggered?.Invoke(this, player);
                return;
            }

            Show();
        }

        // Notes a player stepping on (for the bounce) and eases the rock in while someone stands on it.
        private void Answer(bool someoneOn)
        {
            if (someoneOn && !someoneWasOn) steppedOnAt = Time.time;
            someoneWasOn = someoneOn;
            rockAmount = Mathf.MoveTowards(rockAmount, someoneOn ? 1f : 0f, Time.deltaTime / RockEaseSeconds);
            rockPhase = rockAmount > 0f ? rockPhase + Time.deltaTime * RocksPerSecond * Mathf.PI * 2f : 0f;
        }

        private bool NearestPlayer(out UWBPlayerController nearest)
        {
            nearest = null;
            if (players == null) return false;

            float best = radius;
            Vector3 at = transform.position;
            foreach (UWBPlayerController player in players.SpawnedPlayers)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                Vector3 offset = player.transform.position - at;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                if (distance > best) continue;
                best = distance;
                nearest = player;
            }

            return nearest != null;
        }

        private void Show()
        {
            bool resting = Resting;
            // Waiting to be used with nobody on it: it calls for attention now and then.
            bool inviting = !resting && !someoneWasOn;
            if (lure != null) lure.Inviting = inviting;
            float attention = inviting ? Mathf.Repeat(Time.time + attentionPhase, AttentionEverySeconds) / AttentionSeconds : 1f;
            float attentionSwell = attention < 1f ? AttentionSwell * Mathf.Sin(attention * Mathf.PI) : 0f;
            float attentionShake = attention < 1f ? AttentionDegrees * Mathf.Sin(attention * Mathf.PI * 6f) * (1f - attention) : 0f;
            float pop = Mathf.Clamp01((Time.time - readyAt) / PopSeconds);
            float swell = resting ? 1f : 1f + 0.25f * Mathf.Sin(pop * Mathf.PI);
            float stepped = (Time.time - steppedOnAt) / StepSeconds;
            float bounce = stepped < 1f ? Mathf.Sin(stepped * Mathf.PI * 2.5f) * (1f - stepped) * (1f - stepped) : 0f;
            float eased = Mathf.SmoothStep(0f, 1f, rockAmount);
            float lift = HoldLift * eased * (0.4f + 0.6f * progress);
            float twist = StepTwist * bounce + HoldRock * eased * Mathf.Sin(rockPhase);
            float appear = EaseOutBack(Mathf.Clamp01((Time.time - bornAt) / AppearSeconds));
            if (prop != null)
            {
                prop.transform.localScale = Vector3.one * (propScale * appear * (swell + StepBounce * bounce + lift + attentionSwell));
                prop.transform.localRotation = Quaternion.Euler(0f, 0f, resting ? 0f : twist + attentionShake);
                prop.color = resting ? RestingColour : Color.white;
            }

            SweepGleam(inviting);

            if (ringFill == null || ringFill.sprite == null) return;

            // The ring only shows while someone is on it or it is still draining: it springs out as
            // they step on and shrinks away once it is empty.
            bool ringWanted = !resting && (someoneWasOn || progress > 0f);
            ringShown = Mathf.MoveTowards(ringShown, ringWanted ? 1f : 0f, Time.deltaTime / RingSeconds);
            Vector3 ringSize = ringScale * EaseOutBack(ringShown);
            ringFill.transform.localScale = ringSize;
            ringFill.gameObject.SetActive(ringShown > 0f);
            Color fillColour = colour;
            fillColour.a *= ringShown;
            ringFill.color = fillColour;
            if (ringBack != null)
            {
                ringBack.transform.localScale = ringSize;
                ringBack.gameObject.SetActive(ringShown > 0f);
                Color back = BackColour;
                back.a *= ringShown;
                ringBack.color = back;
            }
            float fill = progress;
            ringFill.GetPropertyBlock(block);
            block.SetFloat(FillId, Mathf.Clamp01(fill));
            ringFill.SetPropertyBlock(block);
        }

        // Every so often a band of light sweeps across the prop, left to right, while it is ready.
        private void SweepGleam(bool inviting)
        {
            if (gleam == null) return;

            float sweep = Mathf.Repeat(Time.time + attentionPhase, GleamEverySeconds) / GleamSeconds;
            gleam.enabled = inviting && sweep < 1f;
            if (!gleam.enabled) return;

            // From just off the left edge to just off the right, in the prop's own units.
            Vector2 size = prop.sprite.bounds.size;
            float across = Mathf.Lerp(-0.75f, 0.75f, Mathf.SmoothStep(0f, 1f, sweep)) * size.x;
            gleam.transform.localPosition = new Vector3(across, 0f, 0f);
        }

        // The band of light, seen only inside the prop's outline (a sprite mask of the prop itself).
        private static SpriteRenderer Gleam(SpriteRenderer prop)
        {
            if (prop == null || prop.sprite == null) return null;

            var mask = prop.gameObject.AddComponent<SpriteMask>();
            mask.sprite = prop.sprite;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = GleamOrder - 1;
            mask.frontSortingOrder = GleamOrder;

            SpriteRenderer band = Part(prop.transform, "Gleam", GleamSprite(), GleamOrder);
            band.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            band.color = GleamColour;
            Vector2 size = prop.sprite.bounds.size;
            float side = band.sprite.bounds.size.x;
            band.transform.localRotation = Quaternion.Euler(0f, 0f, -GleamSlant);
            band.transform.localScale = new Vector3(size.x * GleamWidth / side, Mathf.Max(size.x, size.y) * 1.6f / side, 1f);
            band.enabled = false;
            return band;
        }

        // A soft upright band, brightest down its middle.
        private static Sprite GleamSprite()
        {
            if (gleamSprite != null) return gleamSprite;

            var pixels = new Color32[GleamTextureSize * GleamTextureSize];
            for (int x = 0; x < GleamTextureSize; x++)
            {
                float across = Mathf.Abs((x + 0.5f) / GleamTextureSize * 2f - 1f);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Pow(1f - across, 1.5f) * 255f);
                for (int y = 0; y < GleamTextureSize; y++) pixels[y * GleamTextureSize + x] = new Color32(255, 255, 255, alpha);
            }

            var texture = new Texture2D(GleamTextureSize, GleamTextureSize, TextureFormat.RGBA32, false) { name = "Floor Switch Gleam", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            gleamSprite = Sprite.Create(texture, new Rect(0f, 0f, GleamTextureSize, GleamTextureSize), new Vector2(0.5f, 0.5f), 100f);
            return gleamSprite;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
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
    }
}
