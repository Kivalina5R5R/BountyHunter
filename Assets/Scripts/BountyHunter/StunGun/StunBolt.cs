using UnityEngine;

namespace BountyHunter.Thieves
{
    // A stun gun's bolt: a jagged streak of electricity from the shooter to what it hits, lying just
    // over the floor, re-jagged every few hundredths of a second so it crackles, and gone in a blink.
    // Removes itself.
    public sealed class StunBolt : MonoBehaviour
    {
        private const float Height = 0.08f;
        private const int Points = 12;
        private const float Seconds = 0.22f;
        private const float RejagSeconds = 0.035f;
        // How far the zigzag strays either side of the straight line (metres), and the line's width.
        private const float Jag = 0.12f;
        private const float Width = 0.07f;
        // Over everything on the floor.
        private const int Order = 30;

        private static Material material;

        private LineRenderer line;
        private Vector3 from;
        private Vector3 to;
        private Color colour;
        private float age;
        private float rejagAt;

        public static void Play(Vector3 from, Vector3 to, Color colour, Transform parent)
        {
            var holder = new GameObject("Stun Bolt");
            holder.transform.SetParent(parent, false);
            var bolt = holder.AddComponent<StunBolt>();
            bolt.from = new Vector3(from.x, Height, from.z);
            bolt.to = new Vector3(to.x, Height, to.z);
            bolt.colour = colour;
            bolt.line = holder.AddComponent<LineRenderer>();
            bolt.line.useWorldSpace = true;
            bolt.line.positionCount = Points;
            bolt.line.widthMultiplier = Width;
            bolt.line.numCapVertices = 2;
            bolt.line.alignment = LineAlignment.TransformZ;
            holder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            bolt.line.sharedMaterial = Material();
            bolt.line.sortingOrder = Order;
            bolt.Jagged();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            if (age >= rejagAt) Jagged();
            float fade = 1f - age / Seconds;
            Color start = Color.Lerp(colour, Color.white, 0.5f);
            start.a = fade;
            Color end = colour;
            end.a = fade;
            line.startColor = start;
            line.endColor = end;
        }

        // A fresh zigzag between the ends, straying most in the middle.
        private void Jagged()
        {
            rejagAt = age + RejagSeconds;
            Vector3 across = Vector3.Cross(to - from, Vector3.up).normalized;
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                float stray = i == 0 || i == Points - 1 ? 0f : Random.Range(-Jag, Jag) * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector3.Lerp(from, to, t) + across * stray);
            }
        }

        private static Material Material()
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Stun Bolt" };
            return material;
        }
    }
}
