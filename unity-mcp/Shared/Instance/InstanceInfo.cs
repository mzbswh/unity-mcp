using System;

namespace UnityMcp.Shared.Instance
{
    [Serializable]
    public class InstanceInfo
    {
        public int Port;
        public string ProjectPath;
        public string ProjectName;
        public int Pid;
        public string UnityVersion;
        public string StartTime;
        public bool IsReloading;
        public string Status;
        public string LastHeartbeat;
    }
}
