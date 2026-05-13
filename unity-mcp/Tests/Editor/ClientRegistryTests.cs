using System.Linq;
using NUnit.Framework;
using UnityMcp.Editor.Window.ClientConfig;

namespace UnityMcp.Tests.Editor
{
    public class ClientRegistryTests
    {
        [Test]
        public void All_IncludesCodexWithSearchableDisplayName()
        {
            var codex = ClientRegistry.All.FirstOrDefault(profile => profile.Id == "codex");

            Assert.That(codex, Is.Not.Null);
            Assert.That(codex.DisplayName, Does.StartWith("Codex"));
        }
    }
}
