using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fortal.Arena.EditorTools
{
    // Tools > Fortal > Arena Remote Tester: stands in for the launcher, so the game can be tried in Play mode with nothing
    // else installed. Close game-launcher-gui first if it is open on this PC (it holds port 19780).
    public sealed class FortalArenaRemoteTester : EditorWindow
    {
        UdpClient launcher, sender;
        string report = "", error = "";
        double lastReportAt;

        [MenuItem("Tools/Fortal/Arena Remote Tester")]
        static void Open() { GetWindow<FortalArenaRemoteTester>("Arena Remote"); }

        void OnEnable()
        {
            sender = new UdpClient(AddressFamily.InterNetwork);
            try { launcher = new UdpClient(new IPEndPoint(IPAddress.Loopback, FortalArenaRemote.LauncherPort)); error = ""; }
            catch (SocketException) { launcher = null; error = "Port 19780 is taken: close Game Launcher on this PC, then reopen this window."; }
            EditorApplication.update += Poll;
        }

        void OnDisable()
        {
            EditorApplication.update -= Poll;
            if (launcher != null) launcher.Close();
            if (sender != null) sender.Close();
        }

        void Poll()
        {
            if (launcher == null) return;
            try
            {
                while (launcher.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var text = Encoding.UTF8.GetString(launcher.Receive(ref from));
                    if (text.StartsWith("RMOTgame_data ")) { report = text.Substring(14); lastReportAt = EditorApplication.timeSinceStartup; Repaint(); }
                }
            }
            catch (SocketException) { }
            if (report.Length > 0 && EditorApplication.timeSinceStartup - lastReportAt > 6) { report = ""; Repaint(); }
        }

        void Send(string text)
        {
            var bytes = Encoding.UTF8.GetBytes("RMOT" + text);
            sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, FortalArenaRemote.GamePort));
        }

        void OnGUI()
        {
            if (error.Length > 0) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!Application.isPlaying) EditorGUILayout.HelpBox("Press Play, then use the buttons.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open tutorial")) Send("tutorial {\"open\":true}");
                    if (GUILayout.Button("Close tutorial")) Send("tutorial {\"open\":false}");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Language TH")) Send("language {\"lang\":\"th\"}");
                    if (GUILayout.Button("Language EN")) Send("language {\"lang\":\"en\"}");
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What the game reports (every 2 s and on every change):", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(report.Length > 0 ? report : "(nothing yet)", EditorStyles.wordWrappedLabel, GUILayout.Height(60));
        }
    }
}
