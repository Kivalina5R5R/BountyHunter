using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace BountyHunter.Thieves
{
    // One suspect in the lineup, laid out in Assets/Prefabs/Lineup/Lineup Suspect.prefab (move or
    // resize anything there): the ring a player stands in to lock in, the suspect itself, the row
    // over its head showing what it carries (up to three of a hat, the bandit mask, glasses, a face
    // mask and the money bag, each fitted into an itemSize square, side by side on a cream strip like
    // the clue cards), and the spotlight the reveal brings out (switched off in the prefab). LineupFloor fills it in for each case. The prefab lies flat on the floor, its top
    // toward the wall.
    public sealed class LineupSuspectView : MonoBehaviour
    {
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        [SerializeField] private SpriteRenderer ringFill;
        // Holds the body; it breathes and sways, and swells at the reveal.
        [SerializeField] private Transform figure;
        [SerializeField] private SpriteRenderer body;
        // How tall every species stands, in metres, whatever the size of its picture.
        [SerializeField, Min(0.2f)] private float bodyHeight = 1.35f;

        [Header("What it carries, in a row over its head")]
        [SerializeField] private Transform items;
        // A sliced sprite behind the row, sized to fit what is in it; hidden when the row is empty.
        [SerializeField] private SpriteRenderer itemBacking;
        // The row's slots, filled left to right; any thing can go in any slot.
        [FormerlySerializedAs("hat")]
        [SerializeField] private SpriteRenderer slotOne;
        [FormerlySerializedAs("mask")]
        [SerializeField] private SpriteRenderer slotTwo;
        [FormerlySerializedAs("bag")]
        [SerializeField] private SpriteRenderer slotThree;
        // Each item fits a square this wide (metres); the gap is between items, the padding round them.
        [SerializeField, Min(0.05f)] private float itemSize = 0.4f;
        [SerializeField, Min(0f)] private float itemGap = 0.08f;
        [SerializeField, Min(0f)] private float itemPadding = 0.07f;
        // Between the top of the suspect's picture and the bottom of the row.
        [SerializeField] private float rowAboveHead = 0.06f;
        // The row floats gently up and down this far (metres), the things in it riding along, still.
        [SerializeField, Min(0f)] private float rowFloat = 0.025f;

        [Header("Reveal")]
        [SerializeField] private SpriteRenderer spotlight;

        private readonly List<SpriteRenderer> shown = new List<SpriteRenderer>();
        private Vector3 rowRest;
        private MaterialPropertyBlock fillBlock;
        // The backing strip's own alpha in the prefab, which fading multiplies.
        private float backingAlpha = -1f;

        public Transform Figure => figure;
        public Transform Items => items;
        public SpriteRenderer Spotlight => spotlight;

        // True once the prefab has its row of items (made before the row, it has none).
        public bool HasItemRow => items != null && slotTwo != null;

        // Dresses it as the look's species with what it carries, left to right (at most three).
        public void Show(LineupLook look, IReadOnlyList<Sprite> carried)
        {
            body.sprite = look.Body;
            body.transform.localScale = Vector3.one;
            figure.localScale = Vector3.one * (bodyHeight / look.Body.bounds.size.y);
            LayOutItems(carried);
            if (spotlight != null) spotlight.gameObject.SetActive(false);
            SetProgress(0f);
            SetAlpha(1f);
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

        // 0 a black silhouette, 1 the suspect in its own colours (its fade is kept).
        public void SetLit(float lit)
        {
            if (body == null) return;

            float shade = Mathf.Clamp01(lit);
            body.color = new Color(shade, shade, shade, body.color.a);
        }

        // Fades only the suspect's picture, leaving its row clear (the others at the reveal, so what
        // they carried can still be compared).
        public void SetBodyAlpha(float alpha)
        {
            SetAlpha(body, alpha);
        }

        // Fades the suspect and its row.
        public void SetAlpha(float alpha)
        {
            SetAlpha(body, alpha);
            foreach (SpriteRenderer item in shown) SetAlpha(item, alpha);
            if (itemBacking != null)
            {
                if (backingAlpha < 0f) backingAlpha = itemBacking.color.a;
                SetAlpha(itemBacking, alpha * backingAlpha);
            }
        }

        // The row's float at time (seconds, already offset for this suspect).
        public void Float(float time)
        {
            if (items != null) items.localPosition = rowRest + new Vector3(0f, rowFloat * Mathf.Sin(time * 1.7f), 0f);
        }

        // Breathing: the body stretches a little up and down (1 is its own height).
        public void Breathe(float stretch)
        {
            body.transform.localScale = new Vector3(1f, stretch, 1f);
        }

        // Puts what it carries side by side, centred over its head, on the backing strip.
        private void LayOutItems(IReadOnlyList<Sprite> carried)
        {
            shown.Clear();
            Use(slotOne, carried.Count > 0 ? carried[0] : null);
            Use(slotTwo, carried.Count > 1 ? carried[1] : null);
            Use(slotThree, carried.Count > 2 ? carried[2] : null);
            if (items == null) return;

            rowRest = new Vector3(0f, bodyHeight * 0.5f + rowAboveHead + itemPadding + itemSize * 0.5f, 0f);
            items.localPosition = rowRest;
            float width = shown.Count * itemSize + Mathf.Max(0, shown.Count - 1) * itemGap;
            float x = -width * 0.5f + itemSize * 0.5f;
            foreach (SpriteRenderer item in shown)
            {
                Vector2 size = item.sprite.bounds.size;
                item.transform.localPosition = new Vector3(x, 0f, 0f);
                item.transform.localScale = Vector3.one * (itemSize / Mathf.Max(size.x, size.y));
                x += itemSize + itemGap;
            }

            if (itemBacking == null) return;
            itemBacking.gameObject.SetActive(shown.Count > 0);
            itemBacking.transform.localPosition = Vector3.zero;
            // The strip is scaled down in the prefab so its round corners stay small; its size is
            // given in its own (scaled) units.
            float scale = Mathf.Max(0.01f, itemBacking.transform.localScale.x);
            itemBacking.size = new Vector2(width + itemPadding * 2f, itemSize + itemPadding * 2f) / scale;
        }

        private void Use(SpriteRenderer slot, Sprite art)
        {
            if (slot == null) return;

            slot.gameObject.SetActive(art != null);
            if (art == null) return;

            slot.sprite = art;
            shown.Add(slot);
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
