using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMcp.Editor.Tools;

namespace UnityMcp.Tests.Editor
{
    public class ConsoleToolsTests
    {
        [Test]
        public void ClearLogs_ReturnsSuccessPayload()
        {
            Debug.Log("ConsoleToolsTests.ClearLogs_ReturnsSuccessPayload");

            var result = ConsoleTools.ClearLogs();
            var json = (JObject)TestUtilities.AssertSuccessAndParse(result);

            Assert.That(json["cleared"], Is.Not.Null);
            Assert.That(json["remaining"], Is.Not.Null);
            Assert.That(json["remaining"].Value<int>(), Is.EqualTo(0));
        }
    }
}
