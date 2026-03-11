using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Shader")]
    public static class ShaderTools
    {
        [McpTool("shader_create", "Create a new shader file (.shader) at a specified path",
            Group = "shader")]
        public static ToolResult Create(
            [Desc("Shader name (used in Shader dropdown, e.g. 'Custom/MyShader')")] string name,
            [Desc("Shader source code. If empty, generates a default Unlit shader.")] string contents = null,
            [Desc("Save path relative to Assets/ (e.g. Assets/Shaders/MyShader.shader)")] string path = null)
        {
            if (string.IsNullOrEmpty(name))
                return ToolResult.Error("Shader name is required");

            if (string.IsNullOrEmpty(path))
            {
                var safeName = name.Replace("/", "_").Replace("\\", "_");
                path = $"Assets/Shaders/{safeName}.shader";
            }

            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (File.Exists(path))
                return ToolResult.Error($"File already exists: {path}");

            if (string.IsNullOrEmpty(contents))
            {
                contents = GenerateDefaultShader(name);
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, contents);
            AssetDatabase.Refresh();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            return ToolResult.Json(new
            {
                path,
                name,
                hasErrors = shader != null && ShaderUtil.ShaderHasError(shader),
                passCount = shader != null ? ShaderUtil.GetShaderActiveSubshaderIndex(shader) : 0,
            });
        }

        [McpTool("shader_read", "Read the source code and metadata of a shader file",
            Group = "shader", ReadOnly = true)]
        public static ToolResult Read(
            [Desc("Shader asset path (e.g. Assets/Shaders/MyShader.shader)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Shader file not found: {path}");

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var source = File.ReadAllText(path);

            return ToolResult.Json(new
            {
                path,
                name = shader != null ? shader.name : Path.GetFileNameWithoutExtension(path),
                hasErrors = shader != null && ShaderUtil.ShaderHasError(shader),
                propertyCount = shader != null ? ShaderUtil.GetPropertyCount(shader) : 0,
                source,
            });
        }

        [McpTool("shader_update", "Update the source code of an existing shader file",
            Group = "shader")]
        public static ToolResult Update(
            [Desc("Shader asset path")] string path,
            [Desc("New shader source code")] string contents)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Shader file not found: {path}");

            if (string.IsNullOrEmpty(contents))
                return ToolResult.Error("Contents cannot be empty");

            File.WriteAllText(path, contents);
            AssetDatabase.Refresh();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            return ToolResult.Json(new
            {
                path,
                name = shader?.name,
                hasErrors = shader != null && ShaderUtil.ShaderHasError(shader),
            });
        }

        [McpTool("shader_delete", "Delete a shader file from the project",
            Group = "shader")]
        public static ToolResult Delete(
            [Desc("Shader asset path")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            if (!File.Exists(path))
                return ToolResult.Error($"Shader file not found: {path}");

            AssetDatabase.DeleteAsset(path);
            return ToolResult.Json(new { deleted = path });
        }

        [McpTool("shader_info", "Get detailed info about a shader: properties, passes, keywords, compilation errors",
            Group = "shader", ReadOnly = true)]
        public static ToolResult Info(
            [Desc("Shader name (e.g. 'Standard') or asset path (e.g. Assets/Shaders/X.shader)")] string shader)
        {
            Shader s = null;

            if (!string.IsNullOrEmpty(shader) && shader.StartsWith("Assets/"))
                s = AssetDatabase.LoadAssetAtPath<Shader>(shader);

            if (s == null)
                s = Shader.Find(shader);

            if (s == null)
                return ToolResult.Error($"Shader not found: '{shader}'");

            int propCount = ShaderUtil.GetPropertyCount(s);
            var properties = new object[propCount];
            for (int i = 0; i < propCount; i++)
            {
                properties[i] = new
                {
                    name = ShaderUtil.GetPropertyName(s, i),
                    description = ShaderUtil.GetPropertyDescription(s, i),
                    type = ShaderUtil.GetPropertyType(s, i).ToString(),
                };
            }

            return ToolResult.Json(new
            {
                name = s.name,
                hasErrors = ShaderUtil.ShaderHasError(s),
                isSupported = s.isSupported,
                renderQueue = s.renderQueue,
                passCount = s.passCount,
                propertyCount = propCount,
                properties,
            });
        }

        [McpTool("shader_list", "List all shaders available in the project (built-in + custom)",
            Group = "shader", ReadOnly = true)]
        public static ToolResult List(
            [Desc("Filter by name substring (case-insensitive)")] string filter = null,
            [Desc("Only list project shaders (not built-in)")] bool projectOnly = false)
        {
            var guids = AssetDatabase.FindAssets("t:Shader");
            var results = guids.Select(g =>
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(p);
                return new { path = p, name = s?.name ?? "" };
            });

            if (!string.IsNullOrEmpty(filter))
                results = results.Where(r => r.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (projectOnly)
                results = results.Where(r => r.path.StartsWith("Assets/"));

            var list = results.ToArray();
            return ToolResult.Json(new { count = list.Length, shaders = list });
        }

        private static string GenerateDefaultShader(string name)
        {
            return $@"Shader ""{name}""
{{
    Properties
    {{
        _MainTex (""Texture"", 2D) = ""white"" {{}}
        _Color (""Color"", Color) = (1,1,1,1)
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Opaque"" }}
        LOD 100

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata
            {{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            }};

            struct v2f
            {{
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            }};

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }}

            fixed4 frag (v2f i) : SV_Target
            {{
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }}
            ENDCG
        }}
    }}
}}";
        }
    }
}
