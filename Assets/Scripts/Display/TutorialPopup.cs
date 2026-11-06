using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Display
{
    // The HOW TO PLAY board on the wall, worked from the kiosk. The wall dims and the wide board
    // springs up from below with a little overshoot and a wobble, then shows the pages one at a time:
    // a picture popping in over a slowly turning sunburst, its title and a few lines on what to do,
    // which step it is, and a dot per page. Each page turns by itself after
    // pageSeconds, and the board drops away after the last. On the kiosk the remote (TutorialRemote)
    // pops up with it: NEXT and BACK slide the page out and the next one in from that side; BACK on
    // the first page, NEXT on the last or touching the dimmed kiosk drops both boards away. Runs on
    // unscaled time, so it works while the game is frozen. Built by KioskTutorialBuilder.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TutorialPopup : MonoBehaviour
    {
        [Serializable]
        private sealed class Page
        {
            [SerializeField] private Sprite picture;
            [SerializeField] private string title;
            [SerializeField, TextArea(2, 4)] private string body;

            public Sprite Picture => picture;
            public string Title => title;
            public string Body => body;
        }

        [SerializeField] private Page[] pages = Array.Empty<Page>();

        [Header("Wall")]
        [SerializeField] private RectTransform board;
        // Slides sideways as the pages change; holds the picture, the burst, the title and the words.
        [SerializeField] private RectTransform pageRoot;
        [SerializeField] private CanvasGroup pageGroup;
        [SerializeField] private Image picture;
        [SerializeField] private RectTransform burst;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        // Which step the page is, as stepWords; may be empty.
        [SerializeField] private TMP_Text step;
        // {0} is the page on show and {1} how many there are.
        [SerializeField] private string stepWords = "STEP {0} / {1}";
        // One copy of this dot is made per page, side by side under its parent.
        [SerializeField] private Image dotTemplate;

        // The kiosk's controls; may be empty (the pages then just turn by themselves).
        [SerializeField] private TutorialRemote remote;

        [Header("Motion")]
        // Each page turns by itself after this long; the last one closes the tutorial.
        [SerializeField, Min(1f)] private float pageSeconds = 9f;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float closeSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float pageOutSeconds = 0.16f;
        [SerializeField, Min(0.05f)] private float pageInSeconds = 0.36f;
        // How far a page slides as it leaves and as the next one comes in.
        [SerializeField, Min(0f)] private float pageSlide = 260f;
        // How far the board rises from as it springs up.
        [SerializeField, Min(0f)] private float openRise = 120f;
        // The boards' tilt as they spring up, settling to straight.
        [SerializeField, Range(0f, 15f)] private float popTilt = 3f;
        // The picture bobs this far and this fast; the sunburst turns this fast (degrees a second).
        [SerializeField] private float bobHeight = 6f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float burstTurnSpeed = 14f;

        [Header("Dots")]
        [SerializeField] private Color dotOn = new Color(1f, 0.8f, 0.13f, 1f);
        [SerializeField] private Color dotOff = new Color(1f, 1f, 1f, 0.3f);
        [SerializeField, Min(1f)] private float dotOnScale = 1.6f;

        private CanvasGroup group;
        private Image[] dots = Array.Empty<Image>();
        private Coroutine showing;
        private Coroutine turning;
        private int current;
        private float pictureRest;
        private float bodyRest;
        // From 0 (none) to 1 (full): how much the picture bobs, so it settles in after popping up.
        private float bob;
        // How long the page on show has been fully in; counts towards pageSeconds.
        private float pageTime;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();

            // With the kiosk switched off its remote never wakes up and cannot be shown; the pages
            // then just turn by themselves.
            Transform remoteKiosk = remote != null ? remote.transform.parent : null;
            if (remoteKiosk != null && !remoteKiosk.gameObject.activeInHierarchy) remote = null;

            if (remote != null)
            {
                remote.BackPressed += Back;
                remote.NextPressed += Next;
                remote.ClosePressed += Close;
            }

            if (picture != null) pictureRest = picture.rectTransform.anchoredPosition.y;
            if (body != null) bodyRest = body.rectTransform.anchoredPosition.y;
            MakeDots();
        }

        // False from the moment it starts to drop away.
        public bool IsOpen => gameObject.activeInHierarchy && group != null && group.interactable;

        public void Open()
        {
            gameObject.SetActive(true);
            if (remote != null) remote.SetActive(true);
            current = 0;
            ShowPage(current);
            PlacePage(0f, 0f);
            Play(PopUp());
        }

        public void Close()
        {
            if (!gameObject.activeInHierarchy) return;
            StopTurning();
            Play(DropAway());
        }

        private void Next()
        {
            if (current >= pages.Length - 1) Close();
            else TurnTo(current + 1);
        }

        private void Back()
        {
            if (current <= 0) Close();
            else TurnTo(current - 1);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (burst != null) burst.localRotation = Quaternion.Euler(0f, 0f, -time * burstTurnSpeed);

            if (picture != null)
            {
                RectTransform rect = picture.rectTransform;
                float height = Mathf.Sin(time * bobSpeed) * bobHeight * bob;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, pictureRest + height);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * bobSpeed * 0.5f) * 3f * bob);
            }

            float ease = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < dots.Length; i++)
            {
                bool on = i == current;
                Image dot = dots[i];
                dot.color = Color.Lerp(dot.color, on ? dotOn : dotOff, ease);
                float scale = Mathf.Lerp(dot.rectTransform.localScale.x, on ? dotOnScale : 1f, ease);
                dot.rectTransform.localScale = Vector3.one * scale;
            }

            CountPageTime();
        }

        // The page turns by itself once it has been fully in for pageSeconds.
        private void CountPageTime()
        {
            if (showing != null || turning != null || !group.interactable) return;

            pageTime += Time.unscaledDeltaTime;
            if (pageTime >= pageSeconds) Next();
        }

        private void MakeDots()
        {
            if (dotTemplate == null) return;

            dots = new Image[pages.Length];
            Transform row = dotTemplate.transform.parent;
            for (int i = 0; i < pages.Length; i++)
            {
                dots[i] = i == 0 ? dotTemplate : Instantiate(dotTemplate, row);
                dots[i].color = dotOff;
            }

            if (pages.Length == 0) dotTemplate.gameObject.SetActive(false);
        }

        private void TurnTo(int page)
        {
            int direction = page > current ? 1 : -1;
            current = page;
            ShowControls(page);
            StopTurning();
            turning = StartCoroutine(SlidePages(page, direction));
        }

        private void StopTurning()
        {
            if (turning != null) StopCoroutine(turning);
            turning = null;
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= pages.Length) return;

            Page page = pages[index];
            if (picture != null)
            {
                picture.sprite = page.Picture;
                picture.enabled = page.Picture != null;
            }

            if (title != null) title.text = page.Title;
            if (body != null) body.text = page.Body;
            if (step != null) step.text = string.Format(stepWords, index + 1, pages.Length);
            ShowControls(index);
        }

        // The remote's words follow the page at once, before the wall has finished turning.
        private void ShowControls(int index)
        {
            pageTime = 0f;
            if (remote != null) remote.ShowPage(index, pages.Length);
        }

        private void Play(IEnumerator routine)
        {
            if (showing != null) StopCoroutine(showing);
            showing = StartCoroutine(routine);
        }

        private IEnumerator PopUp()
        {
            SetShown(0f, true);
            PlacePicture(0f);
            SetWords(0f);
            for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / openSeconds;
                SetShown(Mathf.Clamp01(u * 2.5f), true);
                float spring = EaseOutBack(u);
                PlaceBoards(Mathf.LerpUnclamped(0.7f, 1f, spring), 1f - spring,
                    popTilt * (1f - u) * Mathf.Sin(u * Mathf.PI * 3f));
                yield return null;
            }

            SetShown(1f, true);
            PlaceBoards(1f, 0f, 0f);
            showing = null;
            // A page turned while the boards sprang up is cut short; the page it was going to comes in.
            StopTurning();
            ShowPage(current);
            turning = StartCoroutine(PageIn(0f));
        }

        private IEnumerator DropAway()
        {
            SetShown(group.alpha, false);
            float from = group.alpha;
            for (float t = 0f; t < closeSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / closeSeconds;
                SetShown(from * (1f - u), false);
                PlaceBoards(Mathf.Lerp(1f, 0.85f, u * u), u * u * 0.5f, 0f);
                yield return null;
            }

            SetShown(0f, false);
            showing = null;
            if (remote != null) remote.SetActive(false);
            gameObject.SetActive(false);
        }

        // The wall board and the kiosk remote fade together; only the remote takes touches.
        private void SetShown(float alpha, bool touchable)
        {
            group.alpha = alpha;
            group.interactable = touchable;
            group.blocksRaycasts = false;
            if (remote != null) remote.SetShown(alpha, touchable);
        }

        // The page on show slides out against the way it is turned, then the next comes in.
        private IEnumerator SlidePages(int page, int direction)
        {
            float fromX = pageRoot != null ? pageRoot.anchoredPosition.x : 0f;
            float fromAlpha = pageGroup != null ? pageGroup.alpha : 1f;
            for (float t = 0f; t < pageOutSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / pageOutSeconds;
                PlacePage(Mathf.Lerp(fromX, -direction * pageSlide, u * u), fromAlpha * (1f - u));
                yield return null;
            }

            ShowPage(page);
            yield return PageIn(direction * pageSlide);
        }

        // The page slides in from fromX and fades up; then the picture pops up and the words follow.
        private IEnumerator PageIn(float fromX)
        {
            PlacePicture(0f);
            SetWords(0f);
            float total = pageInSeconds * 1.6f;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float slide = Mathf.Clamp01(t / pageInSeconds);
                PlacePage(Mathf.Lerp(fromX, 0f, EaseOutCubic(slide)), Mathf.Clamp01(slide * 2f));
                float pop = Mathf.Clamp01((t - pageInSeconds * 0.2f) / pageInSeconds);
                PlacePicture(EaseOutBack(pop));
                SetWords(Mathf.Clamp01((t - pageInSeconds * 0.45f) / pageInSeconds));
                yield return null;
            }

            PlacePage(0f, 1f);
            PlacePicture(1f);
            SetWords(1f);
            turning = null;
        }

        // Both boards at this scale and tilt, lowered by this share of how far each rises.
        private void PlaceBoards(float scale, float lowered, float tilt)
        {
            if (remote != null) remote.PlaceBoard(scale, lowered, tilt);
            if (board == null) return;
            board.localScale = Vector3.one * scale;
            board.anchoredPosition = new Vector2(board.anchoredPosition.x, -lowered * openRise);
            board.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void PlacePage(float x, float alpha)
        {
            if (pageRoot != null) pageRoot.anchoredPosition = new Vector2(x, pageRoot.anchoredPosition.y);
            if (pageGroup != null) pageGroup.alpha = alpha;
        }

        // The picture and the sunburst behind it, at this share of their size; it bobs once it is full.
        private void PlacePicture(float size)
        {
            bob = Mathf.Clamp01(size);
            if (picture != null) picture.rectTransform.localScale = Vector3.one * size;
            if (burst != null) burst.localScale = Vector3.one * Mathf.Clamp01(size);
        }

        // The title drops in and the words rise in a little after it, both fading up.
        private void SetWords(float shown)
        {
            float titleIn = EaseOutCubic(Mathf.Clamp01(shown * 1.5f));
            float bodyIn = EaseOutCubic(Mathf.Clamp01(shown * 1.5f - 0.5f));
            if (title != null)
            {
                title.alpha = titleIn;
                title.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.25f, 1f, titleIn);
            }

            if (step != null) step.alpha = titleIn;

            if (body != null)
            {
                body.alpha = bodyIn;
                RectTransform rect = body.rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bodyRest - (1f - bodyIn) * 15f);
            }
        }

        private static float EaseOutCubic(float t)
        {
            float back = 1f - t;
            return 1f - back * back * back;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
