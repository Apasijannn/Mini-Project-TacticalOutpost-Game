#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Pengawas file untuk pipeline otomatis: mendeteksi script baru (refresh walau jendela tidak fokus)
// dan menjalankan perintah dari BuilderStatus/request.txt ("build", "preview <nama...>", "nav").
[InitializeOnLoad]
public static class OutpostWatcher
{
    static double nextPoll, nextBeat;
    static DateTime lastScan;
    static bool busy;

    static string StatusDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus")); } }

    static OutpostWatcher()
    {
        lastScan = Newest();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Write("watcher.txt", "alive " + DateTime.Now.ToString("HH:mm:ss"));
    }

    static void Write(string file, string text)
    {
        try { Directory.CreateDirectory(StatusDir); File.WriteAllText(Path.Combine(StatusDir, file), text); } catch { }
    }

    static DateTime Newest()
    {
        DateTime n = DateTime.MinValue;
        try
        {
            foreach (var sub in new[] { "Editor", "Outpost" })
            {
                string dir = Path.Combine(Application.dataPath, sub);
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > n) n = t;
                }
            }
        }
        catch { }
        return n;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.timeSinceStartup > nextBeat)
        {
            nextBeat = EditorApplication.timeSinceStartup + 10.0;
            Write("watcher.txt", "alive " + DateTime.Now.ToString("HH:mm:ss"));
        }
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

        var newest = Newest();
        if (newest > lastScan)
        {
            lastScan = newest;
            AssetDatabase.Refresh();
            return;
        }

        string req = Path.Combine(StatusDir, "request.txt");
        if (!File.Exists(req)) return;
        string cmd;
        try { cmd = File.ReadAllText(req).Trim(); File.Delete(req); } catch { return; }
        if (string.IsNullOrEmpty(cmd)) return;
        Run(cmd);
    }

    static void Run(string cmd)
    {
        busy = true;
        string name = cmd, args = "";
        int sp = cmd.IndexOf(' ');
        if (sp > 0) { name = cmd.Substring(0, sp); args = cmd.Substring(sp + 1).Trim(); }
        string method = name == "build" ? "BuildAll" : name == "preview" ? "RenderPreviews" : name == "nav" ? "ValidateOnly" : name == "run" ? "RunCustom" : null;
        string result;
        try
        {
            if (method == null) throw new Exception("perintah tidak dikenal: " + cmd);
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("OutpostBuilder");
                if (t != null) break;
            }
            if (t == null) throw new Exception("OutpostBuilder belum ter-compile");
            var mi = t.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) throw new Exception("method tidak ada: " + method);
            Write("result.txt", "RUNNING " + cmd + " " + DateTime.Now.ToString("HH:mm:ss"));
            var ps = mi.GetParameters();
            object r = mi.Invoke(null, ps.Length == 1 ? new object[] { args } : null);
            result = "DONE " + cmd + " " + DateTime.Now.ToString("HH:mm:ss") + (r != null ? "\n" + r : "");
        }
        catch (Exception e)
        {
            var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            result = "ERROR " + cmd + "\n" + ie.GetType().Name + ": " + ie.Message + "\n" + ie.StackTrace;
        }
        Write("result.txt", result);
        busy = false;
    }
}
#endif
