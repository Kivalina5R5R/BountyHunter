using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // Shows another picture on a RawImage for a while (the boss's rooftop on the floor and the wall)
    // and puts the one it had back afterwards. The image's AspectRatioFitter, if any, follows the
    // picture's shape.
    public sealed class PictureSwap
    {
        private readonly RawImage image;
        private readonly Texture picture;
        private Texture before;
        private bool swapped;

        // Either may be null: then nothing changes.
        public PictureSwap(RawImage image, Texture picture)
        {
            this.image = image;
            this.picture = picture;
        }

        public void Show()
        {
            if (image == null || picture == null || swapped) return;

            before = image.texture;
            swapped = true;
            SetPicture(picture);
        }

        public void PutBack()
        {
            if (!swapped) return;

            swapped = false;
            if (image != null && before != null) SetPicture(before);
        }

        private void SetPicture(Texture texture)
        {
            image.texture = texture;
            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = (float)texture.width / texture.height;
        }
    }
}
