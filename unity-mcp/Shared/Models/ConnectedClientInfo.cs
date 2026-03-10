using System;

namespace UnityMcp.Shared.Models
{
    public class ConnectedClientInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Endpoint { get; set; }
        public DateTime ConnectedAt { get; set; }
    }
}
