using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Fortal.Arena
{
    // Game settings from the arena web launcher (ADMIN > เกม > ตั้งค่าในเกม, kit 3.0 / 2026-11-12):
    // sound (all / music / effects / mute), brightness and picture quality. FortalArenaRemote receives the "settings" message and
    // calls Set; values are kept in PlayerPrefs, so the game opens with them even when no launcher is around.
    //
    //   all sound  : AudioListener.volume, works by itself in every game
    //   brightness : a black layer over every display, works by itself (1 = off, 0.4 = darkest allowed)
    //   quality    : QualitySettings level index, works by itself (-1 = leave the build's own)
    //   music / sfx: the game must use them. No code: FortalArenaRemoteBridge (Audio Mixer parameters or AudioSource lists).
    //                With code: multiply your volumes by Music / Sfx and listen to Changed. See README_TH.md.
    public static class FortalArenaSettings
    {
        public static float Master { get; private set; } = 1f;
        public static float Music { get; private set; } = 1f;
        public static float Sfx { get; private set; } = 1f;
        public static bool Mute { get; private set; }
        public static float Brightness { get; private set; } = 1f;
        public static int Quality { get; private set; } = -1;
        /// <summary>Fired on the main thread after any change (and once at start), so code can set its volumes again.</summary>
        public static event Action Changed;

        public const float MinBrightness = .4f;
        const string Key = "FortalArenaSettings.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Changed = null; Master = Music = Sfx = Brightness = 1f; Mute = false; Quality = -1; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Load()
        {
            Master = Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "master", 1f));
            Music = Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "music", 1f));
            Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(Key + "sfx", 1f));
            Mute = PlayerPrefs.GetInt(Key + "mute", 0) == 1;
            Brightness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "brightness", 1f), MinBrightness, 1f);
            Quality = PlayerPrefs.GetInt(Key + "quality", -1);
            Apply();
        }

        /// <summary>The "settings" message: only the fields that are sent change (-1 = not sent; quality -2 = not sent, -1 = the build's own).</summary>
        [Serializable]
        public class Message { public float master = -1, music = -1, sfx = -1, brightness = -1; public int mute = -1, quality = -2; }

        public static void Set(Message m)
        {
            if (m == null) return;
            if (m.master >= 0) Master = Mathf.Clamp01(m.master);
            if (m.music >= 0) Music = Mathf.Clamp01(m.music);
            if (m.sfx >= 0) Sfx = Mathf.Clamp01(m.sfx);
            if (m.mute >= 0) Mute = m.mute == 1;
            if (m.brightness >= 0) Brightness = Mathf.Clamp(m.brightness, MinBrightness, 1f);
            if (m.quality >= -1) Quality = m.quality < QualitySettings.names.Length ? m.quality : -1;
            PlayerPrefs.SetFloat(Key + "master", Master);
            PlayerPrefs.SetFloat(Key + "music", Music);
            PlayerPrefs.SetFloat(Key + "sfx", Sfx);
            PlayerPrefs.SetInt(Key + "mute", Mute ? 1 : 0);
            PlayerPrefs.SetFloat(Key + "brightness", Brightness);
            PlayerPrefs.SetInt(Key + "quality", Quality);
            PlayerPrefs.Save();   // now: the launcher may close the game with taskkill /F
            Apply();
        }

        static void Apply()
        {
            AudioListener.volume = Mute ? 0f : Master;
            if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality)
                QualitySettings.SetQualityLevel(Quality, true);
            FortalArenaDimmer.Set(Brightness);
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
            {
                try { listener(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>The "settings" object of the report (what the launcher page shows).</summary>
        public static string ReportJson()
        {
            var names = QualitySettings.names;
            var list = new StringBuilder();
            for (int i = 0; i < names.Length; i++) list.Append(i > 0 ? "," : "").Append('"').Append(names[i].Replace("\"", "")).Append('"');
            return "{\"master\":" + Num(Master) + ",\"music\":" + Num(Music) + ",\"sfx\":" + Num(Sfx) + ",\"mute\":" + (Mute ? "true" : "false") +
                   ",\"brightness\":" + Num(Brightness) + ",\"quality\":" + (Quality >= 0 ? Quality : QualitySettings.GetQualityLevel()) +
                   ",\"qualityNames\":[" + list + "]}";
        }

        static string Num(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    }

    // A black layer over every display, alpha = 1 - brightness. Made on first use (never while brightness is 100%); survives scene loads.
    public sealed class FortalArenaDimmer : MonoBehaviour
    {
        static FortalArenaDimmer instance;
        static float level = 1f;
        Image[] layers = new Image[0];

        public static void Set(float brightness)
        {
            level = brightness;
            if (!Application.isPlaying) return;
            if (instance == null)
            {
                if (brightness >= .999f) return;
                var go = new GameObject("Fortal Arena Dimmer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<FortalArenaDimmer>();
            }
            instance.Refresh();
        }

        void Update()
        {
            if (layers.Length < Mathf.Min(Display.displays.Length, 8)) Refresh();   // a display switched on later
        }

        void Refresh()
        {
            int count = Mathf.Min(Display.displays.Length, 8);
            if (layers.Length < count)
            {
                var grown = new Image[count];
                Array.Copy(layers, grown, layers.Length);
                for (int i = layers.Length; i < count; i++)
                {
                    var canvasGo = new GameObject("Dim display " + (i + 1), typeof(Canvas));
                    canvasGo.transform.SetParent(transform, false);
                    var canvas = canvasGo.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 32767;
                    canvas.targetDisplay = i;
                    var imageGo = new GameObject("Layer", typeof(RectTransform), typeof(Image));
                    imageGo.transform.SetParent(canvasGo.transform, false);
                    var rect = (RectTransform)imageGo.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                    var image = imageGo.GetComponent<Image>();
                    image.raycastTarget = false;
                    grown[i] = image;
                }
                layers = grown;
            }
            foreach (var image in layers)
            {
                if (image == null) continue;
                image.color = new Color(0f, 0f, 0f, 1f - level);
                image.enabled = level < .999f;
            }
        }
    }
}
