using System;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The vault's laser beams: each runs right across the floor, from the top edge to the bottom
    // (Upright) or from the left edge to the right, and sweeps back and forth across the floor,
    // easing as it turns at the far edges, once every SweepSeconds (starting Phase of the way through
    // a sweep). They glow over the dark, so they show in a blackout too. A beam the boss touches
    // (HitBy) shakes for shakeSeconds, hardest where it was touched and less towards its ends, and
    // glows brighter and thicker meanwhile. Each beam is drawn as a row of short pieces so it can bend
    // as it shakes. Lies flat on the floor.
    public sealed class VaultLasers : MonoBehaviour
    {
        // One beam: upright (top to bottom, sweeping left and right) or lying (left to right,
        // sweeping up and down), how long one sweep there and back takes, and where in it it starts.
        [Serializable]
        public struct Beam
        {
            public bool Upright;
            public float SweepSeconds;
            [Range(0f, 1f)] public float Phase;

            public Beam(bool upright, float sweepSeconds, float phase)
            {
                Upright = upright;
                SweepSeconds = sweepSeconds;
                Phase = phase;
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
        }

        private Lit[] beams;
        private Vector2 areaMin;
        private Vector2 areaMax;
        // How far in from the edges a beam turns round.
        private float sweepInset;
        private float width;
        private Color colour;
        private float bornAt;

        // Beams across the floor (areaMin to areaMax, x and world z), width metres wide, turning
        // sweepInset in from the edges, drawn at sortingOrder (over the blackout's dark).
        public static VaultLasers Create(Transform parent, Vector2 areaMin, Vector2 areaMax, Beam[] beams, float sweepInset, float width, Color colour, int sortingOrder)
        {
            var holder = new GameObject("Lasers");
            holder.transform.SetParent(parent, false);
            var lasers = holder.AddComponent<VaultLasers>();
            lasers.areaMin = areaMin;
            lasers.areaMax = areaMax;
            lasers.sweepInset = sweepInset;
            lasers.width = width;
            lasers.colour = colour;
            lasers.bornAt = Time.time;
            lasers.beams = new Lit[beams.Length];
            for (int i = 0; i < beams.Length; i++)
            {
                var lit = new Lit { Beam = beams[i], Pieces = new SpriteRenderer[Pieces] };
                for (int p = 0; p < Pieces; p++) lit.Pieces[p] = lasers.Piece(sortingOrder);
                lasers.beams[i] = lit;
            }

            lasers.Show();
            return lasers;
        }

        // The beam within reach of point (on the floor), or -1; where along it (metres from its start)
        // comes back in along.
        public int HitBy(Vector3 point, float reach, out float along)
        {
            along = 0f;
            for (int i = 0; i < beams.Length; i++)
            {
                Beam beam = beams[i].Beam;
                float across = beam.Upright ? point.x : point.z;
                if (Mathf.Abs(across - Where(beam)) > reach) continue;

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
            Show();
        }

        private void Show()
        {
            foreach (Lit lit in beams)
            {
                Beam beam = lit.Beam;
                // Along the beam (its start at one edge) and across it, on the floor's x and z.
                Vector2 direction = beam.Upright ? Vector2.up : Vector2.right;
                Vector2 across = beam.Upright ? Vector2.right : Vector2.up;
                Vector2 start = beam.Upright ? new Vector2(Where(beam), areaMin.y - Overhang) : new Vector2(areaMin.x - Overhang, Where(beam));
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

        // Where the beam is across the floor now (x for an upright beam, world z for a lying one).
        private float Where(Beam beam)
        {
            float low = (beam.Upright ? areaMin.x : areaMin.y) + sweepInset;
            float high = (beam.Upright ? areaMax.x : areaMax.y) - sweepInset;
            float turn = (Time.time - bornAt) / Mathf.Max(0.1f, beam.SweepSeconds) + beam.Phase;
            // 0 at one edge, 1 at the other, easing as it turns.
            float share = 0.5f - 0.5f * Mathf.Cos(turn * Mathf.PI * 2f);
            return Mathf.Lerp(low, high, share);
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
