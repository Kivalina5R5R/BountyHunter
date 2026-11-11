using System;
using TMPro;
using UnityEngine;

namespace BountyHunter.Thieves
{
    // The stage banner on the wall: "STAGE 1-1" over a mm:ss countdown. Counts down from Play
    // (stage 1); BountyRounds calls StartStage for every new round and the title rolls up to the
    // new number, but the clock only starts over when the stage number itself changes (a new map), so
    // all the rounds of a stage share its countdown. Stops at 00:00 and raises TimeUp. The last
    // seconds turn the time red so the players notice.
    public sealed class StageTimer : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text time;
        // Each stage's clock, stage 1 first; a stage past the end of the list uses the last one.
        [SerializeField] private float[] secondsPerStage = { 160f, 170f, 180f };
        // {0} is the stage (map) number, {1} the round within it.
        [SerializeField] private string titleFormat = "STAGE {0}-{1}";
        [SerializeField, Min(0f)] private float warningSeconds = 10f;
        [SerializeField] private Color warningColor = new Color(1f, 0.36f, 0.26f, 1f);
        // How long the title takes to roll to a new stage number.
        [SerializeField, Min(0f)] private float titleRollSeconds = 0.45f;

        private float remaining;
        private bool running;
        // The stage the clock is counting for; 0 before the first.
        private int clockStage;
        private int shownSeconds = -1;
        private Color normalColor;
        private bool normalColorRead;

        public float Remaining => remaining;
        // How long the title takes to roll to a new stage number.
        public float TitleRollSeconds => titleRollSeconds;

        public event Action TimeUp;
        // Each whole second shown within the last warningSeconds, while the clock runs (for its tick).
        public event Action<int> WarningSecond;

        private void Start()
        {
            if (!running && remaining <= 0f) StartStage(1);
        }

        public void StartStage(int stage, int round = 1)
        {
            if (title != null) RollingLabel.On(title).Roll(string.Format(titleFormat, stage, round), titleRollSeconds);
            // A new round of the same stage keeps the clock going.
            if (stage == clockStage) return;

            clockStage = stage;
            remaining = SecondsFor(stage);
            running = true;
            shownSeconds = -1;
            Show();
        }

        // Shows the stage's title and its full time without starting the clock, for the rest before
        // it (WallTutorial); StartStage then starts it.
        public void ShowStage(int stage)
        {
            if (title != null) RollingLabel.On(title).Roll(string.Format(titleFormat, stage, 1), titleRollSeconds);
            running = false;
            // Forgotten, so StartStage starts the clock afresh after the rest.
            clockStage = 0;
            remaining = SecondsFor(stage);
            shownSeconds = -1;
            Show();
        }

        // Shows heading in place of the stage title and counts down from seconds, for a part of the
        // game that is not a stage (the boss chase). Raises TimeUp at the end like a stage.
        public void StartCountdown(string heading, float seconds)
        {
            if (title != null) RollingLabel.On(title).Roll(heading, titleRollSeconds);
            clockStage = -1;
            remaining = seconds;
            running = true;
            shownSeconds = -1;
            Show();
        }

        // Rolls the title to heading without touching the clock.
        public void SetHeading(string heading)
        {
            if (title != null) RollingLabel.On(title).Roll(heading, titleRollSeconds);
        }

        private float SecondsFor(int stage)
        {
            const float Fallback = 180f;
            if (secondsPerStage == null || secondsPerStage.Length == 0) return Fallback;

            int index = Mathf.Clamp(stage - 1, 0, secondsPerStage.Length - 1);
            return Mathf.Max(1f, secondsPerStage[index]);
        }

        // Stops the clock where it is, without raising TimeUp (a mini game between stages); the next
        // stage starts it over.
        public void Stop()
        {
            running = false;
            clockStage = 0;
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
            if (running && seconds > 0 && seconds <= warningSeconds) WarningSecond?.Invoke(seconds);
            time.color = seconds <= warningSeconds ? warningColor : normalColor;
        }
    }
}
