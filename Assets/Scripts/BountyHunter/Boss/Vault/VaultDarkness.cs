using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The vault's blackout: while Dark, the floor goes dark (a sheet over it, drawn with the Vault
    // Darkness material). Each player carries a faint glow (flashlightRadius, lifting flashlightLift
    // of the dark), just enough to see themselves; each spot in Lamps (the light switches) is lit
    // brightly out to lampRadius; both fade softly at their edges. It stays dark throughout (no
    // flashes). Lit says whether a point on the floor can be seen now (a player's glow counts).
    public sealed class VaultDarkness : MonoBehaviour
    {
        private const float Height = 0.06f;
        private const float FadeSeconds = 0.25f;
        // The shader takes this many lights.
        private const int MostLights = 16;
        private static readonly int LightsId = Shader.PropertyToID("_Lights");
        private static readonly int LightCountId = Shader.PropertyToID("_LightCount");
        private static Sprite square;

        private readonly List<Vector3> lampSpots = new List<Vector3>();
        private readonly Vector4[] lights = new Vector4[MostLights];
        private UWBPlayerSpawner players;
        private SpriteRenderer sheet;
        private MaterialPropertyBlock block;
        private float flashlightRadius;
        private float flashlightLift;
        private float lampRadius;
        private float darkAlpha;
        private float shown;

        // Dark now (fading in and out over a moment).
        public bool Dark { get; private set; }

        // A sheet floorSize across at sortingOrder, drawn with darkness (the Vault Darkness material).
        public static VaultDarkness Create(Transform parent, Vector2 floorSize, int sortingOrder, Material darkness, UWBPlayerSpawner players, float flashlightRadius,
            float flashlightLift, float lampRadius, float darkAlpha)
        {
            var holder = new GameObject("Blackout");
            holder.transform.SetParent(parent, false);
            var dark = holder.AddComponent<VaultDarkness>();
            dark.players = players;
            dark.flashlightRadius = flashlightRadius;
            dark.flashlightLift = flashlightLift;
            dark.lampRadius = lampRadius;
            dark.darkAlpha = darkAlpha;
            dark.block = new MaterialPropertyBlock();

            var sheetObject = new GameObject("Dark", typeof(SpriteRenderer));
            sheetObject.transform.SetParent(holder.transform, false);
            sheetObject.transform.SetPositionAndRotation(new Vector3(0f, Height, 0f), Quaternion.Euler(90f, 0f, 0f));
            dark.sheet = sheetObject.GetComponent<SpriteRenderer>();
            dark.sheet.sprite = Square();
            dark.sheet.sortingOrder = sortingOrder;
            if (darkness != null) dark.sheet.sharedMaterial = darkness;
            dark.sheet.color = Color.clear;
            // A little past the floor all round.
            sheetObject.transform.localScale = new Vector3(floorSize.x + 2f, floorSize.y + 2f, 1f);
            return dark;
        }

        // Turns the lights off (true) or on again.
        public void SetDark(bool dark)
        {
            Dark = dark;
        }

        // Spots that stay lit in the dark (the light switches).
        public void SetLamps(IReadOnlyList<Vector3> spots)
        {
            lampSpots.Clear();
            lampSpots.AddRange(spots);
        }

        // Whether a point on the floor can be seen now: always with the lights on, otherwise only in
        // a player's glow or under a lamp.
        public bool Lit(Vector3 point)
        {
            if (!Dark) return true;

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
            sheet.color = new Color(0f, 0f, 0f, darkAlpha * shown);
            PlaceLights();
        }

        // The lamps first (they must never be left out), then the players' glows, as many as fit.
        private void PlaceLights()
        {
            int count = 0;
            foreach (Vector3 spot in lampSpots)
            {
                if (count >= MostLights) break;
                lights[count++] = new Vector4(spot.x, spot.z, lampRadius, 1f);
            }

            if (players != null)
            {
                foreach (UWBPlayerController player in players.SpawnedPlayers)
                {
                    if (count >= MostLights) break;
                    if (player == null || !player.isActiveAndEnabled) continue;

                    Vector3 at = player.transform.position;
                    lights[count++] = new Vector4(at.x, at.z, flashlightRadius, flashlightLift);
                }
            }

            sheet.GetPropertyBlock(block);
            block.SetVectorArray(LightsId, lights);
            block.SetFloat(LightCountId, count);
            sheet.SetPropertyBlock(block);
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
    }
}
