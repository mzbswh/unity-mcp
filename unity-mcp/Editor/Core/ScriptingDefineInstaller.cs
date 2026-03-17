using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace UnityMcp.Editor.Core
{
    [InitializeOnLoad]
    static class ScriptingDefineInstaller
    {
        const string k_Define = "UNITY_MCP";

        static ScriptingDefineInstaller()
        {
            AddDefineIfMissing();
        }

        static void AddDefineIfMissing()
        {
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            if (defines.Contains(k_Define))
                return;
            var list = new List<string>(defines) { k_Define };
            PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
#else
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (defines.Split(';').Contains(k_Define))
                return;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
                string.IsNullOrEmpty(defines) ? k_Define : defines + ";" + k_Define);
#endif
        }
    }
}
