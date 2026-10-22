using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BountyHunter.Thieves
{
    // The result screen on the wall once the time is up: TIME'S UP and WINNER on the board over the
    // standings (the winner's row on top and bigger, the others with their faces), the winner dancing
    // on the bench and the others clapping beside the board. When shown, the board pops up first, then
    // its pieces one after another while the scores count up, then the dogs. Its moments are raised
    // as events (ResultSounds plays them). Laid out in Assets/Prefabs/UI/Result.prefab; move or
    // resize anything there. Console lines start with [ResultScreen].
    public sealed class ResultScreen : MonoBehaviour
    {
        // How one character looks here: its animations (states Dance and Clap), its face for the rows
        // and its size, as a multiple of its frames' pixels, matched to how big it is in the intro so
        // every dog keeps its own size wherever it stands. A spot's own scale enlarges it further.
        // On the winner's stand winnerOffset moves it (UI pixels, x right, y up) from Stand Spot, so
        // each dog can be set to stand just right there; it slides in and sways around that place.
        // Ticking previewOnStand shows that dog on the stand in the prefab while setting it up, moving
        // as its offset changes (editor only; the game ignores it).
        [Serializable]
        private sealed class Look
        {
            [SerializeField] private string character;
            [SerializeField] private RuntimeAnimatorController animations;
            [SerializeField] private Sprite face;
            [SerializeField, Min(0.01f)] private float size = 0.5f;
            [SerializeField] private Vector2 winnerOffset;
            [SerializeField] private bool previewOnStand;

            public string Character => character;
            public RuntimeAnimatorController Animations => animations;
            public Sprite Face => face;
            public float Size => size;
            public Vector2 WinnerOffset => winnerOffset;
            public bool PreviewOnStand => previewOnStand;
        }

        // One line of the standings; face may be left empty (the winner's line has none).
        [Serializable]
        private sealed class Row
        {
            [SerializeField] private GameObject root;
            [SerializeField] private Image face;
            [SerializeField] private TMP_Text rank;
            [SerializeField] private TMP_Text name;
            [SerializeField] private TMP_Text score;

            public GameObject Root => root;
            public Image Face => face;
            public TMP_Text Rank => rank;
            public TMP_Text Name => name;
            public TMP_Text Score => score;
        }

        private const string DanceState = "Dance";
        private const string ClapState = "Clap";

        [SerializeField] private Look[] looks;
        // Top to bottom: row 1 is the winner.
        [SerializeField] private Row[] rows;
        // Stands on the bench and dances.
        [SerializeField] private Animator winner;
        // Where the winner stands on the bench before its own Winner Offset (UI pixels). The Winner
        // picture itself may be anywhere (a preview moves it); the game puts it here.
        [SerializeField] private Vector2 standSpot = new Vector2(16f, 21.125824f);
        // Clap beside the board, for 2nd, 3rd and 4th place in that order.
        [SerializeField] private Animator[] clappers;
        [SerializeField] private string rankFormat = "NO. {0}";
        [SerializeField] private string scoreFormat = "{0}";

        [Header("Entrance")]
        // Come in this order when the screen is shown (the rows slide in from the left and then count
        // their scores up, the rest pops up); pieces switched off are skipped.
        [SerializeField] private RectTransform[] entrance;
        [SerializeField, Min(0f)] private float entranceStagger = 0.12f;
        [SerializeField, Min(0.05f)] private float popSeconds = 0.45f;
        // How far left of its place a row starts, in wall units, and how long it takes to arrive.
        [SerializeField, Min(0f)] private float slideDistance = 260f;
        [SerializeField, Min(0.05f)] private float slideSeconds = 0.55f;
        [SerializeField, Min(0f)] private float countUpSeconds = 1.2f;

        [Header("Idle")]
        // Keeps the screen alive once a piece has come in; each moves around where it rests.
        [SerializeField] private IdleMotion[] idle;
        // How long the idle motion takes to build up after a piece has arrived.
        [SerializeField, Min(0.05f)] private float idleRampSeconds = 0.6f;

        private readonly Dictionary<RectTransform, Rest> rests = new Dictionary<RectTransform, Rest>();
        private readonly Dictionary<TMP_Text, int> shownScores = new Dictionary<TMP_Text, int>();
        // Where the winner stands before its own winnerOffset.
        private Rest winnerSpot;
        // When each piece finished coming in; idle motion starts from then.
        private readonly Dictionary<RectTransform, float> arrivedAt = new Dictionary<RectTransform, float>();

        // Where a piece sits when nothing moves it.
        private readonly struct Rest
        {
            public Rest(RectTransform piece)
                : this(piece.anchoredPosition, piece.localScale, piece.localRotation)
            {
            }

            public Rest(Vector2 position, Vector3 scale, Quaternion rotation)
            {
                Position = position;
                Scale = scale;
                Rotation = rotation;
            }

            // The same rest moved by offset.
            public Rest Moved(Vector2 offset)
            {
                return new Rest(Position + offset, Scale, Rotation);
            }

            public Vector2 Position { get; }
            public Vector3 Scale { get; }
            public Quaternion Rotation { get; }
        }

        // Raised each time a counting score shows a new number, with how far the count is (0..1).
        public event Action<float> ScoreTicked;

        // Raised as a row's score has finished counting up, with its place (1 is the winner).
        public event Action<int> ScoreCounted;

        // Shows the standings, best first, and plays the entrance.
        public void Show(IReadOnlyList<ResultStanding> standings)
        {
            gameObject.SetActive(true);
            RememberRests();

            for (int i = 0; i < Count(rows); i++)
            {
                bool used = i < standings.Count;
                if (rows[i].Root != null) rows[i].Root.SetActive(used);
                if (used) FillRow(rows[i], i + 1, standings[i]);
            }

            Cast(winner, standings.Count > 0 ? standings[0].Character : null, DanceState, true);
            for (int i = 0; i < Count(clappers); i++)
            {
                Cast(clappers[i], i + 1 < standings.Count ? standings[i + 1].Character : null, ClapState, false);
            }

            StopAllCoroutines();
            StartCoroutine(Entrance(standings));
            Debug.Log($"[ResultScreen] {(standings.Count > 0 ? standings[0].Character : "Nobody")} wins", this);
        }

        // Read once, before anything has moved them.
        private void RememberRests()
        {
            if (rests.Count > 0) return;
            if (winner != null) winnerSpot = new Rest(standSpot, winner.transform.localScale, winner.transform.localRotation);
            if (entrance != null)
            {
                foreach (RectTransform piece in entrance)
                {
                    if (piece != null) rests[piece] = new Rest(piece);
                }
            }

            if (idle == null) return;
            foreach (IdleMotion motion in idle)
            {
                if (motion != null && motion.Piece != null && !rests.ContainsKey(motion.Piece)) rests[motion.Piece] = new Rest(motion.Piece);
            }
        }

        private void Update()
        {
            if (idle == null) return;

            foreach (IdleMotion motion in idle)
            {
                if (motion == null || motion.Piece == null) continue;
                if (!arrivedAt.TryGetValue(motion.Piece, out float arrived) || !rests.TryGetValue(motion.Piece, out Rest rest)) continue;

                float age = Time.time - arrived;
                float ramp = Mathf.SmoothStep(0f, 1f, age / idleRampSeconds);
                motion.Apply(rest.Position, rest.Scale, rest.Rotation, age, ramp);
            }
        }

        private void FillRow(Row row, int place, ResultStanding standing)
        {
            Look look = LookOf(standing.Character);
            if (row.Rank != null) row.Rank.text = string.Format(rankFormat, place);
            if (row.Name != null) row.Name.text = standing.Character.ToUpperInvariant();
            if (row.Score != null) row.Score.text = string.Format(scoreFormat, 0);
            if (row.Face == null) return;

            row.Face.sprite = look != null ? look.Face : null;
            row.Face.enabled = row.Face.sprite != null;
        }

        // Puts the character's animations on the spot and starts the state; no character hides it.
        // onStand: the winner's spot, where the character's winnerScale applies too.
        private void Cast(Animator spot, string character, string state, bool onStand)
        {
            if (spot == null) return;

            Look look = character != null ? LookOf(character) : null;
            bool shown = look != null && look.Animations != null;
            spot.gameObject.SetActive(shown);
            if (!shown) return;

            spot.runtimeAnimatorController = look.Animations;
            spot.Play(state, 0, 0f);
            // Shows the first frame now and sizes the picture to it; every frame of a clip is one size.
            spot.Update(0f);
            if (!spot.TryGetComponent(out Image picture) || picture.sprite == null) return;

            picture.SetNativeSize();
            picture.rectTransform.sizeDelta *= look.Size;
            if (onStand) StandOnSpot(picture.rectTransform, look.WinnerOffset);
        }

        private IEnumerator Entrance(IReadOnlyList<ResultStanding> standings)
        {
            arrivedAt.Clear();
            foreach (KeyValuePair<RectTransform, Rest> rest in rests)
            {
                Place(rest.Key, rest.Value);
            }

            // Idle pieces that do not come in move from the start.
            if (idle != null)
            {
                foreach (IdleMotion motion in idle)
                {
                    if (motion != null && motion.Piece != null && !IsInEntrance(motion.Piece)) arrivedAt[motion.Piece] = Time.time;
                }
            }

            if (entrance == null) yield break;

            foreach (RectTransform piece in entrance)
            {
                if (piece == null) continue;
                if (RowIndexOf(piece) >= 0) FadeOf(piece).alpha = 0f;
                else piece.localScale = Vector3.zero;
            }

            for (int i = 0; i < entrance.Length; i++)
            {
                RectTransform piece = entrance[i];
                if (piece == null || !piece.gameObject.activeInHierarchy) continue;

                int row = RowIndexOf(piece);
                if (row < 0) StartCoroutine(Pop(piece));
                else StartCoroutine(SlideIn(piece, rows[row].Score, row + 1, row < standings.Count ? standings[row].Score : 0));
                yield return new WaitForSeconds(entranceStagger);
            }
        }

        // Grows from nothing past its size and settles back.
        private IEnumerator Pop(RectTransform piece)
        {
            Vector3 rest = rests.TryGetValue(piece, out Rest place) ? place.Scale : Vector3.one;
            for (float t = 0f; t < popSeconds; t += Time.deltaTime)
            {
                piece.localScale = rest * EaseOutBack(t / popSeconds);
                yield return null;
            }

            piece.localScale = rest;
            arrivedAt[piece] = Time.time;
        }

        // Slides in from the left while it fades in, running a little past its place before settling,
        // and once there counts its score up. rank: the row's place in the standings (1 is the winner).
        private IEnumerator SlideIn(RectTransform piece, TMP_Text score, int rank, int points)
        {
            Vector2 rest = rests.TryGetValue(piece, out Rest place) ? place.Position : piece.anchoredPosition;
            CanvasGroup fade = FadeOf(piece);
            for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
            {
                float u = t / slideSeconds;
                piece.anchoredPosition = rest + Vector2.left * (slideDistance * (1f - EaseOutBack(u)));
                fade.alpha = Mathf.Clamp01(u * 2.5f);
                yield return null;
            }

            piece.anchoredPosition = rest;
            fade.alpha = 1f;
            arrivedAt[piece] = Time.time;
            yield return CountUp(score, points);
            ScoreCounted?.Invoke(rank);
        }

        // The row's own fader, added the first time it is needed.
        private static CanvasGroup FadeOf(RectTransform piece)
        {
            if (piece.TryGetComponent(out CanvasGroup fade)) return fade;

            fade = piece.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.interactable = false;
            return fade;
        }

        private bool IsInEntrance(RectTransform piece)
        {
            return entrance != null && System.Array.IndexOf(entrance, piece) >= 0;
        }

        // Puts the winner at the stand's spot moved by its offset, as the place it comes in to and
        // sways around (when it slides in or sways) or straight away (when it does neither).
        private void StandOnSpot(RectTransform piece, Vector2 offset)
        {
            Rest spot = winnerSpot.Moved(offset);
            if (rests.ContainsKey(piece)) rests[piece] = spot;
            else Place(piece, spot);
        }

        private static void Place(RectTransform piece, Rest rest)
        {
            piece.anchoredPosition = rest.Position;
            piece.localScale = rest.Scale;
            piece.localRotation = rest.Rotation;
        }

        private IEnumerator CountUp(TMP_Text label, int score)
        {
            if (label == null) yield break;

            shownScores[label] = 0;
            for (float t = 0f; t < countUpSeconds; t += Time.deltaTime)
            {
                float u = t / countUpSeconds;
                ShowScore(label, Mathf.RoundToInt(score * (1f - (1f - u) * (1f - u))), u);
                yield return null;
            }

            ShowScore(label, score, 1f);
        }

        private void ShowScore(TMP_Text label, int score, float progress)
        {
            label.text = string.Format(scoreFormat, score);
            if (shownScores.TryGetValue(label, out int shown) && shown == score) return;

            shownScores[label] = score;
            ScoreTicked?.Invoke(progress);
        }

        private int RowIndexOf(RectTransform piece)
        {
            for (int i = 0; i < Count(rows); i++)
            {
                if (rows[i].Root != null && rows[i].Root.transform == piece) return i;
            }

            return -1;
        }

#if UNITY_EDITOR
        // Shows the look ticked Preview On Stand on the winner's stand, in the editor only, whenever
        // a value here changes (after the change, as pictures cannot be resized during it).
        private void OnValidate()
        {
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += ShowPreview;
        }

        private void ShowPreview()
        {
            if (this == null || Application.isPlaying || winner == null || looks == null) return;

            Look look = Array.Find(looks, candidate => candidate != null && candidate.PreviewOnStand);
            if (look == null || !winner.TryGetComponent(out Image picture)) return;

            Sprite first = FirstFrame(look.Animations, DanceState);
            if (first == null) return;

            picture.sprite = first;
            picture.SetNativeSize();
            picture.rectTransform.sizeDelta *= look.Size;
            picture.rectTransform.anchoredPosition = standSpot + look.WinnerOffset;
        }

        // The first picture of the controller's clip for state (named like it, e.g. Chiwawa_Dance).
        private static Sprite FirstFrame(RuntimeAnimatorController controller, string state)
        {
            if (controller == null) return null;

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip == null || !clip.name.EndsWith(state, StringComparison.Ordinal)) continue;

                foreach (UnityEditor.EditorCurveBinding binding in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    UnityEditor.ObjectReferenceKeyframe[] keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keys.Length > 0 && keys[0].value is Sprite sprite) return sprite;
                }
            }

            return null;
        }
#endif

        private Look LookOf(string character)
        {
            if (looks == null) return null;
            foreach (Look look in looks)
            {
                if (look != null && look.Character == character) return look;
            }

            return null;
        }

        private static int Count<T>(T[] items)
        {
            return items != null ? items.Length : 0;
        }

        // Runs a little past 1 before settling on it.
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float back = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }
    }
}
