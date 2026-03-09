using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Tests.Editor
{
    public class JsonSchemaGeneratorTests
    {
        // Test helper methods
        private static void StringMethod(string value) { }
        private static void IntMethod(int value) { }
        private static void FloatMethod(float value) { }
        private static void BoolMethod(bool value) { }
        private static void Vector3Method(Vector3 pos) { }
        private static void OptionalMethod(string required, int optional = 42) { }
        private static void DescMethod([Desc("A name")] string name) { }
        private static void ArrayMethod(string[] items) { }
        private static void ListMethod(List<int> items) { }
        private static void NullableMethod(int? value) { }

        [Test]
        public void String_GeneratesStringType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("StringMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["value"]["type"].ToString(), Is.EqualTo("string"));
            Assert.That(schema["required"], Is.Not.Null);
        }

        [Test]
        public void Int_GeneratesIntegerType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("IntMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["value"]["type"].ToString(), Is.EqualTo("integer"));
        }

        [Test]
        public void Float_GeneratesNumberType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("FloatMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["value"]["type"].ToString(), Is.EqualTo("number"));
        }

        [Test]
        public void Bool_GeneratesBooleanType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("BoolMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["value"]["type"].ToString(), Is.EqualTo("boolean"));
        }

        [Test]
        public void Vector3_GeneratesObjectWithXYZ()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("Vector3Method",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            var posSchema = schema["properties"]["pos"];
            Assert.That(posSchema["type"].ToString(), Is.EqualTo("object"));
            Assert.That(posSchema["properties"]["x"], Is.Not.Null);
            Assert.That(posSchema["properties"]["y"], Is.Not.Null);
            Assert.That(posSchema["properties"]["z"], Is.Not.Null);
        }

        [Test]
        public void Optional_NotInRequired()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("OptionalMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            var required = (JArray)schema["required"];
            Assert.That(required, Is.Not.Null);
            Assert.That(required.Count, Is.EqualTo(1));
            Assert.That(required[0].ToString(), Is.EqualTo("required"));
        }

        [Test]
        public void DescAttribute_AddsDescription()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("DescMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["name"]["description"].ToString(),
                Is.EqualTo("A name"));
        }

        [Test]
        public void Array_GeneratesArrayType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("ArrayMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["items"]["type"].ToString(), Is.EqualTo("array"));
            Assert.That(schema["properties"]["items"]["items"]["type"].ToString(), Is.EqualTo("string"));
        }

        [Test]
        public void List_GeneratesArrayType()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("ListMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            Assert.That(schema["properties"]["items"]["type"].ToString(), Is.EqualTo("array"));
            Assert.That(schema["properties"]["items"]["items"]["type"].ToString(), Is.EqualTo("integer"));
        }

        [Test]
        public void Nullable_NotRequired()
        {
            var method = typeof(JsonSchemaGeneratorTests).GetMethod("NullableMethod",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var schema = JsonSchemaGenerator.GenerateForMethod(method);

            // Nullable params should not appear in required
            Assert.That(schema["required"], Is.Null);
        }
    }
}
