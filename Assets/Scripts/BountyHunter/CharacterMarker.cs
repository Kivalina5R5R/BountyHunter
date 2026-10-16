using System.Collections;
using Fortal.Display;
using Fortal.Players;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Shows on a player's floor marker which character they picked: the plain tag circle hides and the
    // character's ring (its face on top) pops up in its place, centred where the player stands.
    // Added to the player when they pick; stays for the rest of the game.
    public sealed class CharacterMarker : MonoBehaviour
    {
        // The ring art is drawn a little bigger than the plain circle, so the face clears it.
        private const float SizeVsCircle = 1.3f;
        private const float PopSeconds = 0.35f;

        private Graphic circle;
        private Image ring;

        public static void Show(UWBPlayerController player, Sprite art)
        {
            if (player == null || art == null) return;

            CharacterMarker marker = player.GetComponent<CharacterMarker>();
            if (marker == null) marker = player.gameObject.AddComponent<CharacterMarker>();
            marker.Use(art);
        }

        private void Use(Sprite art)
        {
            if (circle == null) circle = GetComponentInChildren<TagCircle>(true);
            if (circle == null) return;

            if (ring == null) ring = CreateRing();
            ring.sprite = art;
            circle.enabled = false;
            if (isActiveAndEnabled) StartCoroutine(Pop());
        }

        private Image CreateRing()
        {
            RectTransform tag = circle.rectTransform;
            var ringObject = new GameObject("Character Ring", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)ringObject.transform;
            rect.SetParent(tag.parent, false);
            rect.SetSiblingIndex(tag.GetSiblingIndex() + 1);
            rect.anchorMin = tag.anchorMin;
            rect.anchorMax = tag.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = tag.anchoredPosition;
            rect.sizeDelta = tag.sizeDelta * SizeVsCircle;
            var image = ringObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        // Springs up from nothing with a little overshoot.
        private IEnumerator Pop()
        {
            Transform rect = ring.transform;
            for (float time = 0f; time < PopSeconds; time += Time.deltaTime)
            {
                float t = time / PopSeconds;
                const float s = 2.2f;
                float u = t - 1f;
                rect.localScale = Vector3.one * (1f + u * u * ((s + 1f) * u + s));
                yield return null;
            }

            rect.localScale = Vector3.one;
        }
    }
}
