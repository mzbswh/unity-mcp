using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Editor.Tools;

namespace UnityMcp.Tests.Editor
{
    public class GameObjectToolsTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up any created test objects
            var testObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in testObjects)
            {
                if (obj.name.StartsWith("Test_"))
                    Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void Create_EmptyGameObject()
        {
            var result = GameObjectTools.Create("Test_Empty");

            Assert.That(result.IsSuccess, Is.True);
            var go = GameObject.Find("Test_Empty");
            Assert.That(go, Is.Not.Null);
        }

        [Test]
        public void Create_Primitive()
        {
            var result = GameObjectTools.Create("Test_Cube", "Cube");

            Assert.That(result.IsSuccess, Is.True);
            var go = GameObject.Find("Test_Cube");
            Assert.That(go, Is.Not.Null);
            Assert.That(go.GetComponent<MeshFilter>(), Is.Not.Null);
        }

        [Test]
        public void Find_ByName()
        {
            new GameObject("Test_FindMe");

            var result = GameObjectTools.Find("Test_FindMe");

            Assert.That(result.IsSuccess, Is.True);
            var content = result.Content as JToken;
            Assert.That(content, Is.Not.Null);
        }

        [Test]
        public void Destroy_ByName()
        {
            new GameObject("Test_Destroy");
            Assert.That(GameObject.Find("Test_Destroy"), Is.Not.Null);

            var result = GameObjectTools.Destroy("Test_Destroy");

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void Modify_Name()
        {
            new GameObject("Test_OldName");

            var result = GameObjectTools.Modify("Test_OldName", name: "Test_NewName");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(GameObject.Find("Test_NewName"), Is.Not.Null);
        }

        [Test]
        public void Modify_Active()
        {
            var go = new GameObject("Test_Active");
            Assert.That(go.activeSelf, Is.True);

            var result = GameObjectTools.Modify("Test_Active", active: false);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(go.activeSelf, Is.False);
        }

        [Test]
        public void SetParent_Works()
        {
            var parent = new GameObject("Test_Parent");
            new GameObject("Test_Child");

            var result = GameObjectTools.SetParent("Test_Child", "Test_Parent");

            Assert.That(result.IsSuccess, Is.True);
            var child = GameObject.Find("Test_Child");
            Assert.That(child.transform.parent, Is.EqualTo(parent.transform));
        }

        [Test]
        public void Duplicate_CreatesClone()
        {
            new GameObject("Test_Original");

            var result = GameObjectTools.Duplicate("Test_Original");

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void GetComponents_ReturnsTransform()
        {
            new GameObject("Test_Components");

            var result = GameObjectTools.GetComponents("Test_Components");

            Assert.That(result.IsSuccess, Is.True);
            // Every GameObject has at least a Transform
        }

        [Test]
        public void Find_NonExistent_ReturnsEmpty()
        {
            var result = GameObjectTools.Find("NonExistent_Object_12345");

            Assert.That(result.IsSuccess, Is.True);
            // Should return empty results, not an error
        }

        [Test]
        public void Destroy_NonExistent_ReturnsError()
        {
            var result = GameObjectTools.Destroy("NonExistent_Object_12345");

            Assert.That(result.IsSuccess, Is.False);
        }
    }
}
