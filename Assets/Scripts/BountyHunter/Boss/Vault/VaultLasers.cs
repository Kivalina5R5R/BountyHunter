using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BountyHunter.Thieves
{
    // The vault's laser beams: each runs right across the floor, from the top edge to the bottom
    // (Upright) or from the left edge to the right, and passes over the floor once: it comes in past
    // one edge (either, at random), crosses at an even pace in CrossSeconds and goes out past the
    // other, then stays away for WaitSeconds before the next pass (the first after StartAfter).
    // Hidden (SetShown false, as in a blackout) no beam shows or can be touched, and each comes back
    // only from an edge once shown again. A beam the boss touches (HitBy) shakes for shakeSeconds,
    // hardest where it was touched and less towards its ends, and glows brighter and thicker
    // meanwhile. Each beam is drawn as a row of short pieces so it can bend as it shakes. Lies flat
    // on the floor.
    public sealed class VaultLasers : MonoBehaviour
    {
        // One beam: upright (top to bottom, passing left or right) or lying (left to right, passing
        // up or down), how long one pass across takes, how long it stays away after, and how long
        // before its first pass.
        [Serializable]
        public struct Beam
        {
            public bool Upright;
            public float CrossSeconds;
            public float WaitSeconds;
            public float StartAfter;

            public Beam(bool upright, float crossSeconds, float waitSeconds, float startAfter)
            {
                Upright = upright;
                CrossSeconds = crossSeconds;
                WaitSeconds = waitSeconds;
                StartAfter = startAfter;
            }
        }

        private const float Height = 0.05f;
        private const int Pieces = 16;
        private const int TexturePixels = 32;
        private const float ShakeSeconds = 0.8f;
        // How far a shaking beam bends at the touch (metres), and how far along it the bend reaches.
        private const float ShakeAmount = 0.12f;
        private const float ShakeReach = 1.6f;
        // How far past the floor's edges a beam runs, so its ends never show.
        private const float Overhang = 0.3f;
        private static Sprite beamSprite;

        private sealed class Lit
        {
            public Beam Beam;
            public SpriteRenderer[] Pieces;
            public float ShookAt = float.MinValue;
            public float ShakeAlong;
            // Crossing now: since PassStart, from the low edge (left or bottom) when FromLow.
            public bool Crossing;
            public float PassStart;
            public bool FromLow;
            public float NextAt;
        }

        private Lit[] beams;
        private Vector2 areaMin;
        private Vector2 areaMax;
        private float width;
        private Color colour;
        private bool shown = true;

        // Beams across the floor (areaMin to areaMax, x and world z), width metres wide, drawn at
        // sortingOrder.
        public static VaultLasers Create(Transform parent, Vector2 areaMin, Vector2 areaMax, Beam[] beams, float width, Color colour, int sortingOrder)
        {
            var holder = new GameObject("Lasers");
            holder.transform.SetParent(parent, false);
            var lasers = holder.AddComponent<VaultLasers>();
            lasers.areaMin = areaMin;
            lasers.areaMax = areaMax;
            lasers.width = width;
            lasers.colour = colour;
            lasers.beams = new Lit[beams.Length];
            for (int i = 0; i < beams.Length; i++)
            {
                var lit = new Lit { Beam = beams[i], Pieces = new SpriteRenderer[Pieces], NextAt = Time.time + beams[i].StartAfter };
                for (int p = 0; p < Pieces; p++) lit.Pieces[p] = lasers.Piece(sortingOrder);
                lasers.beams[i] = lit;
            }

            lasers.Show();
            return lasers;
        }

        // Shows the beams (true) or takes them all away at once (false); shown again, each comes
        // back in from an edge after a short wait.
        public void SetShown(bool show)
        {
            if (show == shown) return;

            shown = show;
            foreach (Lit lit in beams)
            {
                lit.Crossing = false;
                if (show) lit.NextAt = Time.time + lit.Beam.WaitSeconds * Random.Range(0.2f, 0.6f);
            }

            Show();
        }

        // The beam within reach of point (on the floor), or -1; where along it (metres from its start)
        // comes back in along.
        public int HitBy(Vector3 point, float reach, out float along)
        {
            along = 0f;
            for (int i = 0; i < beams.Length; i++)
            {
                Lit lit = beams[i];
                if (!lit.Crossing) continue;

                Beam beam = lit.Beam;
                float across = beam.Upright ? point.x : point.z;
                if (Mathf.Abs(across - Where(lit)) > reach) continue;

                along = beam.Upright ? point.z - (areaMin.y - Overhang) : point.x - (areaMin.x - Overhang);
                return i;
            }

            return -1;
        }

        // Shakes beam index from the spot along it (metres from its start).
        public void Shake(int index, float along)
        {
            if (index < 0 || index >= beams.Length) return;

            beams[index].ShookAt = Time.time;
            beams[index].ShakeAlong = along;
        }

        private void Update()
        {
            if (shown)
            {
                foreach (Lit lit in beams) Pass(lit);
            }

            Show();
        }

        // Starts a pass when its wait is over, and ends it once it is out past the far edge.
        private void Pass(Lit lit)
        {
            if (!lit.Crossing)
            {
                if (Time.time < lit.NextAt) return;

                lit.Crossing = true;
                lit.PassStart = Time.time;
                lit.FromLow = Random.value < 0.5f;
                return;
            }

            if (Progress(lit) < 1f) return;

            lit.Crossing = false;
            lit.NextAt = Time.time + lit.Beam.WaitSeconds;
        }

        private void Show()
        {
            foreach (Lit lit in beams)
            {
                bool on = shown && lit.Crossing;
                foreach (SpriteRenderer piece in lit.Pieces) piece.enabled = on;
                if (!on) continue;

                Beam beam = lit.Beam;
                // Along the beam (its start at one edge) and across it, on the floor's x and z.
                Vector2 direction = beam.Upright ? Vector2.up : Vector2.right;
                Vector2 across = beam.Upright ? Vector2.right : Vector2.up;
                Vector2 start = beam.Upright ? new Vector2(Where(lit), areaMin.y - Overhang) : new Vector2(areaMin.x - Overhang, Where(lit));
                float length = (beam.Upright ? areaMax.y - areaMin.y : areaMax.x - areaMin.x) + Overhang * 2f;
                float pieceLength = length / Pieces;
                float shook = (Time.time - lit.ShookAt) / ShakeSeconds;
                float shaking = shook < 1f ? 1f - shook : 0f;
                float angle = beam.Upright ? 90f : 0f;
                Color glow = Color.Lerp(colour, Color.white, 0.45f * shaking);
                for (int p = 0; p < Pieces; p++)
                {
                    float along = (p + 0.5f) * pieceLength;
                    float near = Mathf.Clamp01(1f - Mathf.Abs(along - lit.ShakeAlong) / ShakeReach);
                    float bend = ShakeAmount * shaking * near * Mathf.Sin(Time.time * 45f + p * 1.3f);
                    Vector2 middle = start + direction * along + across * bend;
                    SpriteRenderer piece = lit.Pieces[p];
                    piece.transform.SetPositionAndRotation(new Vector3(middle.x, Height, middle.y),
                        Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angle));
                    float thick = width * (1f + 1.2f * shaking * near);
                    float size = piece.sprite.bounds.size.x;
                    piece.transform.localScale = new Vector3(pieceLength * 1.04f / size, thick / size, 1f);
                    piece.color = glow;
                }
            }
        }

        // How far through its pass the beam is: 0 just out past its first edge, 1 out past the other.
        private static float Progress(Lit lit)
        {
            return (Time.time - lit.PassStart) / Mathf.Max(0.1f, lit.Beam.CrossSeconds);
        }

        // Where the beam is across the floor now (x for an upright beam, world z for a lying one),
        // moving at an even pace from just past one edge to just past the other.
        private float Where(Lit lit)
        {
            Beam beam = lit.Beam;
            float low = (beam.Upright ? areaMin.x : areaMin.y) - width;
            float high = (beam.Upright ? areaMax.x : areaMax.y) + width;
            float share = Mathf.Clamp01(Progress(lit));
            return lit.FromLow ? Mathf.Lerp(low, high, share) : Mathf.Lerp(high, low, share);
        }

        private SpriteRenderer Piece(int order)
        {
            var piece = new GameObject("Beam", typeof(SpriteRenderer));
            piece.transform.SetParent(transform, false);
            var picture = piece.GetComponent<SpriteRenderer>();
            picture.sprite = BeamSprite();
            picture.sortingOrder = order;
            return picture;
        }

        // A square glowing brightest along its middle line, fading to its top and bottom edges.
        private static Sprite BeamSprite()
        {
            if (beamSprite != null) return beamSprite;

            var pixels = new Color32[TexturePixels * TexturePixels];
            for (int y = 0; y < TexturePixels; y++)
            {
                float across = Mathf.Abs((y + 0.5f) / TexturePixels * 2f - 1f);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Pow(1f - across, 1.6f) * 255f);
                for (int x = 0; x < TexturePixels; x++) pixels[y * TexturePixels + x] = new Color32(255, 255, 255, alpha);
            }

            var texture = new Texture2D(TexturePixels, TexturePixels, TextureFormat.RGBA32, false) { name = "Vault Laser", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            beamSprite = Sprite.Create(texture, new Rect(0f, 0f, TexturePixels, TexturePixels), new Vector2(0.5f, 0.5f), TexturePixels);
            return beamSprite;
        }
    }
}
