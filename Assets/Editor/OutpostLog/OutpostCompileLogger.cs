#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Menulis hasil kompilasi (error/warning) ke BuilderStatus/compile.txt agar mudah dicek tanpa membuka Console.
[InitializeOnLoad]
public static class OutpostCompileLogger
{
    static readonly StringBuilder sb = new StringBuilder();

    static OutpostCompileLogger()
    {
        CompilationPipeline.assemblyCompilationFinished -= OnAssembly;
        CompilationPipeline.assemblyCompilationFinished += OnAssembly;
        CompilationPipeline.compilationFinished -= OnAll;
        CompilationPipeline.compilationFinished += OnAll;
    }

    static void OnAssembly(string assemblyPath, CompilerMessage[] messages)
    {
        foreach (var m in messages)
        {
            if (m.type == CompilerMessageType.Error)
                sb.AppendLine("ERROR " + Path.GetFileName(assemblyPath) + " " + m.file + "(" + m.line + "): " + m.message);
        }
    }

    static void OnAll(object ctx)
    {
        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuilderStatus"));
            Directory.CreateDirectory(dir);
            string body = sb.Length == 0 ? "COMPILE OK" : sb.ToString();
            File.WriteAllText(Path.Combine(dir, "compile.txt"), DateTime.Now.ToString("HH:mm:ss") + "\n" + body);
        }
        catch (Exception) { }
        sb.Clear();
    }
}
#endif
