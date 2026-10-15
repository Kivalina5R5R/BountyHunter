using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The police car's light bar outside the window: a red and a blue lamp that swell and fade in
    // turn on smooth waves, each with a soft glow at the lamp. The mixed light reaches the edges of
    // every wall character facing it (PoliceLightRim), less so further away. Move this object to
    // aim the light.
    public sealed class PoliceLight : MonoBehaviour
    {
        [SerializeField] private Image redGlow;
        [SerializeField] private Image blueGlow;
        [SerializeField] private Color red = new Color(1f, 0.16f, 0.12f, 1f);
        [SerializeField] private Color blue = new Color(0.2f, 0.45f, 1f, 1f);
        // Full red-and-blue rounds per second.
        [SerializeField, Min(0.05f)] private float roundsPerSecond = 0.8f;
        // Higher keeps each lamp dark longer between swells; 1 is a plain, even wave.
        [SerializeField, Range(1f, 6f)] private float pulseSharpness = 2.5f;
        [SerializeField, Range(0f, 1f)] private float strength = 0.9f;
        // Lamp glows swell by this much at their brightest.
        [SerializeField, Range(0f, 1f)] private float glowSwell = 0.3f;
        // Beyond this world distance the light on characters is at its weakest.
        [SerializeField, Min(0.01f)] private float reach = 10f;
        // Share of the light still reaching the furthest characters.
        [SerializeField, Range(0f, 1f)] private float farStrength = 0.35f;

        private PoliceLightRim[] rims;

        private void Start()
        {
            rims = FindObjectsByType<PoliceLightRim>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            float wave = Time.time * roundsPerSecond * Mathf.PI * 2f;
            float redLevel = Pulse(wave);
            float blueLevel = Pulse(wave + Mathf.PI);
            ShowGlow(redGlow, red, redLevel);
            ShowGlow(blueGlow, blue, blueLevel);

            // Both lamps add up, so the light passes smoothly through purple instead of snapping.
            Color mixed = red * redLevel + blue * blueLevel;
            Vector3 position = transform.position;
            foreach (PoliceLightRim rim in rims)
            {
                if (rim == null) continue;

                float distance = Vector3.Distance(position, rim.transform.position);
                Color color = mixed;
                color.a = strength * Mathf.Lerp(1f, farStrength, Mathf.Clamp01(distance / reach));
                rim.Light(position, color);
            }
        }

        // 0..1 on a smooth wave that peaks once per round.
        private float Pulse(float wave)
        {
            return Mathf.Pow(0.5f + 0.5f * Mathf.Cos(wave), pulseSharpness);
        }

        private void ShowGlow(Image glow, Color color, float level)
        {
            if (glow == null) return;

            color.a = strength * level;
            glow.color = color;
            glow.transform.localScale = Vector3.one * (1f + glowSwell * level);
        }
    }
}
