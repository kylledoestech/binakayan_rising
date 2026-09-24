using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.Gameplay.Meta;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Command-line probes for the evaluation targets in proposal Table 5: frame rate in combat
    /// (<c>-brFps file</c>) and zero corrupted saves (<c>-brSaveStress</c>). Inert without the flags.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>-brFps out.txt</c> records every frame while a battle is on the board and writes the
    /// average, the 1% low and the worst frame when the player quits. Run it with a shot route
    /// that plays a battle to the end, e.g. <c>Tools/qa/fps.sh</c>.
    /// </para>
    /// <para>
    /// <c>-brSaveStress -brSaveDir dir</c> loads the save in <c>dir</c>, reports whether it read
    /// cleanly, then rewrites it every frame until the process is killed. <c>Tools/qa/save-stress.sh</c>
    /// kills it with SIGKILL at random moments and relaunches, so every load after the first
    /// checks a save that was interrupted mid-write.
    /// </para>
    /// </remarks>
    public sealed class QaProbes : MonoBehaviour
    {
        private const int WarmupFrames = 60;

        private string fpsPath;
        private readonly List<float> frames = new List<float>(20000);
        private int battleFrames;
        private BattlePlaytest battle;
        private float nextLookup;

        private SaveStore stress;
        private SaveData stressData;
        private int stressWrites;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            bool fps = !string.IsNullOrEmpty(CommandLine.Value("-brFps"));
            bool saves = CommandLine.Has("-brSaveStress");
            if (!fps && !saves)
            {
                return;
            }

            var host = new GameObject("QA Probes");
            DontDestroyOnLoad(host);
            QaProbes probes = host.AddComponent<QaProbes>();
            if (fps)
            {
                probes.fpsPath = CommandLine.Value("-brFps");
            }

            if (saves)
            {
                probes.BeginSaveStress();
            }
        }

        // ------------------------------------------------------------------ frame rate

        private void Update()
        {
            if (stress != null)
            {
                stressData.reales = ++stressWrites;
                if (!stress.Save(stressData))
                {
                    Debug.LogError("[SaveStress] write failed at " + stressWrites);
                }
                else if (stressWrites % 50 == 0)
                {
                    Debug.Log("[SaveStress] wrote " + stressWrites);
                }
            }

            if (fpsPath == null)
            {
                return;
            }

            if (battle == null && Time.unscaledTime >= nextLookup)
            {
                battle = FindAnyObjectByType<BattlePlaytest>();
                nextLookup = Time.unscaledTime + 0.5f;
                battleFrames = 0;
            }

            if (battle != null && ++battleFrames > WarmupFrames)
            {
                frames.Add(Time.unscaledDeltaTime);
            }
        }

        private void OnApplicationQuit()
        {
            if (fpsPath == null)
            {
                return;
            }

            File.WriteAllText(fpsPath, FpsReport(frames));
            Debug.Log("[Fps] report written to " + fpsPath);
        }

        /// <summary>Average, 1% low and worst frame, from per-frame durations in seconds.</summary>
        public static string FpsReport(IList<float> durations)
        {
            var text = new StringBuilder();
            text.AppendLine("gpu: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")");
            text.AppendLine("cpu: " + SystemInfo.processorType + ", " + SystemInfo.processorCount + " threads");
            text.AppendLine("ram: " + SystemInfo.systemMemorySize + " MB");
            text.AppendLine("resolution: " + Screen.width + "x" + Screen.height + ", vsync " + QualitySettings.vSyncCount + ", target " + Application.targetFrameRate);
            if (durations.Count == 0)
            {
                text.AppendLine("frames: 0 (no battle was on screen)");
                return text.ToString();
            }

            var sorted = new List<float>(durations);
            sorted.Sort();
            double total = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                total += sorted[i];
            }

            int slowest = Math.Max(1, sorted.Count / 100);
            double slowTotal = 0;
            for (int i = sorted.Count - slowest; i < sorted.Count; i++)
            {
                slowTotal += sorted[i];
            }

            text.AppendLine("frames: " + sorted.Count + " over " + total.ToString("0.0") + " s");
            text.AppendLine("average fps: " + (sorted.Count / total).ToString("0.0"));
            text.AppendLine("1% low fps: " + (slowest / slowTotal).ToString("0.0"));
            text.AppendLine("worst frame: " + (sorted[sorted.Count - 1] * 1000f).ToString("0.0") + " ms");
            return text.ToString();
        }

        // ------------------------------------------------------------------ saves

        private void BeginSaveStress()
        {
            string dir = CommandLine.Value("-brSaveDir");
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogError("[SaveStress] refusing to run without -brSaveDir.");
                Application.Quit(2);
                return;
            }

            stress = new SaveStore(dir);
            MetaRules rules = MetaRules.Default();
            bool existed = stress.Exists;
            SaveData loaded = stress.Load(rules);
            string verdict = loaded != null ? "ok" : existed ? "FAIL" : "none";
            Debug.Log("[SaveStress] boot load=" + verdict
                + " writes=" + (loaded != null ? loaded.reales : 0)
                + " backup=" + stress.LastLoadUsedBackup
                + " problem=" + (string.IsNullOrEmpty(stress.LastProblem) ? "-" : stress.LastProblem.Trim()));

            stressData = loaded ?? new SaveData();
            stressData.Repair(rules);
            stressWrites = stressData.reales;
            Debug.Log("[SaveStress] saving");
        }
    }
}
