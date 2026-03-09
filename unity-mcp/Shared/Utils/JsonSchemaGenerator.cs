using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Shared.Attributes;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Generates JSON Schema for MCP tool parameters from C# method signatures.
    /// </summary>
    public static class JsonSchemaGenerator
    {
        public static JObject GenerateForMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var properties = new JObject();
            var required = new JArray();

            foreach (var param in parameters)
            {
                var schema = GetSchemaForType(param.ParameterType);
                var desc = param.GetCustomAttribute<DescAttribute>();
                if (desc != null)
                    schema["description"] = desc.Text;

                properties[param.Name] = schema;

                if (!param.HasDefaultValue && !IsNullable(param.ParameterType))
                    required.Add(param.Name);
            }

            var result = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            };
            if (required.Count > 0)
                result["required"] = required;

            return result;
        }

        public static JObject GetSchemaForType(Type type)
        {
            // Nullable<T> unwrap — preserve nullable hint for AI clients
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                var inner = GetSchemaForType(underlying);
                inner["nullable"] = true;
                return inner;
            }

            // Primitives
            if (type == typeof(string))
                return new JObject { ["type"] = "string" };
            if (type == typeof(int) || type == typeof(long))
                return new JObject { ["type"] = "integer" };
            if (type == typeof(float) || type == typeof(double))
                return new JObject { ["type"] = "number" };
            if (type == typeof(bool))
                return new JObject { ["type"] = "boolean" };

            // Enums
            if (type.IsEnum)
                return new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(Enum.GetNames(type)),
                    ["description"] = $"Enum values are case-insensitive. Valid: {string.Join(", ", Enum.GetNames(type))}"
                };

            // Unity types
            if (type == typeof(Vector2))
                return VectorSchema("x", "y");
            if (type == typeof(Vector3))
                return VectorSchema("x", "y", "z");
            if (type == typeof(Vector4) || type == typeof(Quaternion))
                return VectorSchema("x", "y", "z", "w");
            if (type == typeof(Color))
                return ColorSchema();
            if (type == typeof(Rect))
                return VectorSchema("x", "y", "width", "height");
            if (type == typeof(Bounds))
                return new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["center"] = VectorSchema("x", "y", "z"),
                        ["size"] = VectorSchema("x", "y", "z")
                    },
                    ["required"] = new JArray("center", "size")
                };

            // Arrays / Lists
            if (type.IsArray)
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = GetSchemaForType(type.GetElementType())
                };
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = GetSchemaForType(type.GetGenericArguments()[0])
                };

            // JObject / JArray passthrough
            if (type == typeof(JObject))
                return new JObject { ["type"] = "object" };
            if (type == typeof(JArray))
                return new JObject { ["type"] = "array" };

            // Default: object
            return new JObject { ["type"] = "object" };
        }

        private static JObject VectorSchema(params string[] fields)
        {
            var props = new JObject();
            foreach (var f in fields)
                props[f] = new JObject { ["type"] = "number" };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = new JArray(fields),
                ["additionalProperties"] = false
            };
        }

        private static JObject ColorSchema()
        {
            var props = new JObject();
            foreach (var f in new[] { "r", "g", "b" })
            {
                props[f] = new JObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                };
            }
            props["a"] = new JObject
            {
                ["type"] = "number",
                ["minimum"] = 0,
                ["maximum"] = 1,
                ["default"] = 1
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = new JArray("r", "g", "b"),
                ["additionalProperties"] = false
            };
        }

        private static bool IsNullable(Type type) =>
            Nullable.GetUnderlyingType(type) != null;
    }
}
