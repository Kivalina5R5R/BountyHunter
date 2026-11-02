using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The shots a player's stun gun has left: one small lightning bolt each, on the bottom edge of
    // the player's character ring (opposite its face on top), stacked on top of each other but each
    // shifted a little to the side and up, all upright, so they can be counted. A new one pops in;
    // one used is gone at once. StunGuns keeps it up to date.
    public sealed class StunShotIcons : MonoBehaviour
    {
        // The character ring is drawn this much wider than the plain tag circle (CharacterMarker).
        private const float RingVsCircle = 1.3f;
        private const float IconHeight = 0.17f;
        // How far each bolt sits from the one under it: to the side and up, as shares of its height,
        // so they overlap but each still shows.
        private const float StepAcross = 0.22f;
        private const float StepUp = 0.06f;
        private const float PopSeconds = 0.25f;
        private const float Height = 0.06f;
        // Over the player's marker (20).
        private const int Order = 25;

        private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
        private readonly List<float> poppedAt = new List<float>();
        private Sprite bolt;
        private float scale;
        private float ringRadius;

        public static StunShotIcons Create(Transform parent, Sprite bolt, UWBPlayerController player)
        {
            var holder = new GameObject($"Stun Shots {player.PlayerId}");
            holder.transform.SetParent(parent, false);
            var shots = holder.AddComponent<StunShotIcons>();
            shots.bolt = bolt;
            shots.scale = IconHeight / Mathf.Max(0.01f, bolt.bounds.size.y);
            shots.ringRadius = PlayerMarker.Width(player) * RingVsCircle * 0.5f;
            return shots;
        }

        // Under the player standing at at, one bolt per shot.
        public void Show(Vector3 at, int shots)
        {
            while (icons.Count < shots) Add();
            while (icons.Count > shots) RemoveLast();

            for (int i = 0; i < icons.Count; i++)
            {
                float shift = i - (icons.Count - 1) * 0.5f;
                var offset = new Vector3(shift * StepAcross, 0f, shift * StepUp) * IconHeight;
                Transform icon = icons[i].transform;
                icon.SetPositionAndRotation(new Vector3(at.x, Height, at.z - ringRadius) + offset, Quaternion.Euler(90f, 0f, 0f));
                float pop = Mathf.Clamp01((Time.time - poppedAt[i]) / PopSeconds);
                icon.localScale = Vector3.one * (scale * EaseOutBack(pop));
            }
        }

        private void Add()
        {
            var part = new GameObject("Shot", typeof(SpriteRenderer));
            part.transform.SetParent(transform, false);
            var picture = part.GetComponent<SpriteRenderer>();
            picture.sprite = bolt;
            // Each later bolt over the one before.
            picture.sortingOrder = Order + icons.Count;
            icons.Add(picture);
            poppedAt.Add(Time.time);
        }

        private void RemoveLast()
        {
            int last = icons.Count - 1;
            Destroy(icons[last].gameObject);
            icons.RemoveAt(last);
            poppedAt.RemoveAt(last);
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
