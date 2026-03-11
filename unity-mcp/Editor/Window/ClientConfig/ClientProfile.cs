using System;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public enum ConfigStrategy { JsonFile, CliCommand }

    [Serializable]
    public class PlatformPaths
    {
        public string Windows;
        public string Mac;
        public string Linux;

        public string Current =>
#if UNITY_EDITOR_WIN
            Windows;
#elif UNITY_EDITOR_OSX
            Mac;
#else
            Linux;
#endif
    }

    [Serializable]
    public class ClientProfile
    {
        public string Id;
        public string DisplayName;
        public ConfigStrategy Strategy;
        public PlatformPaths Paths;
        public string[] InstallSteps;
        public bool IsProjectLevel;
    }
}
