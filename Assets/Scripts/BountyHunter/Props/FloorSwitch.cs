using System;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // Something on the floor a player sets off by standing on it (a sprinkler valve, a crate stack, a
    // switch): a ring round it in the look of the players' own floor rings (Tools/ParkArt) fills
    // while anyone stands within its radius and, full, it goes off
    // (Triggered, with the player nearest it) and rests for its cooldown. Stepping off does not start
    // it over: the ring drains slowly instead, since the tags never hold perfectly still. While it
    // rests the prop is dimmed and the ring shows the rest left in grey; ready again, it pops. It
    // answers the player too: a springy bounce with a twist as someone steps on, then while they stand
    // it lifts a little and rocks gently, more as the ring fills, and it settles back as they leave.
    // Its owner makes it and listens to it.
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

        private UWBPlayerSpawner players;
        private SpriteRenderer prop;
        private SpriteRenderer ringBack;
        private SpriteRenderer ringFill;
        private MaterialPropertyBlock block;
        private Color colour;
        private float radius;
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

        // A player stood on it long enough (the one nearest it).
        public event Action<FloorSwitch, UWBPlayerController> Triggered;

        public bool Resting => Time.time < restUntil;

        // ringBack is drawn as it is; ringFill is tinted colour and revealed round as it fills.
        public static FloorSwitch Create(Transform parent, string name, Vector3 at, Sprite propSprite, float propWidth, Sprite ringBack, Sprite ringFill,
            Material radialFill, Color colour, float radius, float holdSeconds, float cooldownSeconds, UWBPlayerSpawner players)
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
            floorSwitch.ringBack = Part(root.transform, "Ring Back", ringBack, RingOrder);
            floorSwitch.ringFill = Part(root.transform, "Ring Fill", ringFill, RingOrder);
            if (radialFill != null) floorSwitch.ringFill.sharedMaterial = radialFill;
            if (ringBack != null)
            {
                // The ring's outer edge on the switch's radius.
                Vector3 ringScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, ringBack.bounds.size.x));
                floorSwitch.ringBack.transform.localScale = ringScale;
                floorSwitch.ringFill.transform.localScale = ringScale;
            }

            floorSwitch.Show();
            return floorSwitch;
        }

        private void Update()
        {
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
            float pop = Mathf.Clamp01((Time.time - readyAt) / PopSeconds);
            float swell = resting ? 1f : 1f + 0.25f * Mathf.Sin(pop * Mathf.PI);
            float stepped = (Time.time - steppedOnAt) / StepSeconds;
            float bounce = stepped < 1f ? Mathf.Sin(stepped * Mathf.PI * 2.5f) * (1f - stepped) * (1f - stepped) : 0f;
            float eased = Mathf.SmoothStep(0f, 1f, rockAmount);
            float lift = HoldLift * eased * (0.4f + 0.6f * progress);
            float twist = StepTwist * bounce + HoldRock * eased * Mathf.Sin(rockPhase);
            if (prop != null)
            {
                prop.transform.localScale = Vector3.one * (propScale * (swell + StepBounce * bounce + lift));
                prop.transform.localRotation = Quaternion.Euler(0f, 0f, resting ? 0f : twist);
                prop.color = resting ? RestingColour : Color.white;
            }

            if (ringBack == null || ringFill == null || ringFill.sprite == null) return;

            ringBack.color = resting ? RestingColour : BackColour;
            ringFill.color = resting ? RestingColour : colour;
            float fill = resting ? (restUntil - Time.time) / Mathf.Max(0.01f, cooldownSeconds) : progress;
            ringFill.GetPropertyBlock(block);
            block.SetFloat(FillId, Mathf.Clamp01(fill));
            ringFill.SetPropertyBlock(block);
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
