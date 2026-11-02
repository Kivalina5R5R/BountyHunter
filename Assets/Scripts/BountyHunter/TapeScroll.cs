using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Slides what is printed on a straight tape (the STOPs, the stripes) along it to the right without
    // end, through the tape scroll shader: the picture's content moves speed pixels a second along the
    // tape and loops every repeat of its pattern, so no seam shows. The way along and the repeat come
    // from Tools/TransitionArt/measure_tapes.py. The picture gets a material of its own. Runs on
    // unscaled time.
    [RequireComponent(typeof(Image))]
    public sealed class TapeScroll : MonoBehaviour
    {
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int AlongId = Shader.PropertyToID("_Along");
        private static readonly int PeriodId = Shader.PropertyToID("_Period");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");

        [SerializeField] private Shader shader;
        // The size of the picture the measures below are in (pixels); zero takes the texture's own.
        [SerializeField] private Vector2 pictureSize;
        // A unit step along the tape in picture pixels (x to the right, y up), pointing right.
        [SerializeField] private Vector2 along = Vector2.right;
        // How far apart (picture pixels) the pattern repeats along the tape.
        [SerializeField, Min(1f)] private float period = 1000f;
        // Picture pixels a second; below zero slides it to the left.
        [SerializeField] private float speed = 600f;

        private Material material;
        private float offset;

        private void OnEnable()
        {
            MakeMaterial();
        }

        private void Update()
        {
            if (material == null) return;

            offset = Mathf.Repeat(offset + speed * Time.unscaledDeltaTime, period);
            material.SetFloat(OffsetId, offset);
        }

        private void MakeMaterial()
        {
            if (material != null || shader == null) return;

            var picture = GetComponent<Image>();
            if (picture.sprite == null) return;

            Texture texture = picture.sprite.texture;
            Vector2 size = pictureSize.x > 0f && pictureSize.y > 0f ? pictureSize : new Vector2(texture.width, texture.height);
            material = new Material(shader) { name = $"{name} Tape Scroll" };
            material.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
            material.SetVector(AlongId, along.normalized);
            material.SetFloat(PeriodId, period);
            picture.material = material;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
