# Unity MCP Comprehensive Optimization Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 11 optimization areas from the comprehensive optimization design spec to make unity-mcp the most capable open-source Unity MCP implementation.

**Architecture:** C# Unity Editor plugin communicates with Python FastMCP server via custom TCP frame protocol. Tools/Resources/Prompts use attribute-based registration (`[McpTool]`, `[McpResource]`, `[McpPrompt]`) discovered by `ToolRegistry.ScanAll()`. All tool code runs on Unity main thread via `MainThreadDispatcher`.

**Tech Stack:** C# / Unity 2021.2+ / NUnit (EditMode tests) / Python 3.10+ / FastMCP / Newtonsoft.Json

**Spec:** `docs/superpowers/specs/2026-03-11-comprehensive-optimization-design.md`

---

## File Structure

### New Files
- `unity-mcp/Shared/Utils/PaginationHelper.cs` — Generic pagination utility
- `unity-mcp/Editor/Core/DependencyChecker.cs` — Python/uv/uvx detection
- `unity-mcp/Editor/Window/McpSetupWindow.cs` — Dependency setup wizard
- `unity-mcp/Editor/Window/McpSetupWindow.uxml` — Setup wizard layout
- `unity-mcp/Editor/Window/McpSetupWindow.uss` — Setup wizard styles
- `unity-mcp/Editor/Tools/CameraTools.cs` — Camera management tools
- `unity-mcp/Editor/Tools/TextureTools.cs` — Texture info/import tools
- `unity-mcp/Editor/Core/ToolCallLogger.cs` — Circular buffer call logger
- `unity-mcp/Editor/Core/PackageUpdateChecker.cs` — Version update detection
- `unity-mcp/Editor/Core/McpServices.cs` — Lightweight ServiceLocator
- `unity-mcp/Tests/Editor/PaginationHelperTests.cs` — Pagination tests
- `unity-mcp/Tests/Editor/DependencyCheckerTests.cs` — Dependency detection tests
- `unity-mcp/Tests/Editor/TestUtilities.cs` — Shared test helpers
- `unity-server/unity_mcp_server/tools/server_status.py` — Python server status resource
- `llms.txt` — LLM ecosystem discovery file
- `server.json` — MCP server discovery metadata

### Modified Files
- `unity-mcp/Shared/Models/Pagination.cs` — Add `Paginate<T>` generic method
- `unity-mcp/Editor/Resources/SceneResources.cs` — Add pagination to hierarchy
- `unity-mcp/Editor/Resources/ConsoleResources.cs` — Add pagination to logs
- `unity-mcp/Editor/Tools/AssetTools.cs` — Add pagination to `asset_find`
- `unity-mcp/Editor/Tools/GameObjectTools.cs` — Add pagination to `gameobject_find`
- `unity-mcp/Editor/Resources/EditorResources.cs` — Enhance with selection/scene dirty
- `unity-mcp/Editor/Resources/ProjectResources.cs` — Add packages, render pipeline
- `unity-mcp/Editor/Tools/EditorTools.cs` — Add `editor_refresh`
- `unity-mcp/Editor/Tools/ScriptTools.cs` — Add compile status check
- `unity-mcp/Editor/Core/McpServer.cs` — Integrate DependencyChecker, PackageUpdateChecker, McpServices
- `unity-mcp/Editor/Core/RequestHandler.cs` — Integrate ToolCallLogger
- `unity-mcp/Editor/Core/TcpTransport.cs` — Add ConnectionStats
- `unity-mcp/Shared/Interfaces/ITcpTransport.cs` — Add stats properties
- `unity-mcp/Editor/Window/Sections/McpAdvancedSection.cs` — Add call log + stats UI
- `unity-server/unity_mcp_server/server.py` — Register server status resource

---

## Chunk 1: Pagination Framework

### Task 1: Enhance PaginationHelper

**Files:**
- Modify: `unity-mcp/Shared/Models/Pagination.cs`
- Create: `unity-mcp/Shared/Utils/PaginationHelper.cs`
- Create: `unity-mcp/Tests/Editor/PaginationHelperTests.cs`

- [ ] **Step 1: Create PaginationHelper with generic Paginate method**

Create `unity-mcp/Shared/Utils/PaginationHelper.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityMcp.Shared.Models;

namespace UnityMcp.Shared.Utils
{
    public static class PaginationHelper
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 200;

        /// <summary>
        /// Paginate a list of items using cursor-based pagination.
        /// Returns the page items and a nextCursor (null if no more pages).
        /// </summary>
        public static (List<T> items, int total, string nextCursor) Paginate<T>(
            IList<T> allItems, int pageSize = DefaultPageSize, string cursor = null)
        {
            int start = Pagination.ParseCursor(cursor);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            int total = allItems.Count;
            var page = allItems.Skip(start).Take(pageSize).ToList();
            string next = Pagination.NextCursor(start, pageSize, total);
            return (page, total, next);
        }

        /// <summary>
        /// Create a paginated ToolResult from a list of items.
        /// </summary>
        public static ToolResult ToPaginatedResult<T>(
            IList<T> allItems, int pageSize = DefaultPageSize, string cursor = null)
        {
            var (items, total, next) = Paginate(allItems, pageSize, cursor);
            return ToolResult.Paginated(items, total, next);
        }
    }
}
```

- [ ] **Step 2: Write pagination tests**

Create `unity-mcp/Tests/Editor/PaginationHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Tests.Editor
{
    public class PaginationHelperTests
    {
        private List<int> _items;

        [SetUp]
        public void SetUp()
        {
            _items = Enumerable.Range(0, 150).ToList();
        }

        [Test]
        public void Paginate_FirstPage_ReturnsDefaultPageSize()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items);
            Assert.AreEqual(50, items.Count);
            Assert.AreEqual(150, total);
            Assert.AreEqual("50", next);
        }

        [Test]
        public void Paginate_WithCursor_ReturnsCorrectPage()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, cursor: "50");
            Assert.AreEqual(50, items.Count);
            Assert.AreEqual(50, items[0]);
            Assert.AreEqual("100", next);
        }

        [Test]
        public void Paginate_LastPage_ReturnsNullCursor()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, cursor: "100");
            Assert.AreEqual(50, items.Count);
            Assert.IsNull(next);
        }

        [Test]
        public void Paginate_CustomPageSize_Respected()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, pageSize: 10);
            Assert.AreEqual(10, items.Count);
            Assert.AreEqual("10", next);
        }

        [Test]
        public void Paginate_PageSizeClamped_ToMax200()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, pageSize: 500);
            Assert.AreEqual(150, items.Count); // all items, clamped to 200 but only 150 exist
        }

        [Test]
        public void Paginate_PageSizeClamped_ToMin1()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, pageSize: -5);
            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void Paginate_EmptyList_ReturnsEmpty()
        {
            var (items, total, next) = PaginationHelper.Paginate(new List<int>());
            Assert.AreEqual(0, items.Count);
            Assert.AreEqual(0, total);
            Assert.IsNull(next);
        }

        [Test]
        public void Paginate_InvalidCursor_StartsFromZero()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, cursor: "invalid");
            Assert.AreEqual(0, items[0]);
        }

        [Test]
        public void ToPaginatedResult_ReturnsSuccessResult()
        {
            var result = PaginationHelper.ToPaginatedResult(_items, pageSize: 10);
            Assert.IsTrue(result.IsSuccess);
        }
    }
}
```

- [ ] **Step 3: Commit pagination framework**

```bash
git add unity-mcp/Shared/Utils/PaginationHelper.cs unity-mcp/Tests/Editor/PaginationHelperTests.cs
git commit -m "feat: add generic PaginationHelper utility with tests"
```

### Task 2: Apply Pagination to Scene Hierarchy Resource

**Files:**
- Modify: `unity-mcp/Editor/Resources/SceneResources.cs`

- [ ] **Step 1: Add pageSize/cursor params to GetHierarchy**

Modify `SceneResources.GetHierarchy()` to accept pagination parameters. The hierarchy is flat-collected into a list, then paginated:

```csharp
[McpResource("unity://scene/hierarchy", "Scene Hierarchy",
    "Full hierarchy tree of the active scene (paginated)")]
public static ToolResult GetHierarchy()
{
    var scene = SceneManager.GetActiveScene();
    var roots = scene.GetRootGameObjects();
    var nodes = roots.Select(r => BuildNode(r.transform, 5, 0)).ToArray();

    return ToolResult.Json(new
    {
        sceneName = scene.name,
        scenePath = scene.path,
        rootCount = roots.Length,
        hierarchy = nodes
    });
}
```

Note: The scene hierarchy resource returns the full tree since it's a resource (not a tool) and is structurally nested. Pagination is more applicable to flat-list tools. Keep the resource as-is and focus pagination on tools that return flat lists.

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Editor/Resources/SceneResources.cs
git commit -m "docs: clarify scene hierarchy resource pagination strategy"
```

### Task 3: Apply Pagination to Flat-List Tools

**Files:**
- Modify: `unity-mcp/Editor/Tools/AssetTools.cs`
- Modify: `unity-mcp/Editor/Tools/GameObjectTools.cs`

- [ ] **Step 1: Add pagination to asset_find**

In `AssetTools.Find()`, replace `maxCount` with `pageSize`/`cursor` parameters:

```csharp
[McpTool("asset_find", "Search for assets using AssetDatabase (supports type filters like t:Texture)",
    Group = "asset", ReadOnly = true)]
public static ToolResult Find(
    [Desc("Search filter (e.g. 'Player t:Prefab', 't:Texture2D', 'l:MyLabel')")] string filter,
    [Desc("Folder paths to search in (e.g. Assets/Prefabs)")] string[] searchInFolders = null,
    [Desc("Page size (default 50, max 200)")] int pageSize = 50,
    [Desc("Pagination cursor from previous response")] string cursor = null)
{
    if (searchInFolders != null)
    {
        foreach (var folder in searchInFolders)
        {
            var pv = PathValidator.QuickValidate(folder);
            if (!pv.IsValid) return ToolResult.Error($"searchInFolders: {pv.Error}");
        }
    }

    var guids = searchInFolders != null && searchInFolders.Length > 0
        ? AssetDatabase.FindAssets(filter, searchInFolders)
        : AssetDatabase.FindAssets(filter);

    var allResults = guids.Select(guid =>
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return new
        {
            guid,
            path,
            name = System.IO.Path.GetFileNameWithoutExtension(path),
            type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name
        };
    }).ToArray();

    return PaginationHelper.ToPaginatedResult(allResults, pageSize, cursor);
}
```

Add `using UnityMcp.Shared.Utils;` to imports if not already present.

- [ ] **Step 2: Read GameObjectTools.cs to find the Find method**

Read the file to locate the `gameobject_find` tool and understand its current return format.

- [ ] **Step 3: Add pagination to gameobject_find (if it returns flat list)**

Apply the same pattern: collect all results into array, then use `PaginationHelper.ToPaginatedResult()`.

- [ ] **Step 4: Commit**

```bash
git add unity-mcp/Editor/Tools/AssetTools.cs unity-mcp/Editor/Tools/GameObjectTools.cs
git commit -m "feat: add cursor-based pagination to asset_find and gameobject_find"
```

---

## Chunk 2: Dependency Detection & Setup Window

### Task 4: DependencyChecker

**Files:**
- Create: `unity-mcp/Editor/Core/DependencyChecker.cs`
- Create: `unity-mcp/Tests/Editor/DependencyCheckerTests.cs`

- [ ] **Step 1: Create DependencyChecker**

```csharp
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UnityMcp.Editor.Core
{
    public static class DependencyChecker
    {
        public struct DependencyStatus
        {
            public bool PythonFound;
            public string PythonVersion;
            public bool UvFound;
            public string UvVersion;
            public bool UvxFound;
            public bool AllSatisfied => PythonFound && UvxFound;
        }

        public static DependencyStatus Check()
        {
            var status = new DependencyStatus();

            // Python
            string pythonCmd = GetPythonCommand();
            var (pythonOk, pythonOut) = RunCommand(pythonCmd, "--version");
            status.PythonFound = pythonOk;
            if (pythonOk)
                status.PythonVersion = ParseVersion(pythonOut);

            // uv
            var (uvOk, uvOut) = RunCommand("uv", "--version");
            status.UvFound = uvOk;
            if (uvOk)
                status.UvVersion = ParseVersion(uvOut);

            // uvx
            var (uvxOk, _) = RunCommand("uvx", "--version");
            status.UvxFound = uvxOk;

            return status;
        }

        private static string GetPythonCommand()
        {
#if UNITY_EDITOR_WIN
            return "python";
#else
            return "python3";
#endif
        }

        internal static (bool success, string output) RunCommand(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                if (process == null) return (false, null);
                string output = process.StandardOutput.ReadToEnd().Trim();
                if (string.IsNullOrEmpty(output))
                    output = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return (process.ExitCode == 0, output);
            }
            catch
            {
                return (false, null);
            }
        }

        internal static string ParseVersion(string output)
        {
            if (string.IsNullOrEmpty(output)) return null;
            var match = Regex.Match(output, @"(\d+\.\d+[\.\d]*)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
```

- [ ] **Step 2: Create DependencyChecker tests**

```csharp
using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class DependencyCheckerTests
    {
        [Test]
        public void ParseVersion_PythonFormat()
        {
            var version = DependencyChecker.ParseVersion("Python 3.12.1");
            Assert.AreEqual("3.12.1", version);
        }

        [Test]
        public void ParseVersion_UvFormat()
        {
            var version = DependencyChecker.ParseVersion("uv 0.5.1 (abcdef 2025-01-01)");
            Assert.AreEqual("0.5.1", version);
        }

        [Test]
        public void ParseVersion_NullInput()
        {
            var version = DependencyChecker.ParseVersion(null);
            Assert.IsNull(version);
        }

        [Test]
        public void ParseVersion_EmptyInput()
        {
            var version = DependencyChecker.ParseVersion("");
            Assert.IsNull(version);
        }

        [Test]
        public void ParseVersion_NoVersionInString()
        {
            var version = DependencyChecker.ParseVersion("no version here");
            Assert.IsNull(version);
        }

        [Test]
        public void Check_ReturnsStatus()
        {
            // Integration test: just verify it runs without exceptions
            var status = DependencyChecker.Check();
            // At minimum, AllSatisfied is a bool
            Assert.That(status.AllSatisfied, Is.TypeOf<bool>());
        }

        [Test]
        public void RunCommand_InvalidCommand_ReturnsFalse()
        {
            var (success, _) = DependencyChecker.RunCommand("nonexistent_command_xyz", "--version");
            Assert.IsFalse(success);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Core/DependencyChecker.cs unity-mcp/Tests/Editor/DependencyCheckerTests.cs
git commit -m "feat: add DependencyChecker for Python/uv/uvx detection with tests"
```

### Task 5: McpSetupWindow

**Files:**
- Create: `unity-mcp/Editor/Window/McpSetupWindow.cs`
- Create: `unity-mcp/Editor/Window/McpSetupWindow.uxml`
- Create: `unity-mcp/Editor/Window/McpSetupWindow.uss`
- Modify: `unity-mcp/Editor/Core/McpServer.cs`

- [ ] **Step 1: Create McpSetupWindow.uxml**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="setup-root">
        <ui:Label text="Unity MCP Setup" class="setup-title" />
        <ui:Label text="The following dependencies are required to run the MCP server." class="setup-subtitle" />

        <ui:VisualElement class="dep-section">
            <ui:VisualElement class="dep-row">
                <ui:VisualElement name="python-dot" class="dep-dot" />
                <ui:Label text="Python 3.10+" class="dep-name" />
                <ui:Label name="python-version" text="" class="dep-version" />
                <ui:Button name="btn-install-python" text="Install" class="dep-btn" />
            </ui:VisualElement>

            <ui:VisualElement class="dep-row">
                <ui:VisualElement name="uv-dot" class="dep-dot" />
                <ui:Label text="uv" class="dep-name" />
                <ui:Label name="uv-version" text="" class="dep-version" />
                <ui:Button name="btn-install-uv" text="Install" class="dep-btn" />
            </ui:VisualElement>

            <ui:VisualElement class="dep-row">
                <ui:VisualElement name="uvx-dot" class="dep-dot" />
                <ui:Label text="uvx" class="dep-name" />
                <ui:Label name="uvx-version" text="" class="dep-version" />
            </ui:VisualElement>
        </ui:VisualElement>

        <ui:VisualElement class="setup-actions">
            <ui:Button name="btn-refresh" text="Refresh" class="action-btn" />
            <ui:Button name="btn-done" text="Done" class="action-btn action-btn-primary" />
            <ui:Button name="btn-skip" text="Skip (not recommended)" class="action-btn action-btn-skip" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create McpSetupWindow.uss**

```css
.setup-root {
    padding: 16px;
}
.setup-title {
    font-size: 18px;
    -unity-font-style: bold;
    margin-bottom: 4px;
}
.setup-subtitle {
    font-size: 12px;
    color: #999;
    margin-bottom: 16px;
}
.dep-section {
    background-color: rgba(0,0,0,0.1);
    border-radius: 6px;
    padding: 12px;
    margin-bottom: 16px;
}
.dep-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: 8px;
    height: 28px;
}
.dep-dot {
    width: 10px;
    height: 10px;
    border-radius: 5px;
    margin-right: 8px;
    background-color: #666;
}
.dep-dot-green { background-color: #4caf50; }
.dep-dot-red { background-color: #f44336; }
.dep-name {
    font-size: 13px;
    -unity-font-style: bold;
    width: 100px;
}
.dep-version {
    font-size: 12px;
    color: #aaa;
    flex-grow: 1;
}
.dep-btn {
    width: 60px;
    height: 22px;
}
.setup-actions {
    flex-direction: row;
    justify-content: flex-end;
}
.action-btn {
    margin-left: 8px;
    padding: 4px 12px;
}
.action-btn-primary {
    background-color: #2196F3;
    color: white;
}
.action-btn-skip {
    color: #999;
    font-size: 11px;
}
```

- [ ] **Step 3: Create McpSetupWindow.cs**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMcp.Editor.Window
{
    public class McpSetupWindow : EditorWindow
    {
        private DependencyChecker.DependencyStatus _status;

        public static void ShowWindow(DependencyChecker.DependencyStatus status)
        {
            var wnd = GetWindow<McpSetupWindow>("Unity MCP Setup");
            wnd.minSize = new Vector2(400, 250);
            wnd.maxSize = new Vector2(500, 300);
            wnd._status = status;
        }

        public void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSetupWindow.uxml");
            if (tree != null) tree.CloneTree(rootVisualElement);

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.mzbswh.unity-mcp/Editor/Window/McpSetupWindow.uss");
            if (style != null) rootVisualElement.styleSheets.Add(style);

            rootVisualElement.Q<Button>("btn-install-python")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://www.python.org/downloads/"));
            rootVisualElement.Q<Button>("btn-install-uv")?.RegisterCallback<ClickEvent>(_ =>
                Application.OpenURL("https://docs.astral.sh/uv/getting-started/installation/"));

            rootVisualElement.Q<Button>("btn-refresh")?.RegisterCallback<ClickEvent>(_ => Refresh());
            rootVisualElement.Q<Button>("btn-done")?.RegisterCallback<ClickEvent>(_ =>
            {
                EditorPrefs.SetBool("UnityMcp_SetupDone", true);
                Close();
                Core.McpServer.Restart();
            });
            rootVisualElement.Q<Button>("btn-skip")?.RegisterCallback<ClickEvent>(_ =>
            {
                EditorPrefs.SetBool("UnityMcp_SetupDone", true);
                Close();
            });

            UpdateUI();
        }

        private void Refresh()
        {
            _status = Core.DependencyChecker.Check();
            UpdateUI();
        }

        private void UpdateUI()
        {
            SetDot("python-dot", _status.PythonFound);
            SetDot("uv-dot", _status.UvFound);
            SetDot("uvx-dot", _status.UvxFound);

            SetVersion("python-version", _status.PythonVersion);
            SetVersion("uv-version", _status.UvVersion);
            SetVersion("uvx-version", _status.UvxFound ? "available" : "");

            var doneBtn = rootVisualElement.Q<Button>("btn-done");
            if (doneBtn != null)
                doneBtn.SetEnabled(_status.AllSatisfied);
        }

        private void SetDot(string name, bool found)
        {
            var dot = rootVisualElement.Q(name);
            if (dot == null) return;
            dot.RemoveFromClassList("dep-dot-green");
            dot.RemoveFromClassList("dep-dot-red");
            dot.AddToClassList(found ? "dep-dot-green" : "dep-dot-red");
        }

        private void SetVersion(string name, string version)
        {
            var label = rootVisualElement.Q<Label>(name);
            if (label != null)
                label.text = string.IsNullOrEmpty(version) ? "Not found" : version;
        }
    }
}
```

- [ ] **Step 4: Integrate into McpServer.Initialize()**

In `McpServer.cs`, add dependency check before server start:

```csharp
// In Initialize(), after log settings sync, before tool scanning:
if (!EditorPrefs.GetBool("UnityMcp_SetupDone", false))
{
    var deps = DependencyChecker.Check();
    if (!deps.AllSatisfied)
    {
        McpSetupWindow.ShowWindow(deps);
        // Continue initialization anyway — TCP server works without Python
        // The setup window is informational for MCP client configuration
    }
}
```

Note: We do NOT block server initialization. The C# TCP server works independently. The setup window just guides users who need the Python bridge.

- [ ] **Step 5: Commit**

```bash
git add unity-mcp/Editor/Core/DependencyChecker.cs unity-mcp/Editor/Window/McpSetupWindow.cs \
       unity-mcp/Editor/Window/McpSetupWindow.uxml unity-mcp/Editor/Window/McpSetupWindow.uss \
       unity-mcp/Editor/Core/McpServer.cs
git commit -m "feat: add dependency detection and setup wizard window"
```

---

## Chunk 3: Resources Enhancement

### Task 6: Enhance EditorState Resource

**Files:**
- Modify: `unity-mcp/Editor/Resources/EditorResources.cs`

The current `unity://editor/state` resource already returns basic state. Enhance it with selection info and scene dirty state per spec section 3.1.

- [ ] **Step 1: Enhance GetEditorState()**

Add to the existing anonymous object in `EditorResources.GetEditorState()`:

```csharp
[McpResource("unity://editor/state", "Editor State",
    "Real-time editor state: compiling, playing, focused, selection, scene")]
public static ToolResult GetEditorState()
{
    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    return ToolResult.Json(new
    {
        isPlaying = EditorApplication.isPlaying,
        isPaused = EditorApplication.isPaused,
        isCompiling = EditorApplication.isCompiling,
        isUpdating = EditorApplication.isUpdating,
        timeSinceStartup = EditorApplication.timeSinceStartup,
        applicationFocused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
        activeScene = new
        {
            name = activeScene.name,
            path = activeScene.path,
            isDirty = activeScene.isDirty,
        },
        selection = new
        {
            count = Selection.objects.Length,
            activeObject = Selection.activeGameObject?.name,
            activeInstanceId = Selection.activeGameObject?.GetInstanceID(),
        },
        platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
        unityVersion = Application.unityVersion,
        mcpPort = McpServer.Transport?.Port,
        mcpClients = McpServer.Transport?.ClientCount ?? 0,
        mcpTools = McpServer.Registry?.ToolCount ?? 0,
        mcpResources = McpServer.Registry?.ResourceCount ?? 0,
        mcpPrompts = McpServer.Registry?.PromptCount ?? 0,
    });
}
```

Add `using UnityEditor;` and `using UnityEngine;` to imports if not already present.

- [ ] **Step 2: Add Selection Resource**

Add a new resource method to `EditorResources`:

```csharp
[McpResource("unity://editor/selection", "Current Selection",
    "Detailed info about currently selected objects in the editor")]
public static ToolResult GetSelection()
{
    var gameObjects = Selection.gameObjects;
    var activeGo = Selection.activeGameObject;

    var selected = gameObjects.Select(go => new
    {
        instanceId = go.GetInstanceID(),
        name = go.name,
        path = GetGameObjectPath(go),
        type = "GameObject",
        isActive = go == activeGo,
    }).ToArray();

    var assetPaths = Selection.assetGUIDs.Select(guid =>
        AssetDatabase.GUIDToAssetPath(guid)).ToArray();

    return ToolResult.Json(new
    {
        count = selected.Length,
        activeObject = activeGo?.name,
        gameObjects = selected,
        assetPaths,
    });
}

private static string GetGameObjectPath(GameObject go)
{
    var path = go.name;
    var t = go.transform.parent;
    while (t != null)
    {
        path = t.name + "/" + path;
        t = t.parent;
    }
    return path;
}
```

Add `using System.Linq;` to imports.

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Resources/EditorResources.cs
git commit -m "feat: enhance editor state resource with selection and scene dirty state"
```

### Task 7: Enhance ProjectInfo Resource

**Files:**
- Modify: `unity-mcp/Editor/Resources/ProjectResources.cs`

- [ ] **Step 1: Add render pipeline and packages info**

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("ProjectResources")]
    public static class ProjectResources
    {
        [McpResource("unity://project/info", "Project Info",
            "Unity project information including version, platform, render pipeline, and packages")]
        public static ToolResult GetProjectInfo()
        {
            return ToolResult.Json(new
            {
                projectName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                dataPath = Application.dataPath,
                platform = Application.platform.ToString(),
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(
                    EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                renderPipeline = GetRenderPipelineName(),
                packages = GetInstalledPackages(),
            });
        }

        private static string GetRenderPipelineName()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return "Built-in";
            var typeName = rp.GetType().Name;
            if (typeName.Contains("Universal") || typeName.Contains("URP"))
                return "URP";
            if (typeName.Contains("HighDefinition") || typeName.Contains("HDRP"))
                return "HDRP";
            return typeName;
        }

        private static object[] GetInstalledPackages()
        {
            // Read from Packages/manifest.json for a quick list
            try
            {
                var manifestPath = System.IO.Path.Combine(
                    Application.dataPath, "..", "Packages", "manifest.json");
                if (!System.IO.File.Exists(manifestPath)) return new object[0];
                var json = Newtonsoft.Json.Linq.JObject.Parse(
                    System.IO.File.ReadAllText(manifestPath));
                var deps = json["dependencies"] as Newtonsoft.Json.Linq.JObject;
                if (deps == null) return new object[0];
                return deps.Properties().Select(p => new
                {
                    name = p.Name,
                    version = p.Value.ToString()
                }).ToArray();
            }
            catch
            {
                return new object[0];
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Editor/Resources/ProjectResources.cs
git commit -m "feat: enhance project info resource with render pipeline and packages"
```

---

## Chunk 4: New Tools

### Task 8: CameraTools

**Files:**
- Create: `unity-mcp/Editor/Tools/CameraTools.cs`

- [ ] **Step 1: Create CameraTools**

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Camera")]
    public static class CameraTools
    {
        [McpTool("camera_create", "Create a new camera with specified settings",
            Group = "camera")]
        public static ToolResult Create(
            [Desc("Name for the camera GameObject")] string name = "New Camera",
            [Desc("Projection type: Perspective or Orthographic")] string projection = "Perspective",
            [Desc("Field of view (perspective only)")] float fov = 60f,
            [Desc("Position [x, y, z]")] float[] position = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create Camera '{name}'");
            var cam = go.AddComponent<Camera>();

            cam.orthographic = projection?.ToLower() == "orthographic";
            if (!cam.orthographic)
                cam.fieldOfView = Mathf.Clamp(fov, 1f, 179f);

            if (position != null && position.Length >= 3)
                go.transform.position = new Vector3(position[0], position[1], position[2]);

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                projection = cam.orthographic ? "Orthographic" : "Perspective",
                fov = cam.fieldOfView,
            });
        }

        [McpTool("camera_configure", "Configure camera settings",
            Group = "camera")]
        public static ToolResult Configure(
            [Desc("Camera name, path, or instanceId")] string target,
            [Desc("Field of view")] float? fov = null,
            [Desc("Near clip plane")] float? nearClip = null,
            [Desc("Far clip plane")] float? farClip = null,
            [Desc("Clear flags: Skybox, SolidColor, Depth, Nothing")] string clearFlags = null,
            [Desc("Background color hex (e.g. #000000)")] string backgroundColor = null,
            [Desc("Orthographic size")] float? orthoSize = null)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null) return ToolResult.Error($"Camera not found: {target}");
            var cam = go.GetComponent<Camera>();
            if (cam == null) return ToolResult.Error($"'{target}' has no Camera component");

            Undo.RecordObject(cam, "Configure Camera");

            if (fov.HasValue) cam.fieldOfView = Mathf.Clamp(fov.Value, 1f, 179f);
            if (nearClip.HasValue) cam.nearClipPlane = nearClip.Value;
            if (farClip.HasValue) cam.farClipPlane = farClip.Value;
            if (orthoSize.HasValue) cam.orthographicSize = orthoSize.Value;

            if (!string.IsNullOrEmpty(clearFlags))
            {
                cam.clearFlags = clearFlags.ToLower() switch
                {
                    "skybox" => CameraClearFlags.Skybox,
                    "solidcolor" => CameraClearFlags.SolidColor,
                    "depth" => CameraClearFlags.Depth,
                    "nothing" => CameraClearFlags.Nothing,
                    _ => cam.clearFlags,
                };
            }

            if (!string.IsNullOrEmpty(backgroundColor) &&
                ColorUtility.TryParseHtmlString(backgroundColor, out var color))
                cam.backgroundColor = color;

            return ToolResult.Json(new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                fov = cam.fieldOfView,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                clearFlags = cam.clearFlags.ToString(),
                orthographic = cam.orthographic,
            });
        }

        [McpTool("camera_get_info", "Get detailed camera parameters",
            Group = "camera", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Camera name, path, or instanceId (empty = Main Camera)")] string target = null)
        {
            Camera cam;
            if (string.IsNullOrEmpty(target))
            {
                cam = Camera.main;
                if (cam == null)
                    return ToolResult.Error("No Main Camera found in scene");
            }
            else
            {
                var go = GameObjectTools.FindGameObject(target, null);
                if (go == null) return ToolResult.Error($"Camera not found: {target}");
                cam = go.GetComponent<Camera>();
                if (cam == null) return ToolResult.Error($"'{target}' has no Camera component");
            }

            return ToolResult.Json(new
            {
                instanceId = cam.gameObject.GetInstanceID(),
                name = cam.gameObject.name,
                isMainCamera = cam.CompareTag("MainCamera"),
                projection = cam.orthographic ? "Orthographic" : "Perspective",
                fov = cam.fieldOfView,
                orthographicSize = cam.orthographicSize,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                clearFlags = cam.clearFlags.ToString(),
                backgroundColor = $"#{ColorUtility.ToHtmlStringRGBA(cam.backgroundColor)}",
                cullingMask = cam.cullingMask,
                depth = cam.depth,
                rect = new { cam.rect.x, cam.rect.y, cam.rect.width, cam.rect.height },
                position = new { x = cam.transform.position.x, y = cam.transform.position.y, z = cam.transform.position.z },
                rotation = new { x = cam.transform.eulerAngles.x, y = cam.transform.eulerAngles.y, z = cam.transform.eulerAngles.z },
            });
        }

        [McpTool("camera_look_at", "Point a camera at a target position or GameObject",
            Group = "camera")]
        public static ToolResult LookAt(
            [Desc("Camera name, path, or instanceId (empty = Main Camera)")] string camera = null,
            [Desc("Target position [x, y, z]")] float[] position = null,
            [Desc("Target GameObject name/path")] string targetObject = null)
        {
            Camera cam;
            if (string.IsNullOrEmpty(camera))
            {
                cam = Camera.main;
                if (cam == null) return ToolResult.Error("No Main Camera found");
            }
            else
            {
                var go = GameObjectTools.FindGameObject(camera, null);
                if (go == null) return ToolResult.Error($"Camera not found: {camera}");
                cam = go.GetComponent<Camera>();
                if (cam == null) return ToolResult.Error($"'{camera}' has no Camera component");
            }

            Vector3 targetPos;
            if (!string.IsNullOrEmpty(targetObject))
            {
                var targetGo = GameObjectTools.FindGameObject(targetObject, null);
                if (targetGo == null) return ToolResult.Error($"Target not found: {targetObject}");
                targetPos = targetGo.transform.position;
            }
            else if (position != null && position.Length >= 3)
            {
                targetPos = new Vector3(position[0], position[1], position[2]);
            }
            else
            {
                return ToolResult.Error("Provide either 'position' or 'targetObject'");
            }

            Undo.RecordObject(cam.transform, "Camera Look At");
            cam.transform.LookAt(targetPos);

            return ToolResult.Json(new
            {
                camera = cam.gameObject.name,
                lookingAt = new { x = targetPos.x, y = targetPos.y, z = targetPos.z },
                rotation = new { x = cam.transform.eulerAngles.x, y = cam.transform.eulerAngles.y, z = cam.transform.eulerAngles.z },
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Editor/Tools/CameraTools.cs
git commit -m "feat: add CameraTools (create, configure, get_info, look_at)"
```

### Task 9: TextureTools

**Files:**
- Create: `unity-mcp/Editor/Tools/TextureTools.cs`

- [ ] **Step 1: Create TextureTools**

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Texture")]
    public static class TextureTools
    {
        [McpTool("texture_get_info", "Get texture information (dimensions, format, compression, mipmap)",
            Group = "texture", ReadOnly = true)]
        public static ToolResult GetInfo(
            [Desc("Asset path (e.g. Assets/Textures/player.png)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return ToolResult.Error($"Not a texture asset: {path}");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            return ToolResult.Json(new
            {
                path,
                name = Path.GetFileNameWithoutExtension(path),
                width = tex?.width,
                height = tex?.height,
                textureType = importer.textureType.ToString(),
                textureShape = importer.textureShape.ToString(),
                sRGB = importer.sRGBTexture,
                alphaSource = importer.alphaSource.ToString(),
                alphaIsTransparency = importer.alphaIsTransparency,
                mipmapEnabled = importer.mipmapEnabled,
                filterMode = importer.filterMode.ToString(),
                wrapMode = importer.wrapMode.ToString(),
                maxTextureSize = importer.maxTextureSize,
                textureCompression = importer.textureCompression.ToString(),
                readWriteEnabled = importer.isReadable,
                spriteMode = importer.spriteImportMode.ToString(),
            });
        }

        [McpTool("texture_set_import", "Modify texture import settings",
            Group = "texture")]
        public static ToolResult SetImport(
            [Desc("Asset path")] string path,
            [Desc("Max texture size (32, 64, 128, 256, 512, 1024, 2048, 4096, 8192)")] int? maxSize = null,
            [Desc("Compression: None, LowQuality, NormalQuality, HighQuality")] string compression = null,
            [Desc("Filter mode: Point, Bilinear, Trilinear")] string filterMode = null,
            [Desc("Enable mipmaps")] bool? mipmapEnabled = null,
            [Desc("Texture type: Default, NormalMap, Sprite, Cursor, Cookie, Lightmap, SingleChannel")] string textureType = null,
            [Desc("Read/Write enabled")] bool? readWrite = null,
            [Desc("sRGB color texture")] bool? sRGB = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return ToolResult.Error($"Not a texture asset: {path}");

            if (maxSize.HasValue) importer.maxTextureSize = maxSize.Value;
            if (mipmapEnabled.HasValue) importer.mipmapEnabled = mipmapEnabled.Value;
            if (readWrite.HasValue) importer.isReadable = readWrite.Value;
            if (sRGB.HasValue) importer.sRGBTexture = sRGB.Value;

            if (!string.IsNullOrEmpty(compression))
            {
                if (System.Enum.TryParse<TextureImporterCompression>(compression, true, out var comp))
                    importer.textureCompression = comp;
            }
            if (!string.IsNullOrEmpty(filterMode))
            {
                if (System.Enum.TryParse<FilterMode>(filterMode, true, out var fm))
                    importer.filterMode = fm;
            }
            if (!string.IsNullOrEmpty(textureType))
            {
                if (System.Enum.TryParse<TextureImporterType>(textureType, true, out var tt))
                    importer.textureType = tt;
            }

            importer.SaveAndReimport();
            return ToolResult.Text($"Updated texture import settings for: {path}");
        }

        [McpTool("texture_search", "Search for texture assets by criteria",
            Group = "texture", ReadOnly = true)]
        public static ToolResult Search(
            [Desc("Search filter name (optional)")] string nameFilter = null,
            [Desc("Folder to search (e.g. Assets/Textures)")] string folder = null,
            [Desc("Min width filter")] int? minWidth = null,
            [Desc("Max results")] int pageSize = 50,
            [Desc("Pagination cursor")] string cursor = null)
        {
            string filter = "t:Texture2D";
            if (!string.IsNullOrEmpty(nameFilter))
                filter = nameFilter + " " + filter;

            var folders = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            var guids = folders != null
                ? AssetDatabase.FindAssets(filter, folders)
                : AssetDatabase.FindAssets(filter);

            var results = guids.Select(guid =>
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                return new
                {
                    path = p,
                    name = Path.GetFileNameWithoutExtension(p),
                    width = tex?.width ?? 0,
                    height = tex?.height ?? 0,
                };
            });

            if (minWidth.HasValue)
                results = results.Where(r => r.width >= minWidth.Value);

            var allResults = results.ToArray();
            return PaginationHelper.ToPaginatedResult(allResults, pageSize, cursor);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Editor/Tools/TextureTools.cs
git commit -m "feat: add TextureTools (get_info, set_import, search)"
```

### Task 10: EditorTools Enhancement — editor_refresh and compile status

**Files:**
- Modify: `unity-mcp/Editor/Tools/EditorTools.cs`

- [ ] **Step 1: Add editor_refresh tool**

Add the following method to `EditorTools`:

```csharp
[McpTool("editor_refresh", "Force refresh the AssetDatabase",
    Group = "editor")]
public static ToolResult Refresh()
{
    AssetDatabase.Refresh();
    return ToolResult.Text("AssetDatabase refreshed");
}
```

- [ ] **Step 2: Add editor_get_compile_status tool**

Add another method to `EditorTools`:

```csharp
[McpTool("editor_get_compile_status", "Check if scripts are currently compiling and get compilation state",
    Group = "editor", ReadOnly = true)]
public static ToolResult GetCompileStatus()
{
    return ToolResult.Json(new
    {
        isCompiling = EditorApplication.isCompiling,
        isUpdating = EditorApplication.isUpdating,
        isPlaying = EditorApplication.isPlaying,
        message = EditorApplication.isCompiling
            ? "Scripts are compiling. Wait before making scene changes."
            : "Ready. No compilation in progress."
    });
}
```

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Tools/EditorTools.cs
git commit -m "feat: add editor_refresh and editor_get_compile_status tools"
```

---

## Chunk 5: Connection Diagnostics & Version Check

### Task 11: ToolCallLogger

**Files:**
- Create: `unity-mcp/Editor/Core/ToolCallLogger.cs`
- Modify: `unity-mcp/Editor/Core/RequestHandler.cs`

- [ ] **Step 1: Create ToolCallLogger with circular buffer**

```csharp
using System;
using System.Collections.Generic;

namespace UnityMcp.Editor.Core
{
    public static class ToolCallLogger
    {
        public struct CallRecord
        {
            public string ToolName;
            public long DurationMs;
            public bool Success;
            public DateTime Timestamp;
        }

        private const int MaxRecords = 20;
        private static readonly CallRecord[] _buffer = new CallRecord[MaxRecords];
        private static int _head;
        private static int _count;

        public static void Log(string tool, long durationMs, bool success)
        {
            _buffer[_head] = new CallRecord
            {
                ToolName = tool,
                DurationMs = durationMs,
                Success = success,
                Timestamp = DateTime.Now,
            };
            _head = (_head + 1) % MaxRecords;
            if (_count < MaxRecords) _count++;
        }

        public static List<CallRecord> GetHistory()
        {
            var list = new List<CallRecord>(_count);
            // Read from oldest to newest
            int start = _count < MaxRecords ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % MaxRecords;
                list.Add(_buffer[idx]);
            }
            return list;
        }

        public static void Clear()
        {
            _head = 0;
            _count = 0;
        }
    }
}
```

- [ ] **Step 2: Integrate into RequestHandler**

In `RequestHandler.HandleToolsCall()`, the audit logging already exists at line ~140. Add ToolCallLogger.Log right after:

```csharp
// After existing McpLogger.Audit call:
ToolCallLogger.Log(toolName, sw.ElapsedMilliseconds, result.IsSuccess);
```

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Core/ToolCallLogger.cs unity-mcp/Editor/Core/RequestHandler.cs
git commit -m "feat: add ToolCallLogger circular buffer for recent call history"
```

### Task 12: ConnectionStats in TcpTransport

**Files:**
- Modify: `unity-mcp/Shared/Interfaces/ITcpTransport.cs`
- Modify: `unity-mcp/Editor/Core/TcpTransport.cs`

- [ ] **Step 1: Add stats properties to ITcpTransport**

```csharp
// Add to ITcpTransport interface:
int ReconnectCount { get; }
DateTime? LastConnectedAt { get; }
```

- [ ] **Step 2: Implement in TcpTransport**

Add fields and implement properties in `TcpTransport`:

```csharp
// New fields:
private int _reconnectCount;
private DateTime? _lastConnectedAt;

// New properties:
public int ReconnectCount => _reconnectCount;
public DateTime? LastConnectedAt => _lastConnectedAt;
```

In the client accept/connect handler method (where new clients are accepted), set `_lastConnectedAt = DateTime.Now;`.

In the `Start()` method, if `IsRunning` was previously true (restart case), increment `_reconnectCount`.

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Shared/Interfaces/ITcpTransport.cs unity-mcp/Editor/Core/TcpTransport.cs
git commit -m "feat: add ConnectionStats (ReconnectCount, LastConnectedAt) to TcpTransport"
```

### Task 13: PackageUpdateChecker

**Files:**
- Create: `unity-mcp/Editor/Core/PackageUpdateChecker.cs`
- Modify: `unity-mcp/Editor/Core/McpServer.cs`

- [ ] **Step 1: Create PackageUpdateChecker**

```csharp
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    public static class PackageUpdateChecker
    {
        private const string PackageJsonUrl =
            "https://raw.githubusercontent.com/mzbswh/unity-mcp/main/unity-mcp/package.json";
        private const string PrefKeyLastCheck = "UnityMcp_LastUpdateCheck";
        private const string PrefKeyLatestVersion = "UnityMcp_LatestVersion";

        public static string LatestVersion =>
            EditorPrefs.GetString(PrefKeyLatestVersion, null);

        public static bool HasUpdate
        {
            get
            {
                var latest = LatestVersion;
                if (string.IsNullOrEmpty(latest)) return false;
                return latest != McpConst.ServerVersion &&
                       IsNewer(latest, McpConst.ServerVersion);
            }
        }

        public static void CheckOncePerDay()
        {
            var lastCheck = EditorPrefs.GetString(PrefKeyLastCheck, "");
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (lastCheck == today) return;

            EditorPrefs.SetString(PrefKeyLastCheck, today);

            var request = UnityWebRequest.Get(PackageJsonUrl);
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(request.downloadHandler.text);
                        var version = json["version"]?.ToString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            EditorPrefs.SetString(PrefKeyLatestVersion, version);
                            if (IsNewer(version, McpConst.ServerVersion))
                                McpLogger.Info($"Unity MCP update available: v{version} (current: v{McpConst.ServerVersion})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    McpLogger.Debug($"Update check failed: {ex.Message}");
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (Version.TryParse(candidate, out var cVer) &&
                Version.TryParse(current, out var curVer))
                return cVer > curVer;
            return false;
        }
    }
}
```

- [ ] **Step 2: Integrate into McpServer.Initialize()**

Add at the end of `McpServer.Initialize()`, before `s_initialized = true`:

```csharp
PackageUpdateChecker.CheckOncePerDay();
```

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Core/PackageUpdateChecker.cs unity-mcp/Editor/Core/McpServer.cs
git commit -m "feat: add PackageUpdateChecker for daily version update detection"
```

---

## Chunk 6: Settings Window Enhancement

### Task 14: Enhance McpAdvancedSection with Call Log and Stats

**Files:**
- Modify: `unity-mcp/Editor/Window/Sections/McpAdvancedSection.cs`

- [ ] **Step 1: Add tool call log display**

In `McpAdvancedSection.BuildUI()`, after the diagnostics box, add a call log section:

```csharp
// Call Log section
var logBox = new VisualElement();
logBox.AddToClassList("section-box");
logBox.style.marginTop = 8;

var logTitle = new Label("Recent Tool Calls");
logTitle.AddToClassList("section-title");
logBox.Add(logTitle);

var logContainer = new VisualElement { name = "call-log-container" };
logBox.Add(logContainer);
_root.Add(logBox);
```

Store a reference to `logContainer` and populate it in `RefreshDiagnostics()`:

```csharp
var logContainer = _root.Q("call-log-container");
if (logContainer != null)
{
    logContainer.Clear();
    var history = ToolCallLogger.GetHistory();
    if (history.Count == 0)
    {
        logContainer.Add(new Label("No tool calls recorded yet.") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
    }
    else
    {
        // Show most recent first
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var record = history[i];
            var row = new VisualElement();
            row.AddToClassList("diag-row");
            var nameLabel = new Label($"{record.ToolName}");
            nameLabel.AddToClassList("diag-key");
            nameLabel.style.width = 200;
            row.Add(nameLabel);
            var statusLabel = new Label($"{record.DurationMs}ms {(record.Success ? "OK" : "ERR")}");
            statusLabel.AddToClassList("diag-value");
            statusLabel.style.color = record.Success ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
            row.Add(statusLabel);
            logContainer.Add(row);
        }
    }
}
```

- [ ] **Step 2: Add connection stats to diagnostics**

In `RefreshDiagnostics()`, add:

```csharp
// After existing diagnostics labels, add:
var reconnects = transport?.ReconnectCount ?? 0;
var lastConnected = transport?.LastConnectedAt;
// Add these as new diag rows in BuildUI and update here
```

Add two new `CreateDiagRow` calls in `BuildUI()`:

```csharp
diagBox.Add(CreateDiagRow("Reconnects:", "diag-reconnects"));
diagBox.Add(CreateDiagRow("Last Connected:", "diag-last-connected"));
```

And update in `RefreshDiagnostics()`:

```csharp
var diagReconnects = _root.Q<Label>("diag-reconnects");
if (diagReconnects != null)
    diagReconnects.text = reconnects.ToString();

var diagLastConnected = _root.Q<Label>("diag-last-connected");
if (diagLastConnected != null)
    diagLastConnected.text = lastConnected?.ToString("HH:mm:ss") ?? "—";
```

- [ ] **Step 3: Add update notification**

In `RefreshDiagnostics()`, check for updates and show banner:

```csharp
if (PackageUpdateChecker.HasUpdate)
{
    var updateBanner = _root.Q("update-banner");
    if (updateBanner == null)
    {
        updateBanner = new VisualElement { name = "update-banner" };
        updateBanner.style.backgroundColor = new Color(0.85f, 0.65f, 0.0f, 0.3f);
        updateBanner.style.borderBottomLeftRadius = updateBanner.style.borderBottomRightRadius =
            updateBanner.style.borderTopLeftRadius = updateBanner.style.borderTopRightRadius = 4;
        updateBanner.style.paddingTop = updateBanner.style.paddingBottom = 4;
        updateBanner.style.paddingLeft = updateBanner.style.paddingRight = 8;
        updateBanner.style.marginBottom = 8;
        var label = new Label($"Update available: v{PackageUpdateChecker.LatestVersion} (current: v{McpConst.ServerVersion})");
        label.style.color = new Color(0.9f, 0.7f, 0.0f);
        updateBanner.Add(label);
        _root.Insert(0, updateBanner);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add unity-mcp/Editor/Window/Sections/McpAdvancedSection.cs
git commit -m "feat: enhance Advanced section with call log, connection stats, update banner"
```

---

## Chunk 7: ServiceLocator, Python Server & Ecosystem Files

### Task 15: McpServices ServiceLocator

**Files:**
- Create: `unity-mcp/Editor/Core/McpServices.cs`
- Modify: `unity-mcp/Editor/Core/McpServer.cs`

- [ ] **Step 1: Create McpServices**

```csharp
using UnityMcp.Shared.Interfaces;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Lightweight service locator for core MCP services.
    /// Enables testability by allowing mock registration.
    /// </summary>
    public static class McpServices
    {
        public static IToolRegistry ToolRegistry { get; set; }
        public static ITcpTransport Transport { get; set; }
        public static RequestHandler RequestHandler { get; set; }

        /// <summary>Reset all services (for testing).</summary>
        public static void Reset()
        {
            ToolRegistry = null;
            Transport = null;
            RequestHandler = null;
        }
    }
}
```

- [ ] **Step 2: Register services in McpServer.Initialize()**

After creating instances in `McpServer.Initialize()`:

```csharp
// After creating Registry, s_handler, Transport:
McpServices.ToolRegistry = Registry;
McpServices.Transport = Transport;
McpServices.RequestHandler = s_handler;
```

- [ ] **Step 3: Commit**

```bash
git add unity-mcp/Editor/Core/McpServices.cs unity-mcp/Editor/Core/McpServer.cs
git commit -m "feat: add McpServices ServiceLocator for testability"
```

### Task 16: Python Server Status Resource

**Files:**
- Modify: `unity-server/unity_mcp_server/server.py`

- [ ] **Step 1: Add server status resource**

Add a Python-side resource at the end of `server.py`, before `main()`:

```python
@mcp.resource("unity://server/status")
def server_status() -> str:
    """Python MCP server status and connection info."""
    import time
    status = {
        "serverName": "Unity MCP Python Bridge",
        "version": __version__,
        "connected": unity is not None and unity.connected if unity else False,
        "registeredTools": len(_registered_tools),
        "registeredResources": len(_registered_resources),
        "registeredPrompts": len(_registered_prompts),
        "transport": TRANSPORT,
        "unityHost": UNITY_HOST,
        "unityPort": UNITY_PORT,
    }
    return json.dumps(status, indent=2)
```

- [ ] **Step 2: Commit**

```bash
git add unity-server/unity_mcp_server/server.py
git commit -m "feat: add unity://server/status resource to Python server"
```

### Task 17: LLM Ecosystem Files

**Files:**
- Create: `llms.txt` (project root)
- Create: `server.json` (project root)

- [ ] **Step 1: Create llms.txt**

Read the current tool list from the codebase to populate. Create at project root:

```
# Unity MCP
> Model Context Protocol server for Unity Editor integration

Unity MCP provides tools, resources, and prompts for AI assistants to control
Unity Editor. It connects via a TCP bridge between a Python MCP server and the
Unity C# editor plugin.

## Tools
### Scene
- scene_create: Create a new empty scene
- scene_open: Open a scene by path
- scene_save: Save the current scene
- scene_get_hierarchy: Get the scene hierarchy tree

### GameObject
- gameobject_create: Create a new GameObject
- gameobject_destroy: Delete a GameObject
- gameobject_find: Find GameObjects by name/tag/layer
- gameobject_get_info: Get detailed info about a GameObject
- gameobject_modify: Modify GameObject properties

### Component
- component_add: Add a component to a GameObject
- component_remove: Remove a component
- component_modify: Modify component properties
- component_list: List components on a GameObject

### Asset
- asset_find: Search for assets
- asset_create_folder: Create asset folders
- asset_move: Move/rename assets
- asset_delete: Delete assets

### Editor
- editor_get_state: Get editor state (playing, compiling, etc.)
- editor_set_playmode: Control play mode
- editor_execute_menu: Execute menu items
- editor_selection_get: Get current selection
- editor_selection_set: Set editor selection
- editor_refresh: Refresh AssetDatabase
- editor_undo: Undo
- editor_redo: Redo

### Camera
- camera_create: Create a camera
- camera_configure: Configure camera settings
- camera_get_info: Get camera parameters
- camera_look_at: Point camera at target

### Material
- material_create: Create materials
- material_modify: Modify material properties

### Script
- script_create: Create C# scripts
- script_read: Read script content
- script_search: Search scripts

### Prefab
- prefab_create: Create prefabs
- prefab_instantiate: Instantiate prefabs

### Texture
- texture_get_info: Get texture info
- texture_set_import: Modify import settings
- texture_search: Search textures

## Resources
- unity://editor/state: Editor state (compiling, playing, selection)
- unity://editor/selection: Currently selected objects
- unity://project/info: Project metadata and packages
- unity://scene/hierarchy: Scene hierarchy tree
- unity://scene/list: Build scenes and loaded scenes
- unity://console/logs: Recent console logs
- unity://server/status: Python server connection status

## Prompts
- script_create_prompt: Generate C# script templates
- architecture_review: Review project architecture
```

Note: This is a representative list. The actual implementation should read from the registry at build time or be manually maintained. For now, create a best-effort static file.

- [ ] **Step 2: Create server.json**

```json
{
  "name": "unity-mcp-server",
  "version": "1.0.0",
  "description": "MCP server for Unity Editor — provides tools, resources, and prompts for AI-driven game development",
  "transport": ["stdio", "streamable-http"],
  "repository": "https://github.com/mzbswh/unity-mcp",
  "homepage": "https://github.com/mzbswh/unity-mcp#readme"
}
```

- [ ] **Step 3: Commit**

```bash
git add llms.txt server.json
git commit -m "feat: add llms.txt and server.json LLM ecosystem discovery files"
```

---

## Chunk 8: Test Coverage

### Task 18: Test Utilities

**Files:**
- Create: `unity-mcp/Tests/Editor/TestUtilities.cs`

- [ ] **Step 1: Create shared test utilities**

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMcp.Shared.Models;

namespace UnityMcp.Tests.Editor
{
    public static class TestUtilities
    {
        private static readonly List<GameObject> _tempObjects = new();

        /// <summary>Create a temporary GameObject that is auto-cleaned after each test.</summary>
        public static GameObject CreateTempGameObject(string name = "TestObject")
        {
            var go = new GameObject(name);
            _tempObjects.Add(go);
            return go;
        }

        /// <summary>Destroy all temp GameObjects. Call from [TearDown].</summary>
        public static void CleanUp()
        {
            foreach (var go in _tempObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _tempObjects.Clear();
        }

        /// <summary>Assert that a ToolResult is successful and contains expected JSON key.</summary>
        public static JToken AssertSuccessAndParse(ToolResult result)
        {
            Assert.IsTrue(result.IsSuccess, $"Expected success but got error: {result.ErrorMessage}");
            Assert.IsNotNull(result.Content);
            return result.Content;
        }

        /// <summary>Assert that a ToolResult is an error with expected message substring.</summary>
        public static void AssertError(ToolResult result, string expectedSubstring = null)
        {
            Assert.IsFalse(result.IsSuccess, "Expected error but got success");
            if (expectedSubstring != null)
                Assert.That(result.ErrorMessage, Does.Contain(expectedSubstring));
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Tests/Editor/TestUtilities.cs
git commit -m "feat: add TestUtilities helper for shared test infrastructure"
```

### Task 19: Enhance ToolRegistry Tests

**Files:**
- Modify: `unity-mcp/Tests/Editor/ToolRegistryTests.cs`

- [ ] **Step 1: Add enable/disable and resource matching tests**

Add the following tests to the existing `ToolRegistryTests`:

```csharp
[Test]
public void SetToolEnabled_DisablesTool()
{
    _registry.ScanAll();
    var tools = _registry.GetToolList();
    if (tools.Count == 0) Assert.Ignore("No tools registered");
    var firstName = tools[0]["name"].ToString();

    _registry.SetToolEnabled(firstName, false);
    Assert.IsFalse(_registry.IsToolEnabled(firstName));
    Assert.IsNull(_registry.GetTool(firstName), "Disabled tool should not be returned");

    _registry.SetToolEnabled(firstName, true);
    Assert.IsTrue(_registry.IsToolEnabled(firstName));
    Assert.IsNotNull(_registry.GetTool(firstName));
}

[Test]
public void MatchResource_FindsExistingUri()
{
    _registry.ScanAll();
    var entry = _registry.MatchResource("unity://editor/state");
    Assert.IsNotNull(entry, "Should match unity://editor/state resource");
}

[Test]
public void MatchResource_ReturnsNullForUnknown()
{
    _registry.ScanAll();
    var entry = _registry.MatchResource("unity://nonexistent/thing");
    Assert.IsNull(entry);
}

[Test]
public void GetAllToolEntries_IncludesDisabled()
{
    _registry.ScanAll();
    var all = _registry.GetAllToolEntries();
    int count = 0;
    foreach (var _ in all) count++;
    Assert.AreEqual(_registry.ToolCount + CountDisabled(), count);
    // Note: ToolCount only counts items in dict, GetAllToolEntries returns all regardless
}
```

Note: Some tests may need adjustment based on actual registry behavior. The key is to increase coverage of edge cases.

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Tests/Editor/ToolRegistryTests.cs
git commit -m "test: enhance ToolRegistry tests with enable/disable and resource matching"
```

### Task 20: RequestHandler Tests

**Files:**
- Create: `unity-mcp/Tests/Editor/RequestHandlerTests.cs`

- [ ] **Step 1: Create RequestHandler tests**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class RequestHandlerTests
    {
        private ToolRegistry _registry;
        private RequestHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _registry = new ToolRegistry();
            _registry.ScanAll();
            _handler = new RequestHandler(_registry, 30000);
        }

        [Test]
        public async Task HandleRequest_InvalidJson_ReturnsParseError()
        {
            var response = await _handler.HandleRequest("not valid json{{{");
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32700, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_UnknownMethod_ReturnsMethodNotFound()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "nonexistent/method"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32601, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_Ping_ReturnsEmptyResult()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "ping"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNull(json["error"]);
        }

        [Test]
        public async Task HandleRequest_ToolsList_ReturnsTools()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/list"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNotNull(json["result"]["tools"]);
            Assert.That(json["result"]["tools"].Count(), Is.GreaterThan(0));
        }

        [Test]
        public async Task HandleRequest_ResourcesList_ReturnsResources()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "resources/list"
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.IsNotNull(json["result"]["resources"]);
        }

        [Test]
        public async Task HandleRequest_ToolsCall_MissingName_ReturnsInvalidParams()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JObject()
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["error"]);
            Assert.AreEqual(-32602, json["error"]["code"].Value<int>());
        }

        [Test]
        public async Task HandleRequest_Initialize_ReturnsServerInfo()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JObject
                {
                    ["clientInfo"] = new JObject { ["name"] = "test", ["version"] = "1.0" }
                }
            }.ToString();

            var response = await _handler.HandleRequest(request);
            var json = JObject.Parse(response);
            Assert.IsNotNull(json["result"]);
            Assert.AreEqual("2024-11-05", json["result"]["protocolVersion"].ToString());
            Assert.IsNotNull(json["result"]["serverInfo"]);
        }

        [Test]
        public async Task HandleNotification_DoesNotThrow()
        {
            // Should just log and return
            await _handler.HandleNotification("{\"method\":\"test/notification\"}");
            await _handler.HandleNotification("invalid json");
            Assert.Pass("Notifications should not throw");
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Tests/Editor/RequestHandlerTests.cs
git commit -m "test: add RequestHandler tests for JSON-RPC protocol handling"
```

### Task 21: ToolCallLogger Tests

**Files:**
- Create: `unity-mcp/Tests/Editor/ToolCallLoggerTests.cs`

- [ ] **Step 1: Create ToolCallLogger tests**

```csharp
using NUnit.Framework;
using UnityMcp.Editor.Core;

namespace UnityMcp.Tests.Editor
{
    public class ToolCallLoggerTests
    {
        [SetUp]
        public void SetUp()
        {
            ToolCallLogger.Clear();
        }

        [Test]
        public void Log_SingleCall_InHistory()
        {
            ToolCallLogger.Log("test_tool", 42, true);
            var history = ToolCallLogger.GetHistory();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("test_tool", history[0].ToolName);
            Assert.AreEqual(42, history[0].DurationMs);
            Assert.IsTrue(history[0].Success);
        }

        [Test]
        public void Log_ExceedsBuffer_OldestDropped()
        {
            for (int i = 0; i < 25; i++)
                ToolCallLogger.Log($"tool_{i}", i, true);

            var history = ToolCallLogger.GetHistory();
            Assert.AreEqual(20, history.Count);
            // Oldest should be tool_5 (first 5 dropped)
            Assert.AreEqual("tool_5", history[0].ToolName);
            Assert.AreEqual("tool_24", history[19].ToolName);
        }

        [Test]
        public void Clear_EmptiesHistory()
        {
            ToolCallLogger.Log("test", 1, true);
            ToolCallLogger.Clear();
            Assert.AreEqual(0, ToolCallLogger.GetHistory().Count);
        }

        [Test]
        public void GetHistory_EmptyByDefault()
        {
            Assert.AreEqual(0, ToolCallLogger.GetHistory().Count);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add unity-mcp/Tests/Editor/ToolCallLoggerTests.cs
git commit -m "test: add ToolCallLogger tests for circular buffer behavior"
```

### Task 22: Final Commit — Update CLAUDE.md and llms.txt

**Files:**
- Modify: `CLAUDE.md` (if exists, update tool list)
- Verify all changes compile and are consistent

- [ ] **Step 1: Review all changes**

Run `git diff --stat feat/progressive-optimization` to review the full scope of changes.

- [ ] **Step 2: Update CLAUDE.md with new tools/resources**

Add CameraTools, TextureTools, editor_refresh, editor_get_compile_status to the tool list in CLAUDE.md.

- [ ] **Step 3: Final commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with new tools and resources"
```

---

## Summary

| Chunk | Tasks | Description |
|-------|-------|-------------|
| 1 | 1-3 | Pagination framework + apply to tools |
| 2 | 4-5 | Dependency detection + setup wizard |
| 3 | 6-7 | Resources enhancement (editor state, project info) |
| 4 | 8-10 | New tools (Camera, Texture, editor enhancements) |
| 5 | 11-13 | ToolCallLogger, ConnectionStats, PackageUpdateChecker |
| 6 | 14 | Settings window Advanced section enhancements |
| 7 | 15-17 | ServiceLocator, Python server resource, LLM files |
| 8 | 18-22 | Test coverage + documentation |

**Total: 22 tasks, ~65 steps**
