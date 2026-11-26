// Isolated API shims: validates control flow, never compiles or runs Unity.
using System;
using System.IO;
using UnityEditor;
public static class Program {
 public static int Main(string[] args) {
  string output=args[1];Directory.CreateDirectory("Assets");File.WriteAllText("Assets/How to be a bartender.unity","fixture");
  foreach(string scenario in new[]{"FailedReport","MissingWasm","ThrownException","UnsupportedTarget","Success"}) {
   BuildPipeline.Scenario=scenario;EditorApplication.ExitCode=0;PlayerSettings.productName="Original tutorial title";
   PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;PlayerSettings.WebGL.decompressionFallback=true;
   BartenderWebBuild.Build();
   bool success=scenario=="Success";
   if((EditorApplication.ExitCode==0)!=success)throw new Exception("Incorrect exit: "+scenario);
   if(PlayerSettings.productName!="Original tutorial title")throw new Exception("Product name not restored");
   if(PlayerSettings.WebGL.compressionFormat!=WebGLCompressionFormat.Brotli || !PlayerSettings.WebGL.decompressionFallback)throw new Exception("Settings not restored");
   if(success && !File.ReadAllText(Path.Combine(output,"A2-build-result.txt")).Contains("Product: How to be a Bartender"))throw new Exception("Success manifest missing");
   if(Directory.Exists(output))Directory.Move(output,output+"-"+scenario);
   Console.WriteLine("PASS: builder control flow "+scenario);
  }
  return 0;
 }
}
namespace UnityEngine {
 public static class Application {public static bool isBatchMode=true;public static string unityVersion="test-shim";}
 public static class Debug {public static void Log(object v){Console.WriteLine(v);}public static void LogException(Exception e){Console.WriteLine("Expected: "+e.Message);}}
}
namespace UnityEditor {
 public enum WebGLCompressionFormat {Disabled,Gzip,Brotli}
 public enum BuildTargetGroup {WebGL}
 public enum BuildTarget {WebGL}
 [Flags] public enum BuildOptions {None=0,CleanBuildCache=1,DetailedBuildReport=2,IncludeTestAssemblies=4}
 public struct BuildPlayerOptions {public string[] scenes;public string locationPathName;public BuildTarget target;public BuildOptions options;}
 public static class PlayerSettings {public static string productName;public static class WebGL {public static WebGLCompressionFormat compressionFormat;public static bool decompressionFallback;}}
 public static class EditorApplication {public static int ExitCode;public static void Exit(int code){ExitCode=code;}}
 public static class BuildPipeline {
  public static string Scenario;
  public static bool IsBuildTargetSupported(BuildTargetGroup g,BuildTarget t)=>Scenario!="UnsupportedTarget";
  public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions opts){
   if(opts.scenes.Length!=1 || opts.scenes[0]!="Assets/How to be a bartender.unity" || !opts.options.HasFlag(BuildOptions.CleanBuildCache) || opts.options.HasFlag(BuildOptions.IncludeTestAssemblies))throw new Exception("Unsafe build options");
   if(PlayerSettings.WebGL.compressionFormat!=WebGLCompressionFormat.Disabled || PlayerSettings.WebGL.decompressionFallback)throw new Exception("Wrong upload settings");
   if(PlayerSettings.productName!="How to be a Bartender")throw new Exception("Wrong release title");
   if(Scenario=="ThrownException")throw new Exception("Simulated build exception");
   var report=new UnityEditor.Build.Reporting.BuildReport();
   report.summary.result=Scenario=="FailedReport"?UnityEditor.Build.Reporting.BuildResult.Failed:UnityEditor.Build.Reporting.BuildResult.Succeeded;
   report.summary.totalErrors=Scenario=="FailedReport"?1:0;
   Directory.CreateDirectory(Path.Combine(opts.locationPathName,"Build"));File.WriteAllText(Path.Combine(opts.locationPathName,"index.html"),"fixture");
   foreach(string file in new[]{"game.loader.js","game.data","game.framework.js","game.wasm"})
    if(file!="game.wasm" || Scenario!="MissingWasm")File.WriteAllText(Path.Combine(opts.locationPathName,"Build",file),"fixture");
   return report;
  }
 }
}
namespace UnityEditor.Build.Reporting {
 public enum BuildResult {Succeeded,Failed}
 public class BuildSummary {public BuildResult result;public int totalErrors,totalWarnings;public ulong totalSize;}
 public class BuildReport {public BuildSummary summary=new BuildSummary();}
}
namespace UnityEditor.Compilation {
 public enum AssembliesType {Player}
 public class Assembly {public string name="Assembly-CSharp",outputPath="isolated/Assembly-CSharp.dll";}
 public static class CompilationPipeline {public static Assembly[] GetAssemblies(AssembliesType t)=>new[]{new Assembly()};}
}
