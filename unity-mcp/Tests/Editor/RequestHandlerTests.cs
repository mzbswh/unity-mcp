using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class RequestHandlerTests
    {
        private ToolRegistry _registry;
        private RequestHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _registry = new ToolRegistry();
            _registry.ScanAll();
            _handler = new RequestHandler(_registry, 30000);
        }

        [Test]
        public async Task HandleRequest_InvalidJson_ReturnsParseError()
        {
            var response = await _handler.HandleRequest("not valid json{{{");
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32700, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_UnknownMethod_ReturnsMethodNotFound()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "nonexistent/method"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32601, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_Ping_ReturnsEmptyResult()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "ping"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNull(json["error"]);
        }

        [Test]
        public async Task HandleRequest_ToolsList_ReturnsTools()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/list"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNotNull(json["result"]["tools"]);
            Assert.That(json["result"]["tools"].Count(), Is.GreaterThan(0));
        }

        [Test]
        public async Task HandleRequest_ResourcesList_ReturnsResources()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "resources/list"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNotNull(json["result"]["resources"]);
        }

        [Test]
        public async Task HandleRequest_ToolsCall_MissingName_ReturnsInvalidParams()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JObject()
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32602, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_Initialize_ReturnsServerInfo()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JObject
                {
                    ["clientInfo"] = new JObject { ["name"] = "test", ["version"] = "1.0" }
                }
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.AreEqual("2024-11-05", json["result"]["protocolVersion"].ToString());
            Assert.IsNotNull(json["result"]["serverInfo"]);
        }

        [Test]
        public async Task HandleNotification_DoesNotThrow()
        {
            await _handler.HandleNotification("{\"method\":\"test/notification\"}");
            await _handler.HandleNotification("invalid json");
            Assert.Pass("Notifications should not throw");
        }
    }
}
