using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
public class UnityAnalyzersUpdater
{
	[MenuItem("Tools/UnityAnalyzersUpdater")]
	static void UpdateUnityAnalyzers()
	{
		var working = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "UnityAnalyzersUpdater"));
		var tmp = Path.Combine(working, "tmp");
		if(Directory.Exists(tmp)) { Directory.Delete(tmp, true); }
		using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
		{
			FileName = GetDotnetPath(),
			Arguments = $"restore --packages \"{tmp}\"",
			CreateNoWindow = true,
			UseShellExecute = false,
			WorkingDirectory = working,
			RedirectStandardError = true,
			StandardErrorEncoding = Encoding.UTF8,
			RedirectStandardOutput = true,
			StandardOutputEncoding = Encoding.UTF8,
		});
		var stdout = proc.StandardOutput.ReadToEnd();
		var stderr = proc.StandardError.ReadToEnd();
		proc.WaitForExit();
		if(!string.IsNullOrEmpty(stdout)) { Debug.Log(stdout); }
		if(!string.IsNullOrEmpty(stderr)) { Debug.Log(stderr); }
		if(!Directory.Exists(tmp))
		{
			Debug.LogError($"not found {tmp}");
			return;
		}
		const string dllName = "Microsoft.Unity.Analyzers.dll";
		var dlls = Directory.GetFiles(tmp, dllName, SearchOption.AllDirectories);
		if(dlls.Length == 0)
		{
			Debug.LogError($"not found {tmp} {dllName}");
			return;
		}
		var source = dlls[0];
		var dest = Path.Combine(Application.dataPath, "Packages", "Editor", dllName);
		File.Copy(source, dest, true);
		Debug.Log($"copy {source} > {dest}");
	}
	static string GetDotnetPath()
	{
#if UNITY_EDITOR_OSX
		string[] candidates = { "/usr/local/share/dotnet/dotnet", "/opt/homebrew/bin/dotnet" };
		foreach (var path in candidates)
		{
			if(File.Exists(path)){return path;}
		}
#endif
		return "dotnet";
	}
}
