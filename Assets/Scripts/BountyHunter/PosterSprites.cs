using UnityEngine;

namespace BountyHunter.Thieves
{
    // Small sprites drawn in code, so they need no art files: a shaded red push pin head, a soft
    // round dot for specks, shadows and sparks, and a thin soft ring for shockwaves. Each is made
    // once and shared.
    public static class PosterSprites
    {
        private const int PinPixels = 64;
        private const int DotPixels = 32;
        private const int RingPixels = 128;
        private static readonly Color PinColor = new Color(0.8f, 0.13f, 0.11f, 1f);
        // Light falls from the top left, like the desk lamp in the wall art.
        private static readonly Vector3 LightDirection = new Vector3(-0.45f, 0.55f, 0.7f).normalized;

        private static Sprite pin;
        private static Sprite dot;
        private static Sprite ring;

        public static Sprite Pin => pin != null ? pin : pin = DrawPin();
        public static Sprite Dot => dot != null ? dot : dot = DrawDot();
        public static Sprite Ring => ring != null ? ring : ring = DrawRing();

        private static Sprite DrawPin()
        {
            var texture = NewTexture(PinPixels, "Poster Pin");
            var pixels = new Color[PinPixels * PinPixels];
            float radius = PinPixels * 0.5f - 2f;
            var centre = new Vector2(PinPixels * 0.5f, PinPixels * 0.5f);
            for (int y = 0; y < PinPixels; y++)
            {
                for (int x = 0; x < PinPixels; x++)
                {
                    Vector2 offset = (new Vector2(x + 0.5f, y + 0.5f) - centre) / radius;
                    float distance = offset.magnitude;
                    float alpha = Mathf.Clamp01((1f - distance) * radius + 0.5f);
                    if (alpha <= 0f)
                    {
                        pixels[y * PinPixels + x] = Color.clear;
                        continue;
                    }

                    // A dome: brighter toward the light, a darker rim and a small shine.
                    var normal = new Vector3(offset.x, offset.y, Mathf.Sqrt(Mathf.Max(0f, 1f - distance * distance)));
                    float light = Mathf.Clamp01(Vector3.Dot(normal, LightDirection));
                    float shine = Mathf.Pow(light, 24f) * 0.8f;
                    float rim = Mathf.SmoothStep(0.75f, 1f, distance) * 0.35f;
                    Color color = PinColor * (0.45f + 0.7f * light - rim) + Color.white * shine;
                    color.a = alpha;
                    pixels[y * PinPixels + x] = color;
                }
            }

            texture.SetPixels(pixels);
            return ToSprite(texture);
        }

        private static Sprite DrawDot()
        {
            var texture = NewTexture(DotPixels, "Poster Dot");
            var pixels = new Color[DotPixels * DotPixels];
            float radius = DotPixels * 0.5f;
            for (int y = 0; y < DotPixels; y++)
            {
                for (int x = 0; x < DotPixels; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    pixels[y * DotPixels + x] = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.55f, 1f, distance));
                }
            }

            texture.SetPixels(pixels);
            return ToSprite(texture);
        }

        // Brightest just inside the edge, fading softly to both sides.
        private static Sprite DrawRing()
        {
            var texture = NewTexture(RingPixels, "Shock Ring");
            var pixels = new Color[RingPixels * RingPixels];
            float radius = RingPixels * 0.5f;
            for (int y = 0; y < RingPixels; y++)
            {
                for (int x = 0; x < RingPixels; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float band = 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.88f) / 0.1f);
                    pixels[y * RingPixels + x] = new Color(1f, 1f, 1f, band * band * (3f - 2f * band));
                }
            }

            texture.SetPixels(pixels);
            return ToSprite(texture);
        }

        private static Texture2D NewTexture(int size, string textureName)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private static Sprite ToSprite(Texture2D texture)
        {
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
