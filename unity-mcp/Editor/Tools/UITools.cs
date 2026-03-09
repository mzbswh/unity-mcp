using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("UI")]
    public static class UITools
    {
        [McpTool("ui_create_element", "Create a UI element (requires Canvas in scene)",
            Group = "ui")]
        public static ToolResult CreateElement(
            [Desc("Element type: 'text', 'image', 'button', 'panel', 'canvas', 'inputfield', 'slider', 'toggle'")] string type,
            [Desc("Name of the UI element")] string name = null,
            [Desc("Parent Canvas or UI element name/path")] string parent = null,
            [Desc("Text content (for text/button)")] string text = null)
        {
            // Ensure Canvas exists
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null && type.ToLower() != "canvas")
            {
                return ToolResult.Error("No Canvas found in scene. Create a canvas first with type='canvas'.");
            }

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectTools.FindGameObject(parent, null);
                if (parentGo != null)
                    parentTransform = parentGo.transform;
            }
            else if (canvas != null)
            {
                parentTransform = canvas.transform;
            }

            GameObject element;
            switch (type.ToLower())
            {
                case "canvas":
                    element = CreateCanvas(name ?? "Canvas");
                    break;
                case "text":
                    element = CreateText(name ?? "Text", text ?? "New Text", parentTransform);
                    break;
                case "image":
                    element = CreateImage(name ?? "Image", parentTransform);
                    break;
                case "button":
                    element = CreateButton(name ?? "Button", text ?? "Button", parentTransform);
                    break;
                case "panel":
                    element = CreatePanel(name ?? "Panel", parentTransform);
                    break;
                case "inputfield":
                    element = CreateInputField(name ?? "InputField", parentTransform);
                    break;
                case "slider":
                    element = CreateSlider(name ?? "Slider", parentTransform);
                    break;
                case "toggle":
                    element = CreateToggle(name ?? "Toggle", text ?? "Toggle", parentTransform);
                    break;
                default:
                    return ToolResult.Error($"Unknown UI type: {type}. Supported: text, image, button, panel, canvas, inputfield, slider, toggle");
            }

            UndoHelper.RegisterCreatedObject(element, $"Create UI {type}");
            Selection.activeGameObject = element;

            return ToolResult.Json(new
            {
                success = true,
                instanceId = element.GetInstanceID(),
                name = element.name,
                type,
                message = $"Created UI element: {element.name}"
            });
        }

        private static GameObject CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return go;
        }

        private static GameObject CreateText(string name, string content, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 50);
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);
            return go;
        }

        private static GameObject CreateButton(string name, string label, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            go.AddComponent<Button>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 40);

            // Add text child
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            return go;
        }

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.sizeDelta = Vector2.zero;

            return go;
        }

        private static GameObject CreateInputField(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>();
            var input = go.AddComponent<InputField>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);

            // Placeholder
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(go.transform, false);
            var phText = placeholder.AddComponent<Text>();
            phText.text = "Enter text...";
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phText.fontSize = 18;
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0.5f, 0.5f, 0.5f);
            phText.alignment = TextAnchor.MiddleLeft;
            var phRt = placeholder.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = Vector2.zero;
            phRt.offsetMin = new Vector2(10, 0);

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = new Vector2(10, 0);

            input.textComponent = text;
            input.placeholder = phText;

            return go;
        }

        private static GameObject CreateSlider(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 20);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.6f, 1f);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;

            slider.fillRect = fillRt;

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = Vector2.zero;

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20, 20);

            slider.handleRect = handleRt;

            return go;
        }

        private static GameObject CreateToggle(string name, string label, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var toggle = go.AddComponent<Toggle>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 30);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.sizeDelta = new Vector2(20, 20);
            bgRt.anchoredPosition = new Vector2(10, 0);

            // Checkmark
            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(bg.transform, false);
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.3f, 0.8f, 0.3f);
            var checkRt = checkmark.GetComponent<RectTransform>();
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.sizeDelta = new Vector2(-4, -4);

            toggle.graphic = checkImage;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(30, 0);

            return go;
        }
    }
}
