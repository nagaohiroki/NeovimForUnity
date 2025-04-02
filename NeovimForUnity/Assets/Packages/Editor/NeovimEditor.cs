using System;
using System.IO;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
#if UNITY_EDITOR_OSX
using System.Xml.Linq;
using System.Linq;
#endif
namespace NeovimEditor
{
	[InitializeOnLoad]
	public class NeovimEditor : IExternalCodeEditor
	{
		const string keyNvimOverrideCmd = "nvim_override_cmd";
		const string keyNvimArgs = "nvim_args";
		const string keyNvimExt = "nvim_ext";
		const string keyNvimKeywords = "nvim_cmd_keywords";
		const string defaultExt = ".cs,.shader,.json,.xml,.txt,.yml,.yaml,.md";
		const string defaultArgs = "+$(Line) \"$(File)\"";
		const string defaultNvimKeywords = "nvim,neovide";
		string[] Extensions => GetString(keyNvimExt, defaultExt).Split(',');
		string[] Keywords => GetString(keyNvimKeywords, defaultNvimKeywords).Split(',');
		public static bool IsEnabled => CodeEditor.CurrentEditor is NeovimEditor;
		public CodeEditor.Installation[] Installations => new[]
		{
			new CodeEditor.Installation
			{
				Name = "nvim",
				Path = ""
			}
		};
		static NeovimEditor()
		{
			CodeEditor.Register(new NeovimEditor());
		}
		public void Initialize(string editorInstallationPath) { }
		public void SyncAll()
		{
			Sync();
		}
		public void OnGUI()
		{
			var path = CodeEditor.Editor.CurrentInstallation.Path;
			EditorGUILayout.BeginVertical();
			TextField($"Command(empty:{GetNvimExe(path)})", keyNvimOverrideCmd, string.Empty);
			TextField("Arguments", keyNvimArgs, defaultArgs);
			TextField("Extensions", keyNvimExt, defaultExt);
			NeovimCommandGUI();
			EditorGUILayout.EndVertical();
			CodeEditor.SetExternalScriptEditor(FindVSPath());
			CodeEditor.Editor.CurrentCodeEditor.OnGUI();
			CodeEditor.SetExternalScriptEditor(path);
		}
		public bool OpenProject(string filePath, int line, int column)
		{
			if(!IsCodeAsset(filePath, Extensions))
			{
				return false;
			}
			var args = EditorPrefs.GetString(keyNvimArgs).
				Replace("$(File)", filePath).
				Replace("$(Line)", Mathf.Max(0, line).ToString()).
				Replace("$(Column)", Mathf.Max(0, column).ToString());
			var exe = EditorPrefs.GetString(keyNvimOverrideCmd);
			if(string.IsNullOrEmpty(exe))
			{
				exe = GetNvimExe(CodeEditor.Editor.CurrentInstallation.Path);
			}
			var info = new System.Diagnostics.ProcessStartInfo
			{
				FileName = exe,
				Arguments = args,
				CreateNoWindow = false,
				UseShellExecute = false,
			};
			var envs = Environment.GetEnvironmentVariables();
			foreach(DictionaryEntry env in envs)
			{
				info.EnvironmentVariables[(string)env.Key] = (string)env.Value;
			}
			System.Diagnostics.Process.Start(info);
			return true;
		}
		public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
		{
			var ext = Extensions;
			if(IsCodeAssets(addedFiles, ext) ||
			   IsCodeAssets(deletedFiles, ext) ||
			   IsCodeAssets(movedFiles, ext) ||
			   IsCodeAssets(movedFromFiles, ext) ||
			   IsCodeAssets(importedFiles, ext))
			{
				Sync();
			}
		}
		public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
		{
			if(IsNvimPath(editorPath))
			{
				installation = new()
				{
					Name = Path.GetFileNameWithoutExtension(editorPath),
					Path = editorPath
				};
				return true;
			}
			installation = default;
			return false;
		}
		void Sync()
		{
			var vs = FindVSPath();
			if(string.IsNullOrEmpty(vs))
			{
				Debug.Log("Visual Studio is not found");
				return;
			}
			var path = CodeEditor.Editor.CurrentInstallation.Path;
			CodeEditor.SetExternalScriptEditor(vs);
			CodeEditor.Editor.CurrentCodeEditor.SyncAll();
			CodeEditor.SetExternalScriptEditor(path);
		}
		bool IsCodeAssets(string[] files, string[] ext)
		{
			foreach(var file in files)
			{
				if(IsCodeAsset(file, ext))
				{
					return true;
				}
			}
			return false;
		}
		bool IsCodeAsset(string filePath, string[] extList)
		{
			var ext = Path.GetExtension(filePath);
			foreach(var targetExt in extList)
			{
				if(ext == targetExt)
				{
					return true;
				}
			}
			return false;
		}
		string GetNvimExe(string inPath)
		{
#if UNITY_EDITOR_OSX
			if(inPath.EndsWith(".app"))
			{
				return FindExecutableMacOS(inPath);
			}
#endif
			return inPath;
		}
		void NeovimCommandGUI()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Neovim Command (Configure on Preferences > Neovim Settings)");
			EditorGUILayout.LabelField(EditorPrefs.GetString(keyNvimKeywords));
			EditorGUILayout.EndHorizontal();
		}
		void TextField(string inLabel, string inKey, string inDefaultValue)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(inLabel);
			EditorPrefs.SetString(inKey, EditorGUILayout.TextField(GetString(inKey, inDefaultValue)));
			EditorGUILayout.EndHorizontal();
		}
		string FindVSPath()
		{
			var paths = CodeEditor.Editor.GetFoundScriptEditorPaths();
			foreach(var path in paths)
			{
				if(path.Value.Contains("Visual Studio"))
				{
					return path.Key;
				}
			}
			return null;
		}
		bool IsNvimPath(string inPath)
		{
			var appName = Path.GetFileNameWithoutExtension(inPath).ToLower();
			var keywords = Keywords;
			foreach(var name in keywords)
			{
				if(appName.Contains(name.ToLower()))
				{
					return true;
				}
			}
			return false;
		}
#if UNITY_EDITOR_OSX
		string FindExecutableMacOS(string inPath)
		{
			var contents = Path.Combine(inPath, "Contents");
			var infoPlistPath = Path.Combine(contents, "Info.plist");
			if(File.Exists(infoPlistPath))
			{
				try
				{
					var doc = XDocument.Load(infoPlistPath);
					var executableElement = doc.Root.Element("dict").Elements("key").FirstOrDefault(e => e.Value == "CFBundleExecutable");
					if(executableElement != null)
					{
						var executableName = executableElement.ElementsAfterSelf("string").First().Value;
						var contentsPath = Path.Combine(contents, "MacOS");
						return Path.Combine(contentsPath, executableName);
					}
				}
				catch(System.Exception e)
				{
					Debug.LogError("Error parsing Info.plist: " + e.Message);
				}
			}
			return null;
		}
#endif
		[SettingsProvider]
		public static SettingsProvider CreateProjectSettingMenu()
		{
			return new SettingsProvider("Preferences/Neovim Settings", SettingsScope.User)
			{
				label = "Neovim Settings",
				guiHandler = Gui
			};
		}
		static void Gui(string inSearchContext)
		{
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Neovim Command Keywords(Application Keywords (comma-separated, partial matches allowed))");
			EditorPrefs.SetString(keyNvimKeywords, EditorGUILayout.TextField(GetString(keyNvimKeywords, defaultNvimKeywords)));
			EditorGUILayout.EndHorizontal();
		}
		static string GetString(string inKey, string inDefault)
		{
			var text = EditorPrefs.GetString(inKey);
			if(!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return inDefault;
		}
	}

}
