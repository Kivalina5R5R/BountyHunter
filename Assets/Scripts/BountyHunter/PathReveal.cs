using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Draws a UI picture along its own path instead of all at once (the red streak of a mini game's
    // cut-in following its zigzag): Show(from, to) shows only the part of the path between those
    // shares of it, through the path reveal shader and the picture's path map
    // (Tools/TransitionArt/make_streak_path.py). The picture gets a material of its own. With the
    // path's middle line (from the same tool) it can also say where along it a share of the way lies
    // (TipAt), for sparks following the drawing's tip.
    [RequireComponent(typeof(Image))]
    public sealed class PathReveal : MonoBehaviour
    {
        private static readonly int PathMapId = Shader.PropertyToID("_PathMap");
        private static readonly int FromId = Shader.PropertyToID("_From");
        private static readonly int ToId = Shader.PropertyToID("_To");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int HeadGlowId = Shader.PropertyToID("_HeadGlow");

        [SerializeField] private Shader shader;
        // How far along the path each point of the picture lies (0 to 1).
        [SerializeField] private Texture2D pathMap;
        // The soft edge, as a share of the path, and how much brighter the drawing's tip is.
        [SerializeField, Range(0.001f, 0.2f)] private float softness = 0.015f;
        [SerializeField, Range(0f, 2f)] private float headGlow = 0.6f;
        // Points along the path's middle: x and y as shares of the picture (x to the right, y up) and
        // z how far along the path each lies (0 to 1, rising); may be empty.
        [SerializeField] private Vector3[] middle = System.Array.Empty<Vector3>();

        private Material material;

        // Shows the path from from to to (shares of it, 0 at its start): Show(0, t) draws it on, and
        // Show(t, 1) wipes it off from its start; a from past 1 leaves nothing at all.
        public void Show(float from, float to)
        {
            if (!MakeMaterial()) return;

            // A little past either end, so the soft edge can leave the path wholly.
            material.SetFloat(FromId, Mathf.Clamp(from, 0f, 1.2f));
            material.SetFloat(ToId, Mathf.Clamp(to, 0f, 1.2f));
        }

        // Where on the wall the point share of the way along the path lies; false without a middle line.
        public bool TipAt(float share, out Vector3 world)
        {
            world = transform.position;
            if (middle == null || middle.Length < 2) return false;

            share = Mathf.Clamp01(share);
            int next = 1;
            while (next < middle.Length - 1 && middle[next].z < share) next++;
            Vector3 before = middle[next - 1];
            Vector3 after = middle[next];
            float between = after.z > before.z ? Mathf.InverseLerp(before.z, after.z, share) : 0f;
            Vector2 point = Vector2.Lerp(before, after, between);

            Rect area = ((RectTransform)transform).rect;
            world = transform.TransformPoint(new Vector3(area.xMin + point.x * area.width, area.yMin + point.y * area.height, 0f));
            return true;
        }

        private bool MakeMaterial()
        {
            if (material != null) return true;
            if (shader == null || pathMap == null) return false;

            material = new Material(shader) { name = $"{name} Path Reveal" };
            material.SetTexture(PathMapId, pathMap);
            material.SetFloat(SoftnessId, softness);
            material.SetFloat(HeadGlowId, headGlow);
            GetComponent<Image>().material = material;
            return true;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
