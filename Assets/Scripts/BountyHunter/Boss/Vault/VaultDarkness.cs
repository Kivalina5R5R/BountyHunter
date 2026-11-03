using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The vault's blackout: while Dark, the floor goes dark but for a flashlight round every player
    // (flashlightRadius) and a lamp over each spot in Lamps (the light switches), and a flash lights
    // it all up for flashSeconds every few seconds (flashEvery). Drawn as a dark sheet over the floor
    // with holes cut by sprite masks, under the lasers and the diamond, which glow through it. Lit
    // says whether a point on the floor can be seen now.
    public sealed class VaultDarkness : MonoBehaviour
    {
        private const float Height = 0.06f;
        private const int CirclePixels = 128;
        private const float FadeSeconds = 0.25f;
        private static Sprite circle;
        private static Sprite square;

        private readonly List<SpriteMask> flashlights = new List<SpriteMask>();
        private readonly List<SpriteMask> lamps = new List<SpriteMask>();
        private readonly List<Vector3> lampSpots = new List<Vector3>();
        private UWBPlayerSpawner players;
        private SpriteRenderer sheet;
        private int order;
        private float flashlightRadius;
        private float lampRadius;
        private float flashSeconds;
        private Vector2 flashEvery;
        private float darkAlpha;
        private float shown;
        private float flashUntil;
        private float nextFlashAt;

        // Dark now (fading in and out over a moment).
        public bool Dark { get; private set; }

        // A flash is lighting the whole floor now.
        public bool Flashing => Dark && Time.time < flashUntil;

        // A sheet floorSize across at sortingOrder.
        public static VaultDarkness Create(Transform parent, Vector2 floorSize, int sortingOrder, UWBPlayerSpawner players, float flashlightRadius, float lampRadius,
            float flashSeconds, Vector2 flashEvery, float darkAlpha)
        {
            var holder = new GameObject("Blackout");
            holder.transform.SetParent(parent, false);
            var darkness = holder.AddComponent<VaultDarkness>();
            darkness.players = players;
            darkness.order = sortingOrder;
            darkness.flashlightRadius = flashlightRadius;
            darkness.lampRadius = lampRadius;
            darkness.flashSeconds = flashSeconds;
            darkness.flashEvery = flashEvery;
            darkness.darkAlpha = darkAlpha;

            var sheetObject = new GameObject("Dark", typeof(SpriteRenderer));
            sheetObject.transform.SetParent(holder.transform, false);
            sheetObject.transform.SetPositionAndRotation(new Vector3(0f, Height, 0f), Quaternion.Euler(90f, 0f, 0f));
            darkness.sheet = sheetObject.GetComponent<SpriteRenderer>();
            darkness.sheet.sprite = Square();
            darkness.sheet.sortingOrder = sortingOrder;
            darkness.sheet.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            darkness.sheet.color = Color.clear;
            // A little past the floor all round.
            sheetObject.transform.localScale = new Vector3(floorSize.x + 2f, floorSize.y + 2f, 1f);
            return darkness;
        }

        // Turns the lights off (true) or on again.
        public void SetDark(bool dark)
        {
            Dark = dark;
            flashUntil = 0f;
            nextFlashAt = Time.time + Random.Range(flashEvery.x, flashEvery.y);
        }

        // Spots that stay lit in the dark (the light switches), lampRadius round each.
        public void SetLamps(IReadOnlyList<Vector3> spots)
        {
            lampSpots.Clear();
            lampSpots.AddRange(spots);
        }

        // Whether a point on the floor can be seen now: always with the lights on or in a flash,
        // otherwise only in a flashlight or under a lamp.
        public bool Lit(Vector3 point)
        {
            if (!Dark || Flashing) return true;

            foreach (Vector3 spot in lampSpots)
            {
                if (Flat(point, spot) <= lampRadius) return true;
            }

            if (players == null) return false;

            foreach (UWBPlayerController player in players.SpawnedPlayers)
            {
                if (player != null && player.isActiveAndEnabled && Flat(point, player.transform.position) <= flashlightRadius) return true;
            }

            return false;
        }

        private void Update()
        {
            shown = Mathf.MoveTowards(shown, Dark ? 1f : 0f, Time.deltaTime / FadeSeconds);
            if (Dark && Time.time >= nextFlashAt)
            {
                flashUntil = Time.time + flashSeconds;
                nextFlashAt = Time.time + Random.Range(flashEvery.x, flashEvery.y);
            }

            float alpha = darkAlpha * shown * (Flashing ? 0f : 1f);
            sheet.color = new Color(0f, 0f, 0f, alpha);
            PlaceFlashlights();
            PlaceLamps();
        }

        private void PlaceFlashlights()
        {
            int count = 0;
            if (players != null)
            {
                foreach (UWBPlayerController player in players.SpawnedPlayers)
                {
                    if (player == null || !player.isActiveAndEnabled) continue;

                    SpriteMask light = Hole(flashlights, count++, flashlightRadius);
                    Vector3 at = player.transform.position;
                    light.transform.position = new Vector3(at.x, Height, at.z);
                }
            }

            for (int i = count; i < flashlights.Count; i++) flashlights[i].gameObject.SetActive(false);
        }

        private void PlaceLamps()
        {
            for (int i = 0; i < lampSpots.Count; i++)
            {
                SpriteMask lamp = Hole(lamps, i, lampRadius);
                lamp.transform.position = new Vector3(lampSpots[i].x, Height, lampSpots[i].z);
            }

            for (int i = lampSpots.Count; i < lamps.Count; i++) lamps[i].gameObject.SetActive(false);
        }

        // The index-th hole of a pool, radius round, made when missing; it cuts only the dark sheet.
        private SpriteMask Hole(List<SpriteMask> pool, int index, float radius)
        {
            while (pool.Count <= index)
            {
                var hole = new GameObject("Light", typeof(SpriteMask));
                hole.transform.SetParent(transform, false);
                hole.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var mask = hole.GetComponent<SpriteMask>();
                mask.sprite = Circle();
                mask.isCustomRangeActive = true;
                mask.backSortingOrder = order - 1;
                mask.frontSortingOrder = order;
                pool.Add(mask);
            }

            SpriteMask found = pool[index];
            found.gameObject.SetActive(true);
            found.transform.localScale = Vector3.one * (radius * 2f / found.sprite.bounds.size.x);
            return found;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        private static Sprite Square()
        {
            if (square != null) return square;

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Vault Dark" };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            square = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return square;
        }

        // A solid disc; the mask cuts where it is opaque.
        private static Sprite Circle()
        {
            if (circle != null) return circle;

            var pixels = new Color32[CirclePixels * CirclePixels];
            float centre = CirclePixels * 0.5f;
            for (int y = 0; y < CirclePixels; y++)
            {
                for (int x = 0; x < CirclePixels; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centre, centre));
                    pixels[y * CirclePixels + x] = new Color32(255, 255, 255, (byte)(distance <= centre - 1f ? 255 : 0));
                }
            }

            var texture = new Texture2D(CirclePixels, CirclePixels, TextureFormat.RGBA32, false) { name = "Vault Light" };
            texture.SetPixels32(pixels);
            texture.Apply();
            circle = Sprite.Create(texture, new Rect(0f, 0f, CirclePixels, CirclePixels), new Vector2(0.5f, 0.5f), CirclePixels);
            return circle;
        }
    }
}
