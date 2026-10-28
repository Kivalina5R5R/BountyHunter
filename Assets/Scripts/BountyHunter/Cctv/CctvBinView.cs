using UnityEngine;

namespace BountyHunter.Thieves
{
    // One bin on the floor in mini game 2, laid out in Assets/Prefabs/Cctv/Cctv Bin.prefab (move or
    // resize anything there; each bin in Cctv Floor.prefab may have its own picture): the ring a
    // player stands in to pick it, the bin itself, the thief that pops out of it at the reveal (with
    // the money bag over its head when it is the one) and the spotlight the reveal brings out. The thief, the bag and the spotlight are switched off in the
    // prefab. The prefab lies flat on the floor, its top toward the wall.
    public sealed class CctvBinView : MonoBehaviour
    {
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        [SerializeField] private SpriteRenderer ringFill;
        // Holds the bin's picture; it wobbles when picked and at the reveal.
        [SerializeField] private Transform figure;
        [SerializeField] private SpriteRenderer bin;
        // How tall the bin stands, in metres, whatever the size of its picture (only what is drawn of
        // it counts, not its clear edge).
        [SerializeField, Min(0.2f)] private float binHeight = 0.9f;

        [Header("Reveal")]
        [SerializeField] private Transform thief;
        [SerializeField] private SpriteRenderer thiefBody;
        [SerializeField, Min(0.2f)] private float thiefHeight = 1.1f;
        [SerializeField] private SpriteRenderer bag;
        [SerializeField] private SpriteRenderer spotlight;

        private MaterialPropertyBlock fillBlock;

        public Transform Figure => figure;
        public Transform Thief => thief;
        public SpriteRenderer Spotlight => spotlight;

        // Fits the bin's picture (set in the prefab) to binHeight and hides what the reveal brings out.
        public void Show()
        {
            if (bin.sprite != null) figure.localScale = Vector3.one * (binHeight / bin.sprite.bounds.size.y);
            if (thief != null) thief.gameObject.SetActive(false);
            if (spotlight != null) spotlight.gameObject.SetActive(false);
            SetProgress(0f);
            SetAlpha(1f);
        }

        // Dresses the thief that pops out at the reveal; hasBag puts the money bag over its head.
        public void Dress(Sprite body, bool hasBag)
        {
            if (thiefBody == null || body == null) return;

            thiefBody.sprite = body;
            thiefBody.transform.localScale = Vector3.one * (thiefHeight / body.bounds.size.y);
            if (bag != null) bag.gameObject.SetActive(hasBag);
        }

        // 0 to 1: how far the ring has filled.
        public void SetProgress(float progress)
        {
            if (ringFill == null) return;

            fillBlock ??= new MaterialPropertyBlock();
            ringFill.GetPropertyBlock(fillBlock);
            fillBlock.SetFloat(FillId, Mathf.Clamp01(progress));
            ringFill.SetPropertyBlock(fillBlock);
        }

        // Fades the bin and whatever came out of it.
        public void SetAlpha(float alpha)
        {
            SetAlpha(bin, alpha);
            SetAlpha(thiefBody, alpha);
            SetAlpha(bag, alpha);
        }

        private static void SetAlpha(SpriteRenderer picture, float alpha)
        {
            if (picture == null) return;

            Color color = picture.color;
            color.a = alpha;
            picture.color = color;
        }
    }
}
