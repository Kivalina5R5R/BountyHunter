using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Splashes the police siren onto one wall character: a copy of the character's picture drawn
    // with the rim shader, lighting only its edge on the side facing the PoliceLight. Sits on a
    // child that covers the character; hidden until a PoliceLight shines on it.
    [RequireComponent(typeof(Image))]
    public sealed class PoliceLightRim : MonoBehaviour
    {
        private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");

        // The character picture whose edge gets lit.
        [SerializeField] private Image source;
        [SerializeField] private Shader rimShader;
        // How far the light reaches in from the edge, in texture pixels.
        [SerializeField, Min(1f)] private float rimWidth = 22f;

        private Image image;
        private Material material;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.raycastTarget = false;
            if (source != null) image.sprite = source.sprite;
            if (rimShader == null) return;

            material = new Material(rimShader) { name = $"{name} Police Rim" };
            material.SetFloat(RimWidthId, rimWidth);
            image.material = material;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        // Lights the edge facing lightPosition (world) in color; a clear color turns it off.
        public void Light(Vector3 lightPosition, Color color)
        {
            if (material == null) return;

            var rect = (RectTransform)transform;
            Vector2 toLight = (Vector2)transform.InverseTransformPoint(lightPosition) - rect.rect.center;
            if (toLight.sqrMagnitude > 0.0001f) material.SetVector(LightDirId, toLight.normalized);
            image.color = color;
        }
    }
}
