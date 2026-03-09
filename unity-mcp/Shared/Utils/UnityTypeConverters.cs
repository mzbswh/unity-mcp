using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Newtonsoft.Json converters for Unity types (Vector2/3/4, Quaternion, Color).
    /// </summary>
    public static class UnityTypeConverters
    {
        private static readonly System.Collections.Generic.HashSet<JsonSerializer> _registeredSerializers = new();

        public static void EnsureRegistered(JsonSerializer serializer)
        {
            if (!_registeredSerializers.Add(serializer)) return;
            serializer.Converters.Add(new Vector2Converter());
            serializer.Converters.Add(new Vector3Converter());
            serializer.Converters.Add(new Vector4Converter());
            serializer.Converters.Add(new QuaternionConverter());
            serializer.Converters.Add(new ColorConverter());
        }
    }

    public class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 ReadJson(JsonReader reader, Type objectType,
            Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Vector2(obj["x"]?.Value<float>() ?? 0f, obj["y"]?.Value<float>() ?? 0f);
        }

        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WriteEndObject();
        }
    }

    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType,
            Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Vector3(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f);
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WriteEndObject();
        }
    }

    public class Vector4Converter : JsonConverter<Vector4>
    {
        public override Vector4 ReadJson(JsonReader reader, Type objectType,
            Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Vector4(
                obj["x"]?.Value<float>() ?? 0f, obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f, obj["w"]?.Value<float>() ?? 0f);
        }

        public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WritePropertyName("w"); writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override Quaternion ReadJson(JsonReader reader, Type objectType,
            Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Quaternion(
                obj["x"]?.Value<float>() ?? 0f, obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f, obj["w"]?.Value<float>() ?? 1f);
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x"); writer.WriteValue(value.x);
            writer.WritePropertyName("y"); writer.WriteValue(value.y);
            writer.WritePropertyName("z"); writer.WriteValue(value.z);
            writer.WritePropertyName("w"); writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    public class ColorConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType,
            Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            return new Color(
                obj["r"]?.Value<float>() ?? 0f, obj["g"]?.Value<float>() ?? 0f,
                obj["b"]?.Value<float>() ?? 0f, obj["a"]?.Value<float>() ?? 1f);
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r"); writer.WriteValue(value.r);
            writer.WritePropertyName("g"); writer.WriteValue(value.g);
            writer.WritePropertyName("b"); writer.WriteValue(value.b);
            writer.WritePropertyName("a"); writer.WriteValue(value.a);
            writer.WriteEndObject();
        }
    }
}
