using UnityEditor;
namespace NeovimEditor
{
	public class CustomProjectGenerator : AssetPostprocessor
	{
		// This method is called when the project generation is triggered.
		public static string OnSelectingCSProjectStyle()
		{
			// "SDK" (Used by VS Code)
			// "Legacy" (Used by Visual Studio, but newer versions are able to handle SDK-Style projects as well)
			return EditorPrefs.GetString(NeovimEditor.keyNvimCSProjectStyle);
		}
	}
}
