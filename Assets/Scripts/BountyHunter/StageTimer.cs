using System;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The stage banner on the wall: "STAGE 1-1" over a mm:ss countdown. Counts down from Play
    // (stage 1) and restarts whenever a new stage or round begins (BountyRounds calls StartStage);
    // the title rolls up to the new number. Stops at 00:00 and raises TimeUp. The last seconds turn the time red so the players notice.
    public sealed class StageTimer : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text time;
        [SerializeField, Min(1f)] private float stageSeconds = 300f;
        // {0} is the stage (map) number, {1} the round within it.
        [SerializeField] private string titleFormat = "STAGE {0}-{1}";
        [SerializeField, Min(0f)] private float warningSeconds = 10f;
        [SerializeField] private Color warningColor = new Color(1f, 0.36f, 0.26f, 1f);
        // How long the title takes to roll to a new stage number.
        [SerializeField, Min(0f)] private float titleRollSeconds = 0.45f;

        private float remaining;
        private bool running;
        private int shownSeconds = -1;
        private Color normalColor;
        private bool normalColorRead;

        public float Remaining => remaining;
        // How long the title takes to roll to a new stage number.
        public float TitleRollSeconds => titleRollSeconds;

        public event Action TimeUp;

        private void Start()
        {
            if (!running && remaining <= 0f) StartStage(1);
        }

        public void StartStage(int stage, int round = 1)
        {
            if (title != null) RollingLabel.On(title).Roll(string.Format(titleFormat, stage, round), titleRollSeconds);
            remaining = stageSeconds;
            running = true;
            shownSeconds = -1;
            Show();
        }

        private void Update()
        {
            if (!running) return;

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            Show();
            if (remaining > 0f) return;

            running = false;
            Debug.Log("[StageTimer] Time is up", this);
            TimeUp?.Invoke();
        }

        // Rounds up, so the display reads 00:01 until the very last moment and 00:00 only at the end.
        private void Show()
        {
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == shownSeconds || time == null) return;

            shownSeconds = seconds;
            // Remembers the text colour from the prefab before the warning colour can replace it.
            if (!normalColorRead)
            {
                normalColor = time.color;
                normalColorRead = true;
            }

            time.text = $"{seconds / 60:00}:{seconds % 60:00}";
            time.color = seconds <= warningSeconds ? warningColor : normalColor;
        }
    }
}
