using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fortal.Arena
{
    // Fortal Arena Remote: lets the arena's web launcher open / close this game's tutorial (and change its language).
    //   web launcher -UDP-> game-launcher-gui -> 127.0.0.1:19790 -> this script -> the game
    //   this script -> 127.0.0.1:19780 -> game-launcher-gui -> web launcher  (the game says what state it is in)
    // It starts by itself with the game. Connect the game through FortalArenaRemoteBridge (no code), or set the Func / Action
    // fields below from your own script. See README_TH.md.
    public sealed class FortalArenaRemote : MonoBehaviour
    {
        public const int GamePort = 19790, LauncherPort = 19780;
        const float ReportEverySeconds = 2f, CheckEverySeconds = .25f;
        static readonly byte[] Header = Encoding.ASCII.GetBytes("RMOT");

        // ---- connect these to the game (all run on Unity's main thread) ----
        public static Func<bool> IsTutorialOpen;     // is the tutorial showing right now?
        public static Func<bool> CanOpenTutorial;    // may it open right now? (normally: only at the menu)
        public static Action OpenTutorial;
        public static Action CloseTutorial;
        // Optional, for a game with Thai and English. Leave them null if the game has one language.
        public static Func<string> GetLanguage;      // "th" or "en"
        public static Action<string> SetLanguage;    // "th" or "en"
        // false: do not listen on 19790, because something else in the game already does (see README_TH.md, "ชนพอร์ต").
        // Set it before the first scene loads, then pass that listener's messages to HandleMessage.
        public static bool ListenOnPort = true;

        static FortalArenaRemote instance;
        UdpClient listen, send;
        Thread reader;
        readonly ConcurrentQueue<KeyValuePair> inbox = new ConcurrentQueue<KeyValuePair>();
        float reportIn, checkIn;
        string lastReport;

        struct KeyValuePair { public string action, payload; }
        [Serializable] class TutorialMessage { public bool open; }
        [Serializable] class LanguageMessage { public string lang; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            IsTutorialOpen = CanOpenTutorial = null;
            OpenTutorial = CloseTutorial = null;
            GetLanguage = null;
            SetLanguage = null;
            ListenOnPort = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            // The launcher's commands must work while the game window is not in front.
            Application.runInBackground = true;
            var go = new GameObject("Fortal Arena Remote");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FortalArenaRemote>();
        }

        // For a game that already listens on 19790: pass it what arrives there ("tutorial" + {"open":true}, ...). Main thread.
        public static void HandleMessage(string action, string payload)
        {
            if (instance != null) instance.Handle(action, payload);
        }

        void OnEnable()
        {
            send = new UdpClient(AddressFamily.InterNetwork);
            IgnorePortUnreachable(send);
            if (ListenOnPort)
            {
                try
                {
                    listen = new UdpClient(new IPEndPoint(IPAddress.Loopback, GamePort));
                    IgnorePortUnreachable(listen);
                    reader = new Thread(Read) { IsBackground = true, Name = "FortalArenaRemote" };
                    reader.Start();
                    Debug.Log("[FortalArenaRemote] listening on 127.0.0.1:" + GamePort);
                }
                catch (SocketException e)
                {
                    listen = null;
                    Debug.LogWarning("[FortalArenaRemote] UDP " + GamePort + " is already taken (" + e.SocketErrorCode + "): the launcher cannot reach this game. See README_TH.md");
                }
            }
            reportIn = 0;
        }

        void OnDisable()
        {
            if (listen != null) { listen.Close(); listen = null; }
            if (send != null) { send.Close(); send = null; }
            reader = null;
        }

        // Windows reports an earlier "port unreachable" as an error on the socket; UDP should not care.
        static void IgnorePortUnreachable(UdpClient client)
        {
            try { client.Client.IOControl(unchecked((int)0x9800000C), new byte[] { 0 }, null); } catch (Exception) { }
        }

        void Read()
        {
            var socket = listen;
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (socket != null && socket == listen)
            {
                byte[] data;
                try { data = socket.Receive(ref from); }
                catch (SocketException) { continue; }
                catch (Exception) { return; }
                string action, payload;
                if (TryParse(data, out action, out payload)) inbox.Enqueue(new KeyValuePair { action = action, payload = payload });
            }
        }

        public static bool TryParse(byte[] data, out string action, out string payload)
        {
            action = payload = "";
            if (data == null || data.Length < 4) return false;
            for (int i = 0; i < 4; i++) if (data[i] != Header[i]) return false;
            var body = Encoding.UTF8.GetString(data, 4, data.Length - 4).TrimStart();
            int space = body.IndexOf(' ');
            action = space < 0 ? body : body.Substring(0, space);
            payload = space < 0 ? "" : body.Substring(space + 1);
            return action.Length > 0;
        }

        void Update()
        {
            KeyValuePair message;
            while (inbox.TryDequeue(out message)) Handle(message.action, message.payload);
            reportIn -= Time.unscaledDeltaTime;
            checkIn -= Time.unscaledDeltaTime;
            if (checkIn > 0f && lastReport != null && reportIn > 0f) return;
            checkIn = CheckEverySeconds;
            var report = Report();
            if (report != lastReport || reportIn <= 0f) Send(report);
        }

        // A state, not a toggle: the launcher repeats a command until the report confirms it, so a repeat must change nothing.
        void Handle(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "tutorial":
                        bool open = JsonUtility.FromJson<TutorialMessage>(payload).open;
                        bool isOpen = IsTutorialOpen != null && IsTutorialOpen();
                        if (open == isOpen) break;
                        if (!open) { if (CloseTutorial != null) CloseTutorial(); }
                        else if (CanOpenTutorial != null && CanOpenTutorial() && OpenTutorial != null) OpenTutorial();
                        break;
                    case "language":
                        var lang = JsonUtility.FromJson<LanguageMessage>(payload).lang;
                        if (SetLanguage != null && (lang == "th" || lang == "en")) SetLanguage(lang);
                        break;
                    case "arena_status":
                        break;
                    default:
                        return;   // other systems' messages (dual lane, players...)
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            lastReport = null;    // report right away
        }

        static bool Ask(Func<bool> question)
        {
            try { return question != null && question(); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }

        string Report()
        {
            bool tutorial = Ask(IsTutorialOpen);
            bool allowed = !tutorial && Ask(CanOpenTutorial);
            var sb = new StringBuilder("{\"type\":\"arena\",\"data\":{\"scene\":\"");
            sb.Append(SceneManager.GetActiveScene().name.Replace("\"", ""));
            sb.Append("\",\"tutorial\":").Append(tutorial ? "true" : "false");
            sb.Append(",\"tutorialAllowed\":").Append(allowed ? "true" : "false");
            if (GetLanguage != null)
            {
                string lang = null;
                try { lang = GetLanguage(); } catch (Exception e) { Debug.LogException(e); }
                if (lang == "th" || lang == "en") sb.Append(",\"lang\":\"").Append(lang).Append('"');
            }
            return sb.Append("}}").ToString();
        }

        void Send(string report)
        {
            lastReport = report;
            reportIn = ReportEverySeconds;
            if (send == null) return;
            var bytes = Encoding.UTF8.GetBytes("RMOTgame_data " + report);
            try { send.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, LauncherPort)); }
            catch (Exception) { /* no launcher on this PC: fine */ }
        }
    }
}
