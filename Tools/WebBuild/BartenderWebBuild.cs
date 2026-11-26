// This file is copied into Assets/Editor ONLY in the disposable Mac build project.
// The original project, packages, scenes, prefabs and Inspector references are not edited.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

public static class BartenderWebBuild
{
    public static void Build()
    {
        try { BuildPlayer(); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void BuildPlayer()
    {
        string output = Argument("-a2WebOutput");
        if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output))
            throw new InvalidOperationException("An absolute -a2WebOutput path is required.");
        const string scene = "Assets/How to be a bartender.unity";
        if (!File.Exists(scene)) throw new FileNotFoundException("The bartender scene is missing.", scene);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Web Build Support is not installed in this Unity Editor. Nothing was installed automatically.");
        if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length != 0)
            throw new InvalidOperationException("The build output must be a new or empty directory.");
        Directory.CreateDirectory(output);
        string diagnostics = Path.Combine(Directory.GetParent(output).FullName, "player-assemblies.txt");
        var inventory = new StringBuilder();
        foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            inventory.AppendLine(assembly.name + "\t" + assembly.outputPath);
        File.WriteAllText(diagnostics, inventory.ToString());

        const string releaseTitle = "How to be a Bartender";
        string savedTitle = PlayerSettings.productName;
        WebGLCompressionFormat savedCompression = PlayerSettings.WebGL.compressionFormat;
        bool savedFallback = PlayerSettings.WebGL.decompressionFallback;
        BuildReport report;
        try
        {
            PlayerSettings.productName = releaseTitle;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.WebGL,
                // Test assemblies are not requested for this playable release.
                options = BuildOptions.CleanBuildCache | BuildOptions.DetailedBuildReport
            };
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            PlayerSettings.productName = savedTitle;
            PlayerSettings.WebGL.compressionFormat = savedCompression;
            PlayerSettings.WebGL.decompressionFallback = savedFallback;
        }
        if (report == null || report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
            throw new InvalidOperationException("Web build failed. See the complete Unity build log; an empty folder is not a successful build.");
        string build = Path.Combine(output, "Build");
        if (!File.Exists(Path.Combine(output, "index.html")) || !Directory.Exists(build)
            || Directory.GetFiles(build, "*.loader.js").Length == 0
            || Directory.GetFiles(build, "*.wasm").Length == 0
            || Directory.GetFiles(build, "*.data").Length == 0
            || Directory.GetFiles(build, "*.framework.js").Length == 0)
            throw new InvalidOperationException("Unity reported success, but the expected Web files are missing.");
        File.WriteAllText(Path.Combine(output, "A2-build-result.txt"),
            "Result: Succeeded\nProduct: " + releaseTitle + "\nUnity: " + Application.unityVersion + "\nScene: " + scene
            + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings
            + "\nBytes: " + report.summary.totalSize + "\n");
        Debug.Log("A2_WEB_BUILD_SUCCEEDED: " + output);
    }

    private static string Argument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < arguments.Length; index++)
            if (arguments[index] == name) return arguments[index + 1];
        return null;
    }
}
#endif
