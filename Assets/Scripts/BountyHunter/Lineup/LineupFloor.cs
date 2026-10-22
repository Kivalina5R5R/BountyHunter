using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The suspect lineup on the floor: six suspects (each a Lineup Suspect prefab, see
    // LineupSuspectView) standing in a row across the middle on a dark band, each showing over its
    // head what the case gives it to carry (a hat, disguises, the money bag).
    // A player locks an answer in by standing on a suspect's ring until it fills (LineupPuzzle drives the fill); the suspect bounces
    // to say so. Answers stay secret: a player who has answered only gets a tick in the middle of
    // their marker, which follows them about. At the reveal the others fade back (their rows stay
    // clear) and the culprit swells under its spotlight (the CAUGHT stamp is on the wall only); then
    // the whole lineup swells a little and shrinks away, one after another, before the wall shows the
    // culprit. Lies flat on the floor, its top toward the wall.
    public sealed class LineupFloor : MonoBehaviour
    {
        private const float Height = 0.03f;
        private const int BandOrder = 1;
        private const int PickOrder = 23;
        // Parts of the reveal's swellSeconds: the light comes up over the first LightPart, the others
        // fade back over the first DimPart, and the culprit's swell starts SwellDelay in.
        private const float LightPart = 0.5f;
        private const float DimPart = 0.6f;
        private const float SwellDelay = 0.2f;

        [SerializeField] private LineupSuspectView suspectPrefab;

        [Header("Layout (metres)")]
        [SerializeField, Min(0.5f)] private float spacing = 1.76f;
        // How far toward the wall from the floor's middle the row stands.
        [SerializeField] private float rowZ = 0.25f;
        [SerializeField] private Vector2 bandSize = new Vector2(10.9f, 3.2f);
        [SerializeField] private Color bandColor = new Color(0.05f, 0.06f, 0.12f, 0.55f);

        [Header("Worn by the suspects")]
        [SerializeField] private Sprite cap;
        [SerializeField] private Sprite beanie;
        [SerializeField] private Sprite mask;
        [SerializeField] private Sprite glasses;
        [SerializeField] private Sprite faceMask;
        [SerializeField] private Sprite mustache;
        [SerializeField] private Sprite newspaper;
        [SerializeField] private Sprite gloves;
        [SerializeField] private Sprite bag;

        [Header("Answered")]
        // The tick shown in the middle of a player who has answered.
        [SerializeField] private Sprite pickMark;
        [SerializeField, Min(0.05f)] private float pickMarkSize = 0.4f;
        // The suspect's bounce when a player picks it: how much it swells and how long it takes.
        [SerializeField, Range(0f, 1f)] private float pickBounce = 0.3f;
        [SerializeField, Min(0.05f)] private float pickBounceSeconds = 0.35f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float entranceStagger = 0.12f;
        [SerializeField, Min(0.05f)] private float popSeconds = 0.4f;
        [SerializeField, Range(0f, 0.1f)] private float breathe = 0.02f;
        // How long the culprit takes to swell up and shrink back at the reveal, and how much it swells.
        [SerializeField, Min(0.05f)] private float swellSeconds = 1.2f;
        [SerializeField, Range(0f, 1f)] private float revealSwell = 0.35f;
        // A breath after the culprit's swell, before the lineup shrinks away.
        [SerializeField, Min(0f)] private float afterSwellSeconds = 0.5f;
        // The lineup leaving the floor: each suspect swells by shrinkSwell and shrinks to nothing over
        // shrinkSeconds, one shrinkStagger after another, the band fading with them.
        [SerializeField, Min(0.05f)] private float shrinkSeconds = 0.55f;
        [SerializeField, Min(0f)] private float shrinkStagger = 0.07f;
        [SerializeField, Range(0f, 0.5f)] private float shrinkSwell = 0.12f;
        // How much the others fade at the reveal (their alpha).
        [SerializeField, Range(0f, 1f)] private float revealDim = 0.3f;

        private readonly List<LineupSuspectView> suspects = new List<LineupSuspectView>();
        private readonly List<List<Sprite>> carriedBy = new List<List<Sprite>>();
        private readonly List<float> phases = new List<float>();
        private readonly List<Vector3> figureSizes = new List<Vector3>();
        private readonly Dictionary<int, Coroutine> bounces = new Dictionary<int, Coroutine>();
        private readonly Dictionary<Transform, Transform> locks = new Dictionary<Transform, Transform>();
        private readonly List<Transform> goneLocks = new List<Transform>();
        private Transform root;
        private SpriteRenderer band;
        private float shownAt;

        public int Count => suspects.Count;

        // False until the suspect prefab is set (Tools > Bounty Hunter > Build Lineup Prefabs).
        public bool IsSetUp => suspectPrefab != null;

        // Where a player stands to pick suspect index (its ring's centre, on the floor).
        public Vector3 StandPoint(int index)
        {
            return suspects[index].transform.position;
        }

        // Builds the lineup for the case and brings the suspects in one after another.
        public void Show(LineupCase lineup, IReadOnlyList<LineupLook> looks)
        {
            Hide();
            if (suspectPrefab == null)
            {
                Debug.LogError("[LineupFloor] Suspect Prefab is not set: run Tools > Bounty Hunter > Build Lineup Prefabs and save the scene", this);
                return;
            }

            root = new GameObject("Lineup Floor").transform;
            root.SetParent(transform, false);
            shownAt = Time.time;
            AddBand();

            for (int i = 0; i < lineup.Suspects.Count; i++)
            {
                LineupSuspect suspect = lineup.Suspects[i];
                float x = (i - (lineup.Suspects.Count - 1) * 0.5f) * spacing;
                LineupSuspectView view = Instantiate(suspectPrefab, root);
                view.name = $"Suspect {i + 1}";
                view.transform.localPosition = new Vector3(x, Height, rowZ);
                List<Sprite> carried = Carried(suspect);
                carriedBy.Add(carried);
                view.Show(looks[suspect.Species], carried);
                suspects.Add(view);
                phases.Add(Random.Range(0f, 6f));
                figureSizes.Add(view.Figure.localScale);
                StartCoroutine(Pop(view.Figure, view.Figure.localScale, i * entranceStagger));
                if (view.Items != null) StartCoroutine(Pop(view.Items, view.Items.localScale, i * entranceStagger + 0.1f));
            }
        }

        public void Hide()
        {
            StopAllCoroutines();
            ClearLocks();
            suspects.Clear();
            phases.Clear();
            figureSizes.Clear();
            carriedBy.Clear();
            bounces.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }

        // 0 to 1: how far the ring of suspect index has filled.
        public void ShowProgress(int index, float progress)
        {
            suspects[index].SetProgress(progress);
        }

        // The tick in the middle of a player who has answered, following them until the lineup is
        // hidden; it pops again each time they change their answer.
        public void ShowPick(Transform player)
        {
            if (player == null || pickMark == null || root == null) return;

            Vector3 size = Vector3.one * (pickMarkSize / pickMark.bounds.size.x);
            if (!locks.TryGetValue(player, out Transform mark) || mark == null)
            {
                var icon = new GameObject("Pick Mark", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                icon.transform.SetParent(root, false);
                icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                icon.sprite = pickMark;
                icon.sortingOrder = PickOrder;
                mark = icon.transform;
                locks[player] = mark;
            }

            StartCoroutine(Pop(mark, size, 0f));
        }

        // What suspect index carries, in the order shown over its head.
        public IReadOnlyList<Sprite> CarriedBy(int index)
        {
            return index >= 0 && index < carriedBy.Count ? carriedBy[index] : (IReadOnlyList<Sprite>)System.Array.Empty<Sprite>();
        }

        // Suspect index swells and settles back: a player has just picked it.
        public void Bounce(int index)
        {
            if (index < 0 || index >= suspects.Count) return;

            if (bounces.TryGetValue(index, out Coroutine running) && running != null) StopCoroutine(running);
            bounces[index] = StartCoroutine(Swell(index));
        }

        public IEnumerator Reveal(int culprit)
        {
            if (culprit < 0 || culprit >= suspects.Count) yield break;

            LineupSuspectView caught = suspects[culprit];
            SpriteRenderer spot = caught.Spotlight;
            Vector3 spotScale = spot != null ? spot.transform.localScale : Vector3.one;
            float spotAlpha = spot != null ? spot.color.a : 0f;
            if (spot != null)
            {
                SetAlpha(spot, 0f);
                spot.gameObject.SetActive(true);
            }

            // Three overlapping moves, each eased in and out so nothing snaps: the spotlight brightens
            // and opens up under the culprit, the others fade back, and a moment after the light the
            // culprit swells up and settles back to its size.
            Vector3 figureScale = caught.Figure.localScale;
            for (float time = 0f; time < swellSeconds; time += Time.deltaTime)
            {
                float u = time / swellSeconds;
                float light = Smooth(u / LightPart);
                if (spot != null)
                {
                    spot.transform.localScale = spotScale * Mathf.Lerp(0.6f, 1f, light);
                    SetAlpha(spot, spotAlpha * light);
                }

                float dim = Smooth(u / DimPart);
                for (int i = 0; i < suspects.Count; i++)
                {
                    if (i != culprit) suspects[i].SetBodyAlpha(Mathf.Lerp(1f, revealDim, dim));
                }

                float swell = Smooth((u - SwellDelay) / (1f - SwellDelay));
                caught.Figure.localScale = figureScale * (1f + revealSwell * Mathf.Sin(Mathf.PI * swell));
                yield return null;
            }

            if (spot != null)
            {
                spot.transform.localScale = spotScale;
                SetAlpha(spot, spotAlpha);
            }

            caught.Figure.localScale = figureScale;
            if (afterSwellSeconds > 0f) yield return new WaitForSeconds(afterSwellSeconds);
        }

        // Clamped to 0..1 and eased in and out.
        private static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // The whole lineup swells a little and shrinks away, left to right, the band fading with it.
        public IEnumerator ShrinkAway()
        {
            var sizes = new List<Vector3>();
            foreach (LineupSuspectView suspect in suspects) sizes.Add(suspect.transform.localScale);
            float bandAlpha = band != null ? band.color.a : 0f;
            var spots = new List<(SpriteRenderer Light, float Alpha)>();
            foreach (LineupSuspectView suspect in suspects)
            {
                if (suspect.Spotlight != null && suspect.Spotlight.gameObject.activeSelf) spots.Add((suspect.Spotlight, suspect.Spotlight.color.a));
            }

            float total = shrinkSeconds + shrinkStagger * Mathf.Max(0, suspects.Count - 1);
            for (float time = 0f; time < total; time += Time.deltaTime)
            {
                for (int i = 0; i < suspects.Count; i++)
                {
                    float u = Mathf.Clamp01((time - i * shrinkStagger) / shrinkSeconds);
                    suspects[i].transform.localScale = sizes[i] * ShrinkCurve(u);
                }

                float left = 1f - Smooth(time / total);
                if (band != null) SetAlpha(band, bandAlpha * left);
                foreach ((SpriteRenderer light, float alpha) in spots) SetAlpha(light, alpha * left);
                yield return null;
            }

            foreach (LineupSuspectView suspect in suspects) suspect.gameObject.SetActive(false);
            if (band != null) SetAlpha(band, 0f);
        }

        // 1 at the start, up to 1 + shrinkSwell a third of the way, then down to nothing, easing in.
        private float ShrinkCurve(float u)
        {
            const float peak = 0.3f;
            if (u < peak) return 1f + shrinkSwell * Mathf.Sin(u / peak * Mathf.PI * 0.5f);
            float down = (u - peak) / (1f - peak);
            return (1f + shrinkSwell) * (1f - down * down);
        }

        private static void SetAlpha(SpriteRenderer picture, float alpha)
        {
            Color colour = picture.color;
            colour.a = alpha;
            picture.color = colour;
        }

        private void Update()
        {
            float age = Time.time - shownAt;
            for (int i = 0; i < suspects.Count; i++)
            {
                float phase = age + phases[i];
                suspects[i].Breathe(1f + breathe * Mathf.Sin(phase * 2.4f));
                suspects[i].Float(phase);
                suspects[i].Figure.localRotation = Quaternion.Euler(0f, 0f, 1.5f * Mathf.Sin(phase * 1.3f));
            }
        }

        // Keeps each tick in the middle of its player's marker.
        private void LateUpdate()
        {
            goneLocks.Clear();
            foreach (KeyValuePair<Transform, Transform> pair in locks)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    goneLocks.Add(pair.Key);
                    continue;
                }

                Vector3 at = pair.Key.position;
                pair.Value.position = new Vector3(at.x, Height + 0.05f, at.z);
            }

            foreach (Transform gone in goneLocks) locks.Remove(gone);
        }

        // What the suspect carries, in a random order along its row.
        private List<Sprite> Carried(LineupSuspect suspect)
        {
            var carried = new List<Sprite>(3);
            if (suspect.Hat == LineupHat.Cap) carried.Add(cap);
            if (suspect.Hat == LineupHat.Beanie) carried.Add(beanie);
            if (suspect.Mask) carried.Add(mask);
            if (suspect.Glasses) carried.Add(glasses);
            if (suspect.FaceMask) carried.Add(faceMask);
            if (suspect.Mustache) carried.Add(mustache);
            if (suspect.Newspaper) carried.Add(newspaper);
            if (suspect.Gloves) carried.Add(gloves);
            if (suspect.Bag) carried.Add(bag);
            carried.RemoveAll(art => art == null);
            for (int i = carried.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (carried[i], carried[j]) = (carried[j], carried[i]);
            }

            return carried;
        }

        private void ClearLocks()
        {
            foreach (Transform icon in locks.Values)
            {
                if (icon != null) Destroy(icon.gameObject);
            }

            locks.Clear();
        }

        private void AddBand()
        {
            band = new GameObject("Band", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            band.transform.SetParent(root, false);
            // Centred above the row's middle, so it takes in the items over the heads too.
            band.transform.localPosition = new Vector3(0f, 0f, rowZ + 0.3f);
            band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            band.sprite = PosterSprites.Dot;
            band.color = bandColor;
            band.sortingOrder = BandOrder;
            Vector2 size = PosterSprites.Dot.bounds.size;
            band.transform.localScale = new Vector3(bandSize.x / size.x, bandSize.y / size.y, 1f);
        }

        private IEnumerator Swell(int index)
        {
            Transform figure = suspects[index].Figure;
            Vector3 size = figureSizes[index];
            for (float time = 0f; time < pickBounceSeconds; time += Time.deltaTime)
            {
                float u = time / pickBounceSeconds;
                figure.localScale = size * (1f + pickBounce * Mathf.Sin(Mathf.PI * u));
                yield return null;
            }

            figure.localScale = size;
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

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
