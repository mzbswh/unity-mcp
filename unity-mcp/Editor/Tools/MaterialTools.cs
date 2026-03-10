using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Material")]
    public static class MaterialTools
    {
        [McpTool("material_create", "Create a new material with a specified shader",
            Group = "material")]
        public static ToolResult Create(
            [Desc("Material name")] string name,
            [Desc("Shader name (e.g. 'Standard', 'Universal Render Pipeline/Lit')")] string shaderName = "Standard",
            [Desc("Save path (e.g. Assets/Materials/MyMat.mat)")] string path = null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return ToolResult.Error($"Shader not found: '{shaderName}'");

            var material = new Material(shader) { name = name };

            if (string.IsNullOrEmpty(path))
                path = $"Assets/{name}.mat";

            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                path,
                name = material.name,
                shader = shaderName,
                instanceId = material.GetInstanceID()
            });
        }

        [McpTool("material_modify", "Modify properties of an existing material. For textures, pass asset path (e.g. 'Assets/Textures/X.png'). For blend/render mode changes, use material_set_render_mode instead.",
            Group = "material")]
        public static ToolResult Modify(
            [Desc("Material asset path")] string path,
            [Desc("Properties to set as {propertyName: value}. Texture properties accept asset paths.")] JObject properties)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                return ToolResult.Error($"Material not found: {path}");

            Undo.RecordObject(material, "Modify Material");
            int modified = 0;

            foreach (var kv in properties)
            {
                string propName = kv.Key;
                var value = kv.Value;

                if (!material.HasProperty(propName))
                    continue;

                var propType = material.shader.FindPropertyIndex(propName);
                if (propType < 0) continue;

                var shaderPropType = material.shader.GetPropertyType(propType);
                switch (shaderPropType)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        material.SetColor(propName, new Color(
                            value["r"]?.Value<float>() ?? 0f,
                            value["g"]?.Value<float>() ?? 0f,
                            value["b"]?.Value<float>() ?? 0f,
                            value["a"]?.Value<float>() ?? 1f));
                        modified++;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        material.SetFloat(propName, value.Value<float>());
                        modified++;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        material.SetVector(propName, new Vector4(
                            value["x"]?.Value<float>() ?? 0f,
                            value["y"]?.Value<float>() ?? 0f,
                            value["z"]?.Value<float>() ?? 0f,
                            value["w"]?.Value<float>() ?? 0f));
                        modified++;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:
                        material.SetInt(propName, value.Value<int>());
                        modified++;
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var texPath = value.Value<string>();
                        if (string.IsNullOrEmpty(texPath))
                        {
                            material.SetTexture(propName, null);
                            modified++;
                        }
                        else
                        {
                            var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                            if (tex != null)
                            {
                                material.SetTexture(propName, tex);
                                modified++;
                            }
                        }
                        break;
                }
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return ToolResult.Text($"Modified {modified} properties on '{material.name}'");
        }

        [McpTool("shader_list", "List all available shaders",
            Group = "material", ReadOnly = true)]
        public static ToolResult ShaderList(
            [Desc("Filter by name (partial match)")] string filter = null,
            [Desc("Max results")] int maxCount = 100)
        {
            var guids = AssetDatabase.FindAssets("t:Shader");
            var shaders = guids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<Shader>(path))
                .Where(s => s != null)
                .Select(s => s.name)
                .ToList();

            // Add built-in shaders
            var builtIn = new[] {
                "Standard", "Standard (Specular setup)",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Simple Lit",
                "HDRP/Lit", "HDRP/Unlit",
                "Unlit/Color", "Unlit/Texture", "Unlit/Transparent",
                "Sprites/Default", "UI/Default",
                "Skybox/Procedural", "Skybox/6 Sided",
                "Particles/Standard Unlit",
            };
            foreach (var name in builtIn)
                if (Shader.Find(name) != null && !shaders.Contains(name))
                    shaders.Add(name);

            if (!string.IsNullOrEmpty(filter))
                shaders = shaders.Where(s => s.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var result = shaders.Take(maxCount).OrderBy(s => s).ToArray();
            return ToolResult.Json(new { count = result.Length, shaders = result });
        }

        [McpTool("material_set_render_mode",
            "Set the rendering mode of a material (handles shader keywords, render queue, etc.). " +
            "For Standard shader: Opaque/Cutout/Fade/Transparent. " +
            "For Particles/Standard Unlit: Additive/AlphaBlend/Multiply. " +
            "For URP shaders: Opaque/Transparent.",
            Group = "material")]
        public static ToolResult SetRenderMode(
            [Desc("Material asset path")] string path,
            [Desc("Render mode name (e.g. 'Opaque', 'Transparent', 'Fade', 'Cutout', 'Additive', 'AlphaBlend', 'Multiply')")] string mode)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                return ToolResult.Error($"Material not found: {path}");

            Undo.RecordObject(material, "Set Render Mode");

            var shaderName = material.shader.name;
            bool applied = false;

            if (shaderName == "Standard" || shaderName == "Standard (Specular setup)")
                applied = ApplyStandardRenderMode(material, mode);
            else if (shaderName.StartsWith("Particles/"))
                applied = ApplyParticleRenderMode(material, mode);
            else if (shaderName.StartsWith("Universal Render Pipeline/"))
                applied = ApplyUrpRenderMode(material, mode);
            else
                return ToolResult.Error(
                    $"Automatic render mode switching not supported for shader '{shaderName}'. " +
                    "Use material_modify to set individual properties manually.");

            if (!applied)
                return ToolResult.Error(
                    $"Unknown render mode '{mode}' for shader '{shaderName}'.");

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return ToolResult.Text($"Set render mode to '{mode}' on '{material.name}' (shader: {shaderName})");
        }

        // --- Standard shader (Built-in RP) ---
        private static bool ApplyStandardRenderMode(Material mat, string mode)
        {
            switch (mode.ToLower())
            {
                case "opaque":
                    mat.SetFloat("_Mode", 0);
                    mat.SetOverrideTag("RenderType", "");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                    break;
                case "cutout":
                    mat.SetFloat("_Mode", 1);
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case "fade":
                    mat.SetFloat("_Mode", 2);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                case "transparent":
                    mat.SetFloat("_Mode", 3);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                default:
                    return false;
            }
            return true;
        }

        // --- Particles shaders (Particles/Standard Unlit, etc.) ---
        private static bool ApplyParticleRenderMode(Material mat, string mode)
        {
            switch (mode.ToLower())
            {
                case "opaque":
                    mat.SetFloat("_BlendMode", 0);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAMODULATE_ON");
                    mat.renderQueue = (int)RenderQueue.Geometry;
                    break;
                case "cutout":
                    mat.SetFloat("_BlendMode", 1);
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAMODULATE_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case "additive":
                    mat.SetFloat("_BlendMode", 2);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.One);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAMODULATE_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                case "alphablend":
                case "fade":
                case "transparent":
                    mat.SetFloat("_BlendMode", 3);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAMODULATE_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                case "multiply":
                    mat.SetFloat("_BlendMode", 4);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.DstColor);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.EnableKeyword("_ALPHAMODULATE_ON");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                default:
                    return false;
            }
            return true;
        }

        // --- URP shaders ---
        private static bool ApplyUrpRenderMode(Material mat, string mode)
        {
            switch (mode.ToLower())
            {
                case "opaque":
                    mat.SetFloat("_Surface", 0);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetInt("_SrcBlend", (int)BlendMode.One);
                    mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Geometry;
                    break;
                case "transparent":
                case "fade":
                    mat.SetFloat("_Surface", 1);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
