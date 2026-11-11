using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // A RawImage that fills a box wider than its picture without zooming it: the picture is drawn as
    // tall as the box and the middle keeps its true shape, while only the outer edgeShare of each end
    // is pulled out to the box's sides. The pull grows smoothly toward the very end (none where the
    // middle stops), so there is no visible bend. A box no wider than the picture shows it centred as is.
    public sealed class EdgeStretchImage : RawImage
    {
        // Strips per end; enough for the smooth pull to look like a curve.
        private const int Strips = 24;

        private float pictureAspect = 1f;
        private float edgeShare = 0.1f;

        // aspect: the picture's width against its height. share: how much of each end (0 to 0.45) may stretch.
        public void Configure(float aspect, float share)
        {
            pictureAspect = Mathf.Max(0.01f, aspect);
            edgeShare = Mathf.Clamp(share, 0.01f, 0.45f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect box = GetPixelAdjustedRect();
            float natural = box.height * pictureAspect;
            float left = box.center.x - natural / 2f;

            // Room each end must reach, and how wide that end is in the picture.
            float reach = (box.width - (1f - 2f * edgeShare) * natural) / 2f;
            float edge = edgeShare * natural;
            if (reach <= edge)
            {
                AddStrip(vh, left, left + natural, 0f, 1f, box);
                return;
            }

            float middleLeft = box.xMin + reach;
            float middleRight = box.xMax - reach;
            AddStrip(vh, middleLeft, middleRight, edgeShare, 1f - edgeShare, box);
            for (int i = 0; i < Strips; i++)
            {
                float t0 = i / (float)Strips;
                float t1 = (i + 1) / (float)Strips;
                float d0 = Pull(t0, edge, reach);
                float d1 = Pull(t1, edge, reach);
                AddStrip(vh, middleLeft - d1, middleLeft - d0, edgeShare * (1f - t1), edgeShare * (1f - t0), box);
                AddStrip(vh, middleRight + d0, middleRight + d1, 1f - edgeShare * (1f - t0), 1f - edgeShare * (1f - t1), box);
            }
        }

        // How far out from the middle a point t (0 at the middle's end, 1 at the picture's end) lands:
        // unstretched at t = 0, reaching the box side at t = 1.
        private static float Pull(float t, float edge, float reach)
        {
            return edge * t + (reach - edge) * t * t;
        }

        // A quad from x0 to x1, showing the picture from u0 to u1 (0 to 1 across the picture).
        private void AddStrip(VertexHelper vh, float x0, float x1, float u0, float u1, Rect box)
        {
            Rect uv = uvRect;
            float a = uv.xMin + u0 * uv.width;
            float b = uv.xMin + u1 * uv.width;
            Color32 tint = color;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, box.yMin), tint, new Vector2(a, uv.yMin));
            vh.AddVert(new Vector3(x0, box.yMax), tint, new Vector2(a, uv.yMax));
            vh.AddVert(new Vector3(x1, box.yMax), tint, new Vector2(b, uv.yMax));
            vh.AddVert(new Vector3(x1, box.yMin), tint, new Vector2(b, uv.yMin));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
