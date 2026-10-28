using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The bins on the floor in mini game 2, laid out in Assets/Prefabs/Cctv/Cctv Floor.prefab (move,
    // add, take out or change the picture of any bin there; the bins count left to right in the
    // hierarchy's order and must match the camera's on the wall). A player picks the bin they think
    // the money bag is in by standing on its ring until it fills (CctvPuzzle drives the fill); the bin
    // wobbles to say so. Picks stay secret: a player who has picked only gets a tick in the middle of
    // their marker, which follows them about. At the reveal a thief pops out of every bin that has
    // one, one after another, the empty bins fade back, and the bag thief's bin lights up while the
    // thief swells. Lies flat on the floor, its top toward the wall.
    public sealed class CctvFloor : MonoBehaviour
    {
        private const float Height = 0.03f;
        private const int PickOrder = 23;

        [SerializeField] private GameObject layoutPrefab;

        [Header("Picked")]
        // The tick shown in the middle of a player who has picked a bin.
        [SerializeField] private Sprite pickMark;
        [SerializeField, Min(0.05f)] private float pickMarkSize = 0.4f;
        [SerializeField, Range(0f, 1f)] private float pickBounce = 0.25f;
        [SerializeField, Min(0.05f)] private float pickBounceSeconds = 0.35f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float entranceStagger = 0.12f;
        [SerializeField, Min(0.05f)] private float popSeconds = 0.4f;
        // Between one thief popping out at the reveal and the next.
        [SerializeField, Min(0f)] private float revealStagger = 0.3f;
        // How long the bag thief takes to swell up and settle back, and how much it swells.
        [SerializeField, Min(0.05f)] private float swellSeconds = 1.2f;
        [SerializeField, Range(0f, 1f)] private float revealSwell = 0.35f;
        // How much the empty bins fade at the reveal (their alpha).
        [SerializeField, Range(0f, 1f)] private float revealDim = 0.3f;

        private readonly List<CctvBinView> bins = new List<CctvBinView>();
        private readonly Dictionary<int, Coroutine> bounces = new Dictionary<int, Coroutine>();
        private readonly Dictionary<Transform, Transform> picks = new Dictionary<Transform, Transform>();
        private readonly List<Transform> gonePicks = new List<Transform>();
        private Transform root;

        public int Count => bins.Count;

        // How many bins the layout prefab has.
        public int BinCount => layoutPrefab != null ? layoutPrefab.GetComponentsInChildren<CctvBinView>(true).Length : 0;

        // False until the layout prefab is set (Tools > Bounty Hunter > Build CCTV Prefabs).
        public bool IsSetUp => BinCount > 0;

        // Where a player stands to pick bin index (its ring's centre, on the floor).
        public Vector3 StandPoint(int index)
        {
            return bins[index].transform.position;
        }

        // Puts the layout on the floor and brings the bins in one after another.
        public void Show()
        {
            Hide();
            if (layoutPrefab == null)
            {
                Debug.LogError("[CctvFloor] Layout Prefab is not set: run Tools > Bounty Hunter > Build CCTV Prefabs and save the scene", this);
                return;
            }

            root = Instantiate(layoutPrefab, transform, false).transform;
            root.name = "CCTV Floor";
            root.GetComponentsInChildren(true, bins);
            for (int i = 0; i < bins.Count; i++)
            {
                CctvBinView view = bins[i];
                view.Show();
                StartCoroutine(Pop(view.Figure, view.Figure.localScale, i * entranceStagger));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            ClearPicks();
            bins.Clear();
            bounces.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }

        // 0 to 1: how far the ring of bin index has filled.
        public void ShowProgress(int index, float progress)
        {
            bins[index].SetProgress(progress);
        }

        // The tick in the middle of a player who has picked, following them until the bins are
        // hidden; it pops again each time they change their pick.
        public void ShowPick(Transform player)
        {
            if (player == null || pickMark == null || root == null) return;

            Vector3 size = Vector3.one * (pickMarkSize / pickMark.bounds.size.x);
            if (!picks.TryGetValue(player, out Transform mark) || mark == null)
            {
                var icon = new GameObject("Pick Mark", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                icon.transform.SetParent(root, false);
                icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                icon.sprite = pickMark;
                icon.sortingOrder = PickOrder;
                mark = icon.transform;
                picks[player] = mark;
            }

            StartCoroutine(Pop(mark, size, 0f));
        }

        // Bin index wobbles and settles back: a player has just picked it.
        public void Bounce(int index)
        {
            if (index < 0 || index >= bins.Count) return;

            if (bounces.TryGetValue(index, out Coroutine running) && running != null) StopCoroutine(running);
            bounces[index] = StartCoroutine(Swell(bins[index].Figure, pickBounce, pickBounceSeconds));
        }

        // Each thief pops out of the bin it ended in (thiefBodies[thief] is its picture), the empty
        // bins fade back, and the bag thief's bin lights up while the thief swells.
        public IEnumerator Reveal(CctvClip clip, IReadOnlyList<Sprite> thiefBodies)
        {
            for (int bin = 0; bin < bins.Count; bin++)
            {
                if (clip.ThiefIn(bin) < 0) StartCoroutine(Fade(bins[bin], revealDim, popSeconds));
            }

            for (int thief = 0; thief < clip.Thieves; thief++)
            {
                int bin = clip.FinalBins[thief];
                if (bin < 0 || bin >= bins.Count || bins[bin].Thief == null) continue;

                CctvBinView view = bins[bin];
                view.Dress(thiefBodies[thief], thief == clip.BagThief);
                view.Thief.gameObject.SetActive(true);
                StartCoroutine(Pop(view.Thief, view.Thief.localScale, 0f));
                if (revealStagger > 0f) yield return new WaitForSeconds(revealStagger);
            }

            if (clip.Answer < 0 || clip.Answer >= bins.Count) yield break;

            // The last thief finishes popping out before the bag thief swells.
            float popLeft = popSeconds - revealStagger;
            if (popLeft > 0f) yield return new WaitForSeconds(popLeft);

            CctvBinView caught = bins[clip.Answer];
            SpriteRenderer spot = caught.Spotlight;
            if (spot != null)
            {
                float alpha = spot.color.a;
                SetAlpha(spot, 0f);
                spot.gameObject.SetActive(true);
                StartCoroutine(FadeIn(spot, alpha, swellSeconds * 0.5f));
            }

            if (caught.Thief != null) yield return Swell(caught.Thief, revealSwell, swellSeconds);
        }

        // Keeps each tick in the middle of its player's marker.
        private void LateUpdate()
        {
            gonePicks.Clear();
            foreach (KeyValuePair<Transform, Transform> pair in picks)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    gonePicks.Add(pair.Key);
                    continue;
                }

                Vector3 at = pair.Key.position;
                pair.Value.position = new Vector3(at.x, Height + 0.05f, at.z);
            }

            foreach (Transform gone in gonePicks) picks.Remove(gone);
        }

        private void ClearPicks()
        {
            foreach (Transform icon in picks.Values)
            {
                if (icon != null) Destroy(icon.gameObject);
            }

            picks.Clear();
        }

        private static IEnumerator Fade(CctvBinView view, float to, float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                view.SetAlpha(Mathf.Lerp(1f, to, Smooth(time / seconds)));
                yield return null;
            }

            view.SetAlpha(to);
        }

        private static IEnumerator FadeIn(SpriteRenderer picture, float to, float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                SetAlpha(picture, to * Smooth(time / seconds));
                yield return null;
            }

            SetAlpha(picture, to);
        }

        // Swells by amount and settles back over seconds.
        private static IEnumerator Swell(Transform piece, float amount, float seconds)
        {
            Vector3 size = piece.localScale;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                piece.localScale = size * (1f + amount * Mathf.Sin(Mathf.PI * Smooth(time / seconds)));
                yield return null;
            }

            piece.localScale = size;
        }

        // Grows from nothing past its size and settles back, after delay.
        private IEnumerator Pop(Transform piece, Vector3 size, float delay)
        {
            piece.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            for (float time = 0f; time < popSeconds; time += Time.deltaTime)
            {
                piece.localScale = size * EaseOutBack(time / popSeconds);
                yield return null;
            }

            piece.localScale = size;
        }

        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        private static void SetAlpha(SpriteRenderer picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }
    }
}
