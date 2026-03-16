using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("PsdToUI")]
    public static class PsdToUITools
    {
        // Default component types for each UI role
        private static readonly Dictionary<string, string> DefaultComponentTypes = new()
        {
            ["Text"] = "UnityEngine.UI.Text, UnityEngine.UI",
            ["TMPText"] = "TMPro.TextMeshProUGUI, Unity.TextMeshPro",
            ["Image"] = "UnityEngine.UI.Image, UnityEngine.UI",
            ["RawImage"] = "UnityEngine.UI.RawImage, UnityEngine.UI",
            ["FillColor"] = "UnityEngine.UI.Image, UnityEngine.UI",
            ["Button"] = "UnityEngine.UI.Button, UnityEngine.UI",
            ["TMPButton"] = "UnityEngine.UI.Button, UnityEngine.UI",
        };

        [McpTool("psd_create_ui",
            "Create Unity UI hierarchy from parsed PSD layer data. " +
            "Accepts a JSON layer tree (from psd_parse output), creates GameObjects with " +
            "UI components, assigns sprites, and saves as a prefab. " +
            "Component type is determined per-layer by uiType and can be overridden via componentMap " +
            "to use project-specific custom components. " +
            "Returns a list of all created objects and exported images for AI renaming.",
            Group = "psd")]
        public static ToolResult CreateUI(
            [Desc("JSON array of layer definitions from psd_parse output. Each layer has: " +
                   "name, type (image/text/group/fillcolor), left, top, width, height, opacity, " +
                   "uiType (Image/RawImage/FillColor/Text/TMPText/Button/TMPButton), " +
                   "textProperties (for text layers), fillColor (for fillcolor layers), " +
                   "imagePath (Assets-relative path for image layers), children (for groups)")]
            JArray layers,
            [Desc("Prefab save path (e.g. Assets/Prefab/MyUI.prefab)")]
            string prefabPath,
            [Desc("Images directory in Assets (e.g. Assets/UI/MyPSD). Used to locate exported sprites")]
            string imageDir,
            [Desc("PSD canvas width in pixels")]
            int canvasWidth,
            [Desc("PSD canvas height in pixels")]
            int canvasHeight,
            [Desc("Component mapping table (JSON object). Maps uiType names to full C# type names. " +
                   "If the type exists in the project, it will be used; otherwise falls back to defaults. " +
                   "Example: {\"Text\": \"MyGame.UI.CustomText, Assembly-CSharp\", " +
                   "\"Image\": \"MyGame.UI.CustomImage, Assembly-CSharp\", " +
                   "\"Button\": \"MyGame.UI.CustomButton, Assembly-CSharp\"}. " +
                   "Supported keys: Text, TMPText, Image, RawImage, FillColor, Button, TMPButton. " +
                   "Defaults: Text=UnityEngine.UI.Text, TMPText=TMPro.TextMeshProUGUI, " +
                   "Image=UnityEngine.UI.Image, RawImage=UnityEngine.UI.RawImage, " +
                   "Button=UnityEngine.UI.Button")]
            JObject componentMap = null)
        {
            if (layers == null || layers.Count == 0)
                return ToolResult.Error("layers array is empty");

            var pv = PathValidator.QuickValidate(prefabPath);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            // Ensure output directory exists
            var prefabDir = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(prefabDir) && !AssetDatabase.IsValidFolder(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
                AssetDatabase.Refresh();
            }

            // Import all exported images as sprites
            ImportImagesAsSprites(imageDir);

            // Build component type map (resolve custom types)
            var resolvedMap = BuildComponentMap(componentMap);

            // Create Canvas root
            var canvasGo = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create PSD UI");

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasScaler = canvasGo.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(canvasWidth, canvasHeight);

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasWidth, canvasHeight);

            // Track all created objects and images for naming
            var createdObjects = new List<object>();
            var imageFiles = new List<object>();

            var context = new CreateContext
            {
                CanvasWidth = canvasWidth,
                CanvasHeight = canvasHeight,
                ImageDir = imageDir,
                ComponentMap = resolvedMap,
                CreatedObjects = createdObjects,
                ImageFiles = imageFiles,
            };

            // Recursively create UI hierarchy
            // Top-level elements are relative to canvas center
            float canvasCenterX = canvasWidth / 2f;
            float canvasCenterY = canvasHeight / 2f;
            foreach (var layerToken in layers)
            {
                CreateLayerGameObject(layerToken, canvasGo.transform, "",
                    canvasCenterX, canvasCenterY, context);
            }

            // Save as prefab
            if (File.Exists(prefabPath))
                AssetDatabase.DeleteAsset(prefabPath);

            PrefabUtility.SaveAsPrefabAsset(canvasGo, prefabPath);
            Object.DestroyImmediate(canvasGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return ToolResult.Json(new
            {
                prefabPath,
                canvasWidth,
                canvasHeight,
                objectCount = createdObjects.Count,
                objects = createdObjects,
                imageCount = imageFiles.Count,
                images = imageFiles,
                resolvedComponents = resolvedMap.ToDictionary(kv => kv.Key, kv => kv.Value.FullName),
                message = $"Created UI prefab with {createdObjects.Count} objects and {imageFiles.Count} images at {prefabPath}. " +
                          "Use asset_move to rename image files, then update prefab object names as needed."
            });
        }

        #region ComponentMap

        private class CreateContext
        {
            public int CanvasWidth;
            public int CanvasHeight;
            public string ImageDir;
            public Dictionary<string, System.Type> ComponentMap;
            public List<object> CreatedObjects;
            public List<object> ImageFiles;

            public System.Type GetComponentType(string uiType)
            {
                if (!string.IsNullOrEmpty(uiType) && ComponentMap.TryGetValue(uiType, out var t))
                    return t;
                return null;
            }
        }

        private static Dictionary<string, System.Type> BuildComponentMap(JObject componentMap)
        {
            var result = new Dictionary<string, System.Type>();

            // Resolve defaults
            foreach (var kv in DefaultComponentTypes)
            {
                var type = System.Type.GetType(kv.Value);
                if (type != null)
                    result[kv.Key] = type;
            }

            // Override with custom mappings from user
            if (componentMap != null)
            {
                foreach (var kv in componentMap)
                {
                    var typeName = kv.Value?.ToString();
                    if (string.IsNullOrEmpty(typeName)) continue;
                    var type = ResolveType(typeName);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                        result[kv.Key] = type;
                }
            }

            return result;
        }

        private static System.Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // Try assembly-qualified name first
            var type = System.Type.GetType(typeName);
            if (type != null) return type;

            // Search all loaded assemblies by full name
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) return type;
            }

            // Search by short name in user/Unity assemblies
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = asm.GetName().Name;
                if (!asmName.StartsWith("Assembly-CSharp") &&
                    !asmName.StartsWith("Unity") &&
                    !asmName.StartsWith("TMPro"))
                    continue;

                try
                {
                    foreach (var t in asm.GetExportedTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch
                {
                    // Skip assemblies that can't enumerate types
                }
            }

            return null;
        }

        private static void SetProp(object component, string propName, object value)
        {
            var prop = component.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                try
                {
                    prop.SetValue(component, value);
                }
                catch
                {
                    // Type mismatch - skip silently
                }
            }
        }

        #endregion

        #region Layer Creation

        private static void CreateLayerGameObject(
            JToken layerToken, Transform parent, string parentPath,
            float parentCenterX, float parentCenterY, CreateContext ctx)
        {
            string name = layerToken["name"]?.ToString() ?? "Unnamed";
            string type = layerToken["type"]?.ToString() ?? "image";
            string uiType = layerToken["uiType"]?.ToString();
            int left = layerToken["left"]?.ToObject<int>() ?? 0;
            int top = layerToken["top"]?.ToObject<int>() ?? 0;
            int width = layerToken["width"]?.ToObject<int>() ?? 0;
            int height = layerToken["height"]?.ToObject<int>() ?? 0;
            int opacity = layerToken["opacity"]?.ToObject<int>() ?? 255;

            string objectPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

            // Check if this is a Button/TMPButton group
            if (type == "group" && (uiType == "Button" || uiType == "TMPButton"))
            {
                CreateButtonGameObject(layerToken, parent, objectPath, parentCenterX, parentCenterY, ctx, uiType);
                return;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            SetRectTransform(rect, left, top, width, height, parentCenterX, parentCenterY);

            float alpha = opacity / 255f;

            if (type == "text")
            {
                CreateTextComponent(go, layerToken, uiType ?? "Text", alpha, ctx);

                var textProps = layerToken["textProperties"];
                ctx.CreatedObjects.Add(new
                {
                    path = objectPath,
                    name,
                    type = "text",
                    uiType = uiType ?? "Text",
                    textContent = textProps?["content"]?.ToString() ?? "",
                });
            }
            else if (type == "fillcolor")
            {
                CreateFillColorComponent(go, layerToken, uiType ?? "FillColor", alpha, ctx);

                ctx.CreatedObjects.Add(new
                {
                    path = objectPath,
                    name,
                    type = "fillcolor",
                    uiType = uiType ?? "FillColor",
                });
            }
            else if (type == "image")
            {
                string imagePath = layerToken["imagePath"]?.ToString();
                CreateImageComponent(go, imagePath, uiType ?? "Image", alpha, ctx);

                ctx.CreatedObjects.Add(new
                {
                    path = objectPath,
                    name,
                    type = "image",
                    uiType = uiType ?? "Image",
                    imagePath = imagePath ?? "",
                });

                if (!string.IsNullOrEmpty(imagePath))
                {
                    ctx.ImageFiles.Add(new
                    {
                        assetPath = imagePath,
                        fileName = Path.GetFileName(imagePath),
                        objectPath,
                        originalLayerName = name,
                    });
                }
            }
            else if (type == "group")
            {
                ctx.CreatedObjects.Add(new
                {
                    path = objectPath,
                    name,
                    type = "group",
                    uiType = uiType ?? "Group",
                });

                var children = layerToken["children"] as JArray;
                if (children != null)
                {
                    // Children positions are relative to this group's center
                    float groupCenterX = left + width / 2f;
                    float groupCenterY = top + height / 2f;
                    foreach (var child in children)
                    {
                        CreateLayerGameObject(child, go.transform, objectPath,
                            groupCenterX, groupCenterY, ctx);
                    }
                }
            }
        }

        #endregion

        #region RectTransform

        private static void SetRectTransform(RectTransform rect, int left, int top, int width, int height,
            float parentCenterX, float parentCenterY)
        {
            // PSD: origin top-left, Y down. Unity UI: anchors at parent center, Y up
            // anchoredPosition is relative to parent's center (anchor 0.5, 0.5)
            float anchoredX = left + width / 2f - parentCenterX;
            float anchoredY = -(top + height / 2f - parentCenterY);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(anchoredX, anchoredY);
            rect.sizeDelta = new Vector2(width, height);
        }

        #endregion

        #region Text

        private static void CreateTextComponent(GameObject go, JToken layerToken, string uiType,
            float alpha, CreateContext ctx)
        {
            var textProps = layerToken["textProperties"];
            string content = textProps?["content"]?.ToString() ?? "";
            int fontSize = textProps?["fontSize"]?.ToObject<int>() ?? 16;
            string fontName = textProps?["fontName"]?.ToString() ?? "";
            string alignment = textProps?["alignment"]?.ToString() ?? "left";
            bool bold = textProps?["bold"]?.ToObject<bool>() ?? false;
            bool italic = textProps?["italic"]?.ToObject<bool>() ?? false;
            float lineSpacing = textProps?["lineSpacing"]?.ToObject<float>() ?? 1f;
            float charSpacing = textProps?["characterSpacing"]?.ToObject<float>() ?? 0f;

            var colorToken = textProps?["color"];
            Color textColor = Color.white;
            if (colorToken != null)
            {
                textColor = new Color(
                    colorToken["r"]?.ToObject<float>() ?? 1f,
                    colorToken["g"]?.ToObject<float>() ?? 1f,
                    colorToken["b"]?.ToObject<float>() ?? 1f,
                    colorToken["a"]?.ToObject<float>() ?? 1f
                );
            }

            var componentType = ctx.GetComponentType(uiType);

            // Default Text (fast path)
            if (componentType == typeof(Text))
            {
                var text = go.AddComponent<Text>();
                text.text = content;
                text.fontSize = fontSize;
                text.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a * alpha);
                text.alignment = ParseTextAnchor(alignment);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.lineSpacing = lineSpacing;

                if (bold && italic) text.fontStyle = FontStyle.BoldAndItalic;
                else if (bold) text.fontStyle = FontStyle.Bold;
                else if (italic) text.fontStyle = FontStyle.Italic;

                var font = FindFont(fontName);
                if (font != null) text.font = font;

                ApplyLayerEffects(go, layerToken);
                return;
            }

            // Custom or TMP text component - use reflection
            if (componentType == null) return;

            var comp = go.AddComponent(componentType);
            SetProp(comp, "text", content);
            SetProp(comp, "fontSize", (float)fontSize);
            SetProp(comp, "color", new Color(textColor.r, textColor.g, textColor.b, textColor.a * alpha));
            SetProp(comp, "raycastTarget", false);

            // lineSpacing: TMP uses percentage-based (0 = 1x), legacy uses multiplier
            if (typeof(Text).IsAssignableFrom(componentType))
                SetProp(comp, "lineSpacing", lineSpacing);
            else
                SetProp(comp, "lineSpacing", (lineSpacing - 1f) * 100f);

            SetProp(comp, "characterSpacing", charSpacing);

            // Font style via reflection
            if (bold || italic)
            {
                // Try TMP FontStyles first
                var fontStylesType = System.Type.GetType("TMPro.FontStyles, Unity.TextMeshPro");
                if (fontStylesType != null)
                {
                    int styleValue = 0;
                    if (bold) styleValue |= 1;
                    if (italic) styleValue |= 2;
                    SetProp(comp, "fontStyle", System.Enum.ToObject(fontStylesType, styleValue));
                }
                else
                {
                    // Legacy FontStyle
                    FontStyle style = FontStyle.Normal;
                    if (bold && italic) style = FontStyle.BoldAndItalic;
                    else if (bold) style = FontStyle.Bold;
                    else if (italic) style = FontStyle.Italic;
                    SetProp(comp, "fontStyle", style);
                }
            }

            // Alignment via reflection
            SetTextAlignment(comp, componentType, alignment);

            // Font lookup
            SetFontByReflection(comp, componentType, fontName);

            // Apply layer effects (Outline, Shadow, etc.)
            ApplyLayerEffects(go, layerToken);
        }

        private static void SetTextAlignment(object comp, System.Type componentType, string alignment)
        {
            // Try TMP alignment
            var tmpAlignType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
            if (tmpAlignType != null)
            {
                var alignProp = componentType.GetProperty("alignment");
                if (alignProp != null && alignProp.PropertyType == tmpAlignType)
                {
                    int alignValue = alignment?.ToLower() switch
                    {
                        "center" => 514,
                        "right" => 516,
                        _ => 513,
                    };
                    alignProp.SetValue(comp, System.Enum.ToObject(tmpAlignType, alignValue));
                    return;
                }
            }

            // Legacy TextAnchor
            if (typeof(Text).IsAssignableFrom(componentType))
            {
                SetProp(comp, "alignment", ParseTextAnchor(alignment));
            }
        }

        private static void SetFontByReflection(object comp, System.Type componentType, string fontName)
        {
            if (string.IsNullOrEmpty(fontName)) return;

            // Check if the component has a "font" property
            var fontProp = componentType.GetProperty("font", BindingFlags.Public | BindingFlags.Instance);
            if (fontProp == null) return;

            // TMP_FontAsset type
            var tmpFontType = System.Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (tmpFontType != null && fontProp.PropertyType.IsAssignableFrom(tmpFontType))
            {
                FindAndSetTMPFont(fontProp, comp, fontName);
                return;
            }

            // Legacy Font
            if (fontProp.PropertyType == typeof(Font))
            {
                var font = FindFont(fontName);
                if (font != null)
                    fontProp.SetValue(comp, font);
            }
        }

        private static TextAnchor ParseTextAnchor(string alignment)
        {
            return alignment?.ToLower() switch
            {
                "center" => TextAnchor.MiddleCenter,
                "right" => TextAnchor.MiddleRight,
                _ => TextAnchor.MiddleLeft,
            };
        }

        #endregion

        #region Font

        private static Font FindFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
                return null;

            var fontNameLower = fontName.TrimStart('\uFEFF').ToLower();
            if (string.IsNullOrEmpty(fontNameLower))
                return null;

            var guids = AssetDatabase.FindAssets("t:Font");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var assetName = Path.GetFileNameWithoutExtension(path).ToLower();
                if (assetName == fontNameLower || assetName.Contains(fontNameLower))
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                    if (font != null) return font;
                }
            }

            return null;
        }

        private static void FindAndSetTMPFont(PropertyInfo fontProp, object comp, string fontName)
        {
            var fontNameLower = fontName.TrimStart('\uFEFF').ToLower();
            if (string.IsNullOrEmpty(fontNameLower)) return;

            var tmpFontType = System.Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            if (tmpFontType == null) return;

            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var assetName = Path.GetFileNameWithoutExtension(path).ToLower();
                if (assetName == fontNameLower || assetName.Contains(fontNameLower))
                {
                    var fontAsset = AssetDatabase.LoadAssetAtPath(path, tmpFontType);
                    if (fontAsset != null)
                    {
                        fontProp.SetValue(comp, fontAsset);
                        return;
                    }
                }
            }
        }

        #endregion

        #region Effects

        private static void ApplyLayerEffects(GameObject go, JToken layerToken)
        {
            var effectsArray = layerToken["effects"] as JArray;
            if (effectsArray == null || effectsArray.Count == 0) return;

            foreach (var fx in effectsArray)
            {
                string fxType = fx["type"]?.ToString() ?? "";
                switch (fxType)
                {
                    case "stroke":
                        ApplyStrokeEffect(go, fx);
                        break;
                    case "dropShadow":
                        ApplyDropShadowEffect(go, fx);
                        break;
                    case "colorOverlay":
                        ApplyColorOverlay(go, fx);
                        break;
                }
            }
        }

        private static void ApplyStrokeEffect(GameObject go, JToken fx)
        {
            float size = fx["size"]?.ToObject<float>() ?? 1f;
            var colorToken = fx["color"];
            Color color = Color.black;
            if (colorToken != null)
            {
                color = new Color(
                    colorToken["r"]?.ToObject<float>() ?? 0f,
                    colorToken["g"]?.ToObject<float>() ?? 0f,
                    colorToken["b"]?.ToObject<float>() ?? 0f,
                    (fx["opacity"]?.ToObject<float>() ?? 100f) / 100f
                );
            }

            // Unity Outline component approximates PSD stroke
            var outline = go.AddComponent<Outline>();
            Undo.RegisterCreatedObjectUndo(outline, "Add Outline");
            outline.effectColor = color;
            // Outline effectDistance is in pixels; PSD stroke size maps roughly
            float dist = Mathf.Max(size * 0.5f, 1f);
            outline.effectDistance = new Vector2(dist, dist);
        }

        private static void ApplyDropShadowEffect(GameObject go, JToken fx)
        {
            var colorToken = fx["color"];
            Color color = Color.black;
            if (colorToken != null)
            {
                color = new Color(
                    colorToken["r"]?.ToObject<float>() ?? 0f,
                    colorToken["g"]?.ToObject<float>() ?? 0f,
                    colorToken["b"]?.ToObject<float>() ?? 0f,
                    (fx["opacity"]?.ToObject<float>() ?? 75f) / 100f
                );
            }

            float angle = fx["angle"]?.ToObject<float>() ?? 120f;
            float distance = fx["distance"]?.ToObject<float>() ?? 5f;

            // Convert PSD angle+distance to Unity Shadow offset (X, Y)
            float rad = angle * Mathf.Deg2Rad;
            float offsetX = Mathf.Round(Mathf.Cos(rad) * distance);
            float offsetY = Mathf.Round(Mathf.Sin(rad) * distance);

            var shadow = go.AddComponent<Shadow>();
            Undo.RegisterCreatedObjectUndo(shadow, "Add Shadow");
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(offsetX, -offsetY);
        }

        private static void ApplyColorOverlay(GameObject go, JToken fx)
        {
            // ColorOverlay overrides the text/graphic color
            var colorToken = fx["color"];
            if (colorToken == null) return;

            float opacity = (fx["opacity"]?.ToObject<float>() ?? 100f) / 100f;
            Color color = new Color(
                colorToken["r"]?.ToObject<float>() ?? 1f,
                colorToken["g"]?.ToObject<float>() ?? 1f,
                colorToken["b"]?.ToObject<float>() ?? 1f,
                opacity
            );

            // Apply to Text component
            var text = go.GetComponent<Text>();
            if (text != null)
            {
                text.color = color;
                return;
            }

            // Apply to any component with a 'color' property (e.g. TMP)
            var graphic = go.GetComponent<MaskableGraphic>();
            if (graphic != null)
            {
                graphic.color = color;
                return;
            }

            // Fallback: reflection for TMP or other custom components
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var colorProp = comp.GetType().GetProperty("color",
                    BindingFlags.Public | BindingFlags.Instance);
                if (colorProp != null && colorProp.PropertyType == typeof(Color))
                {
                    colorProp.SetValue(comp, color);
                    break;
                }
            }
        }

        #endregion

        #region Image

        private static void CreateImageComponent(GameObject go, string imagePath, string uiType,
            float alpha, CreateContext ctx)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(imagePath))
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);

            var componentType = ctx.GetComponentType(uiType);

            // Default Image (fast path)
            if (componentType == typeof(Image))
            {
                var image = go.AddComponent<Image>();
                if (sprite != null) image.sprite = sprite;
                image.color = new Color(1, 1, 1, alpha);
                image.raycastTarget = false;
                return;
            }

            // Default RawImage (fast path)
            if (componentType == typeof(RawImage))
            {
                var rawImage = go.AddComponent<RawImage>();
                if (sprite != null) rawImage.texture = sprite.texture;
                rawImage.color = new Color(1, 1, 1, alpha);
                rawImage.raycastTarget = false;
                return;
            }

            // Custom image component - use reflection
            if (componentType == null) return;

            var comp = go.AddComponent(componentType);
            SetProp(comp, "color", new Color(1, 1, 1, alpha));
            SetProp(comp, "raycastTarget", false);

            if (sprite != null)
            {
                // Try sprite first, then texture
                var spriteProp = componentType.GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
                if (spriteProp != null)
                    spriteProp.SetValue(comp, sprite);
                else
                    SetProp(comp, "texture", sprite.texture);
            }
        }

        #endregion

        #region FillColor

        private static void CreateFillColorComponent(GameObject go, JToken layerToken, string uiType,
            float alpha, CreateContext ctx)
        {
            var fillColorToken = layerToken["fillColor"];
            Color color = Color.white;
            if (fillColorToken != null)
            {
                color = new Color(
                    fillColorToken["r"]?.ToObject<float>() ?? 1f,
                    fillColorToken["g"]?.ToObject<float>() ?? 1f,
                    fillColorToken["b"]?.ToObject<float>() ?? 1f,
                    (fillColorToken["a"]?.ToObject<float>() ?? 1f) * alpha
                );
            }

            var componentType = ctx.GetComponentType(uiType);

            if (componentType == typeof(Image) || componentType == null)
            {
                var image = go.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                return;
            }

            if (componentType == typeof(RawImage))
            {
                var rawImage = go.AddComponent<RawImage>();
                rawImage.color = color;
                rawImage.raycastTarget = false;
                return;
            }

            // Custom component
            var comp = go.AddComponent(componentType);
            SetProp(comp, "color", color);
            SetProp(comp, "raycastTarget", false);
        }

        #endregion

        #region Button

        private static void CreateButtonGameObject(JToken layerToken, Transform parent,
            string objectPath, float parentCenterX, float parentCenterY,
            CreateContext ctx, string uiType)
        {
            string name = layerToken["name"]?.ToString() ?? "Button";
            int left = layerToken["left"]?.ToObject<int>() ?? 0;
            int top = layerToken["top"]?.ToObject<int>() ?? 0;
            int width = layerToken["width"]?.ToObject<int>() ?? 0;
            int height = layerToken["height"]?.ToObject<int>() ?? 0;

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            SetRectTransform(rect, left, top, width, height, parentCenterX, parentCenterY);

            // Button's children are relative to button center
            float btnCenterX = left + width / 2f;
            float btnCenterY = top + height / 2f;

            // Add Image component for button background
            var btnImage = go.AddComponent<Image>();
            btnImage.raycastTarget = true;

            // Add Button component (may be custom)
            var buttonType = ctx.GetComponentType(uiType) ?? typeof(Button);
            Component buttonComp;
            if (buttonType == typeof(Button))
                buttonComp = go.AddComponent<Button>();
            else
                buttonComp = go.AddComponent(buttonType);

            // Find sub-layers from children
            var children = layerToken["children"] as JArray;
            JToken bgLayer = null, textLayer = null;
            JToken highlightLayer = null, pressLayer = null, selectLayer = null, disableLayer = null;
            var otherChildren = new List<JToken>();

            if (children != null)
            {
                foreach (var child in children)
                {
                    string childUiType = child["uiType"]?.ToString();
                    string childType = child["type"]?.ToString();
                    switch (childUiType)
                    {
                        case "Background":
                        case "Image" when bgLayer == null && childType == "image":
                            bgLayer = child;
                            break;
                        case "Button_Text":
                        case "Text" when textLayer == null && childType == "text":
                        case "TMPText" when textLayer == null && childType == "text":
                            textLayer = child;
                            break;
                        case "Button_Highlight":
                            highlightLayer = child;
                            break;
                        case "Button_Press":
                            pressLayer = child;
                            break;
                        case "Button_Select":
                            selectLayer = child;
                            break;
                        case "Button_Disable":
                            disableLayer = child;
                            break;
                        default:
                            otherChildren.Add(child);
                            break;
                    }
                }
            }

            // Set background sprite
            if (bgLayer != null)
            {
                string bgImagePath = bgLayer["imagePath"]?.ToString();
                if (!string.IsNullOrEmpty(bgImagePath))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgImagePath);
                    if (sprite != null)
                        btnImage.sprite = sprite;

                    ctx.ImageFiles.Add(new
                    {
                        assetPath = bgImagePath,
                        fileName = Path.GetFileName(bgImagePath),
                        objectPath = objectPath + "/Background",
                        originalLayerName = bgLayer["name"]?.ToString() ?? "bg",
                    });
                }

                int bgOpacity = bgLayer["opacity"]?.ToObject<int>() ?? 255;
                btnImage.color = new Color(1, 1, 1, bgOpacity / 255f);
            }

            // Sprite state transitions (only for standard Button)
            if (buttonComp is Button button)
            {
                bool useSpriteSwap = highlightLayer != null || pressLayer != null ||
                                     selectLayer != null || disableLayer != null;
                button.transition = useSpriteSwap
                    ? Selectable.Transition.SpriteSwap
                    : Selectable.Transition.ColorTint;

                if (useSpriteSwap)
                {
                    var spriteState = new SpriteState();
                    spriteState.highlightedSprite = LoadSpriteFromLayer(highlightLayer);
                    spriteState.pressedSprite = LoadSpriteFromLayer(pressLayer);
                    spriteState.selectedSprite = LoadSpriteFromLayer(selectLayer);
                    spriteState.disabledSprite = LoadSpriteFromLayer(disableLayer);
                    button.spriteState = spriteState;
                }

                TrackButtonStateImage(highlightLayer, objectPath, ctx);
                TrackButtonStateImage(pressLayer, objectPath, ctx);
                TrackButtonStateImage(selectLayer, objectPath, ctx);
                TrackButtonStateImage(disableLayer, objectPath, ctx);
            }

            // Create text child
            if (textLayer != null)
            {
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.layer = LayerMask.NameToLayer("UI");
                textGo.transform.SetParent(go.transform, false);

                int tLeft = textLayer["left"]?.ToObject<int>() ?? 0;
                int tTop = textLayer["top"]?.ToObject<int>() ?? 0;
                int tWidth = textLayer["width"]?.ToObject<int>() ?? 0;
                int tHeight = textLayer["height"]?.ToObject<int>() ?? 0;
                int tOpacity = textLayer["opacity"]?.ToObject<int>() ?? 255;

                var textRect = textGo.GetComponent<RectTransform>();
                SetRectTransform(textRect, tLeft, tTop, tWidth, tHeight, btnCenterX, btnCenterY);

                // Determine text type: TMPButton uses TMPText, Button uses Text
                string textUiType = textLayer["uiType"]?.ToString();
                string resolvedTextType;
                if (textUiType == "TMPText")
                    resolvedTextType = "TMPText";
                else if (textUiType == "Text")
                    resolvedTextType = "Text";
                else
                    resolvedTextType = uiType == "TMPButton" ? "TMPText" : "Text";

                CreateTextComponent(textGo, textLayer, resolvedTextType, tOpacity / 255f, ctx);

                ctx.CreatedObjects.Add(new
                {
                    path = objectPath + "/Text",
                    name = textLayer["name"]?.ToString() ?? "Text",
                    type = "text",
                    uiType = resolvedTextType,
                    textContent = textLayer["textProperties"]?["content"]?.ToString() ?? "",
                });
            }

            // Create any remaining children
            foreach (var child in otherChildren)
            {
                CreateLayerGameObject(child, go.transform, objectPath,
                    btnCenterX, btnCenterY, ctx);
            }

            ctx.CreatedObjects.Add(new
            {
                path = objectPath,
                name,
                type = "button",
                uiType,
                hasBackground = bgLayer != null,
                hasText = textLayer != null,
                componentType = buttonType.FullName,
            });
        }

        private static Sprite LoadSpriteFromLayer(JToken layer)
        {
            if (layer == null) return null;
            string path = layer["imagePath"]?.ToString();
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void TrackButtonStateImage(JToken layer, string objectPath, CreateContext ctx)
        {
            if (layer == null) return;
            string imgPath = layer["imagePath"]?.ToString();
            if (string.IsNullOrEmpty(imgPath)) return;
            ctx.ImageFiles.Add(new
            {
                assetPath = imgPath,
                fileName = Path.GetFileName(imgPath),
                objectPath = objectPath + "/" + (layer["uiType"]?.ToString() ?? "state"),
                originalLayerName = layer["name"]?.ToString() ?? "",
            });
        }

        #endregion

        #region Import

        private static void ImportImagesAsSprites(string imageDir)
        {
            if (string.IsNullOrEmpty(imageDir) || !AssetDatabase.IsValidFolder(imageDir))
                return;

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { imageDir });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        #endregion
    }
}
