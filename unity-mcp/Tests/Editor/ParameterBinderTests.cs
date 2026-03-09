using System;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Tests.Editor
{
    public class ParameterBinderTests
    {
        // Test helper methods
        private static string Echo(string value) => value;
        private static int Add(int a, int b) => a + b;
        private static float Scale(float value, float multiplier = 1f) => value * multiplier;
        private static Vector3 MakeVec(Vector3 pos) => pos;
        private static Color MakeColor(Color c) => c;
        private static string WithOptional(string required, string optional = "default") => required + optional;

        private enum TestEnum { Alpha, Beta, Gamma }
        private static string EnumParam(TestEnum mode) => mode.ToString();

        [Test]
        public void Bind_StringParam()
        {
            var method = typeof(ParameterBinderTests).GetMethod("Echo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["value"] = "hello" };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result.Length, Is.EqualTo(1));
            Assert.That(result[0], Is.EqualTo("hello"));
        }

        [Test]
        public void Bind_IntParams()
        {
            var method = typeof(ParameterBinderTests).GetMethod("Add",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["a"] = 3, ["b"] = 7 };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result[0], Is.EqualTo(3));
            Assert.That(result[1], Is.EqualTo(7));
        }

        [Test]
        public void Bind_DefaultParam_UsesDefault()
        {
            var method = typeof(ParameterBinderTests).GetMethod("Scale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["value"] = 5f };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result[0], Is.EqualTo(5f));
            Assert.That(result[1], Is.EqualTo(1f)); // default
        }

        [Test]
        public void Bind_DefaultParam_OverrideProvided()
        {
            var method = typeof(ParameterBinderTests).GetMethod("Scale",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["value"] = 5f, ["multiplier"] = 2f };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result[1], Is.EqualTo(2f));
        }

        [Test]
        public void Bind_Vector3()
        {
            var method = typeof(ParameterBinderTests).GetMethod("MakeVec",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject
            {
                ["pos"] = new JObject { ["x"] = 1f, ["y"] = 2f, ["z"] = 3f }
            };

            var result = ParameterBinder.Bind(method, args);
            var vec = (Vector3)result[0];

            Assert.That(vec.x, Is.EqualTo(1f));
            Assert.That(vec.y, Is.EqualTo(2f));
            Assert.That(vec.z, Is.EqualTo(3f));
        }

        [Test]
        public void Bind_Color()
        {
            var method = typeof(ParameterBinderTests).GetMethod("MakeColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject
            {
                ["c"] = new JObject { ["r"] = 1f, ["g"] = 0.5f, ["b"] = 0f, ["a"] = 1f }
            };

            var result = ParameterBinder.Bind(method, args);
            var color = (Color)result[0];

            Assert.That(color.r, Is.EqualTo(1f));
            Assert.That(color.g, Is.EqualTo(0.5f));
            Assert.That(color.b, Is.EqualTo(0f));
        }

        [Test]
        public void Bind_Enum()
        {
            var method = typeof(ParameterBinderTests).GetMethod("EnumParam",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["mode"] = "Beta" };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result[0], Is.EqualTo(TestEnum.Beta));
        }

        [Test]
        public void Bind_MissingRequired_Throws()
        {
            var method = typeof(ParameterBinderTests).GetMethod("Echo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject(); // missing "value"

            Assert.Throws<ArgumentException>(() => ParameterBinder.Bind(method, args));
        }

        [Test]
        public void Bind_NullArgs_UsesDefaults()
        {
            var method = typeof(ParameterBinderTests).GetMethod("WithOptional",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var args = new JObject { ["required"] = "test" };

            var result = ParameterBinder.Bind(method, args);

            Assert.That(result[0], Is.EqualTo("test"));
            Assert.That(result[1], Is.EqualTo("default"));
        }
    }
}
