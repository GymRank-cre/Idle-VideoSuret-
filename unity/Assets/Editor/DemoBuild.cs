using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Build WebGL de DÉMONSTRATION (captures navigateur) : active le symbole IDLE_DEMO le temps du build, jamais dans le build publié.
public static class DemoBuild
{
    [MenuItem("Tools/Idle Prototype/Build WebGL démo (captures)")]
    static void Run()
    {
        NamedBuildTarget target = NamedBuildTarget.WebGL;
        string old = PlayerSettings.GetScriptingDefineSymbols(target);
        try
        {
            PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(old) ? "IDLE_DEMO" : old + ";IDLE_DEMO");
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("Tools/Idle Prototype/Build WebGL de test");
        }
        finally
        {
            PlayerSettings.SetScriptingDefineSymbols(target, old);
        }
    }
}
