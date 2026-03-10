using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Shared.Utils
{
    /// <summary>
    /// Binds JObject arguments to a C# method's parameter list,
    /// handling Unity types, enums, nullables, and standard types.
    /// </summary>
    public static class ParameterBinder
    {
        public static object[] Bind(MethodInfo method, JObject arguments)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var token = arguments?[param.Name];

                if (token == null || token.Type == JTokenType.Null)
                {
                    if (param.HasDefaultValue)
                        args[i] = param.DefaultValue;
                    else if (IsNullable(param.ParameterType))
                        args[i] = null;
                    else
                        throw new ArgumentException(
                            $"Required parameter '{param.Name}' not provided");
                }
                else
                {
                    args[i] = ConvertParameter(token, param.ParameterType, param.Name);
                }
            }
            return args;
        }

        private static object ConvertParameter(JToken token, Type targetType, string paramName)
        {
            // Unity vector types
            if (targetType == typeof(Vector2))
                return new Vector2(
                    token["x"]?.Value<float>() ?? 0f,
                    token["y"]?.Value<float>() ?? 0f);

            if (targetType == typeof(Vector3))
                return new Vector3(
                    token["x"]?.Value<float>() ?? 0f,
                    token["y"]?.Value<float>() ?? 0f,
                    token["z"]?.Value<float>() ?? 0f);

            if (targetType == typeof(Vector4))
                return new Vector4(
                    token["x"]?.Value<float>() ?? 0f,
                    token["y"]?.Value<float>() ?? 0f,
                    token["z"]?.Value<float>() ?? 0f,
                    token["w"]?.Value<float>() ?? 0f);

            if (targetType == typeof(Quaternion))
                return new Quaternion(
                    token["x"]?.Value<float>() ?? 0f,
                    token["y"]?.Value<float>() ?? 0f,
                    token["z"]?.Value<float>() ?? 0f,
                    token["w"]?.Value<float>() ?? 1f);

            if (targetType == typeof(Color))
                return new Color(
                    token["r"]?.Value<float>() ?? 0f,
                    token["g"]?.Value<float>() ?? 0f,
                    token["b"]?.Value<float>() ?? 0f,
                    token["a"]?.Value<float>() ?? 1f);

            if (targetType == typeof(Bounds))
            {
                var centerToken = token["center"];
                var sizeToken = token["size"];
                var center = centerToken != null
                    ? (Vector3)ConvertParameter(centerToken, typeof(Vector3), "center")
                    : Vector3.zero;
                var size = sizeToken != null
                    ? (Vector3)ConvertParameter(sizeToken, typeof(Vector3), "size")
                    : Vector3.zero;
                return new Bounds(center, size);
            }

            if (targetType == typeof(Rect))
                return new Rect(
                    token["x"]?.Value<float>() ?? 0f,
                    token["y"]?.Value<float>() ?? 0f,
                    token["width"]?.Value<float>() ?? 0f,
                    token["height"]?.Value<float>() ?? 0f);

            // Enum (string name -> enum value)
            if (targetType.IsEnum)
            {
                var str = token.Value<string>();
                if (Enum.TryParse(targetType, str, ignoreCase: true, out var enumVal))
                    return enumVal;
                throw new ArgumentException(
                    $"Invalid enum value '{str}' for '{paramName}'. " +
                    $"Valid: {string.Join(", ", Enum.GetNames(targetType))}");
            }

            // Nullable<T>
            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
                return ConvertParameter(token, underlying, paramName);

            // JObject / JArray passthrough (also handle stringified JSON from MCP clients)
            if (targetType == typeof(JObject))
            {
                if (token is JObject jo) return jo;
                if (token.Type == JTokenType.String)
                    return JObject.Parse(token.Value<string>());
                return token.ToObject<JObject>();
            }
            if (targetType == typeof(JArray))
            {
                if (token is JArray ja) return ja;
                if (token.Type == JTokenType.String)
                    return JArray.Parse(token.Value<string>());
                return token.ToObject<JArray>();
            }

            // Standard types (string, int, float, bool, arrays, objects)
            return token.ToObject(targetType);
        }

        private static bool IsNullable(Type type) =>
            !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
