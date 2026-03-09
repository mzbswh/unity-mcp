using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("SpecialtyPrompts2")]
    public static class SpecialtyPrompts2
    {
        [McpPrompt("xr_development",
            "XR development guide: XR Plugin Management, spatial interaction, performance targets, gesture input")]
        public static ToolResult XrDevelopment()
        {
            return ToolResult.Text(@"# XR Development Guide

## Setup (XR Plugin Management)
1. Install: Edit > Project Settings > XR Plugin Management > Install
2. Select target: Oculus, OpenXR, ARCore, ARKit
3. Install XR Interaction Toolkit: `com.unity.xr.interaction.toolkit`

## XR Interaction Toolkit Architecture
```
XR Origin (Camera Offset)
├── Main Camera (TrackedPoseDriver)
├── Left Controller (XRController + XRDirectInteractor/XRRayInteractor)
├── Right Controller (XRController + XRDirectInteractor/XRRayInteractor)
└── Locomotion System
    ├── Snap Turn Provider
    ├── Teleportation Provider
    └── Continuous Move Provider
```

## Performance Targets (VR)
| Metric | Target |
|--------|--------|
| Frame rate | 72/90/120 FPS (must never drop!) |
| Frame time | < 11ms (90fps) |
| Draw calls | < 100 |
| Triangles | < 100K per eye |
| Texture memory | < 256MB |

## Common Patterns
- **Grab interaction**: XRGrabInteractable on objects + Rigidbody
- **UI in VR**: World Space Canvas + TrackedDeviceGraphicRaycaster
- **Teleportation**: TeleportationArea/Anchor components on floor objects
- **Hand tracking**: use XR Hands package (`com.unity.xr.hands`)

## AR Development (ARFoundation)
- Use `com.unity.xr.arfoundation` for cross-platform AR
- Plane detection: `ARPlaneManager` → place objects on surfaces
- Image tracking: `ARTrackedImageManager` → recognize predefined images
- Face tracking: `ARFaceManager` → face mesh, expressions
- Light estimation: `ARCameraManager.frameReceived` → adjust scene lighting

## Tips
- Use Single Pass Instanced rendering (halves GPU draw calls for stereo)
- Avoid post-processing in VR (bloom, DOF cause discomfort)
- Maintain stable frame rate — frame drops cause motion sickness
- Use fixed foveated rendering if available (reduces peripheral resolution)
- Test on actual hardware — XR performance varies dramatically from Editor");
        }

        [McpPrompt("ecs_dots_guide",
            "ECS/DOTS guide: Entity design, System organization, Burst compilation, Job scheduling patterns")]
        public static ToolResult EcsDotsGuide()
        {
            return ToolResult.Text(@"# ECS / DOTS Guide (Entities 1.0+)

## Core Concepts
- **Entity**: ID only (no class, no MonoBehaviour)
- **Component**: struct data (IComponentData) — no methods, no logic
- **System**: logic that operates on component queries
- **World**: container for entities + systems

## Component Design
```csharp
// Components are pure data structs
public struct Health : IComponentData
{
    public float Current;
    public float Max;
}

public struct MoveSpeed : IComponentData
{
    public float Value;
}

// Tag component (zero-size, for filtering)
public struct IsPlayer : IComponentData { }

// Buffer element (dynamic array per entity)
public struct InventorySlot : IBufferElementData
{
    public Entity Item;
    public int Count;
}
```

## System (ISystem — unmanaged, Burst-compatible)
```csharp
[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (transform, speed) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
        {
            transform.ValueRW.Position += new float3(0, 0, speed.ValueRO.Value * dt);
        }
    }
}
```

## Jobs + Burst (Parallel Processing)
```csharp
[BurstCompile]
public partial struct DamageJob : IJobEntity
{
    public float DamageAmount;

    void Execute(ref Health health, in IsPlayer tag)
    {
        health.Current -= DamageAmount;
        if (health.Current < 0) health.Current = 0;
    }
}

// Schedule in system:
new DamageJob { DamageAmount = 10f }.ScheduleParallel();
```

## Entity Creation
```csharp
// Baking (convert GameObject → Entity at build time)
public class HealthAuthoring : MonoBehaviour
{
    public float maxHealth = 100;
}

public class HealthBaker : Baker<HealthAuthoring>
{
    public override void Bake(HealthAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new Health
        {
            Current = authoring.maxHealth,
            Max = authoring.maxHealth
        });
    }
}
```

## Tips
- Use Burst for all systems (10-100x speedup over managed code)
- Prefer `IJobEntity` over manual `EntityQuery` iteration
- Use `EntityCommandBuffer` for structural changes (add/remove components)
- Profile with Entities Profiler modules (Archetypes, Systems)
- Start with SystemBase (managed) for prototyping, migrate to ISystem for performance");
        }

        [McpPrompt("terrain_guide",
            "Terrain system guide: Terrain Layers, trees/grass, terrain LOD, procedural generation")]
        public static ToolResult TerrainGuide()
        {
            return ToolResult.Text(@"# Terrain System Guide

## Terrain Setup
1. Create: GameObject > 3D Object > Terrain
2. Configure size: Terrain Settings > Mesh Resolution (heightmap, detail, control)
3. Typical sizes: 500x500 (small), 1000x1000 (medium), 2000x2000 (large)
4. Heightmap Resolution: 513 (small), 1025 (medium), 2049 (large)

## Terrain Layers (Textures)
- Add via Paint Terrain > Paint Texture > Edit Terrain Layers
- Use 4 layers max per terrain for best performance (single pass)
- Each layer: Diffuse + Normal + Mask Map (metallic, AO, smoothness)
- Tiling Size: adjust to match terrain scale (15-30 for most terrains)

## Trees
- Paint Trees tool: add tree prototypes (prefabs)
- Use SpeedTree or custom LOD meshes for tree models
- Billboards auto-generated for distant trees
- Density: use Tree Distance in Terrain Settings to control draw distance
- Wind: enable on SpeedTree materials for natural movement

## Grass & Detail
- Paint Details: add grass textures or mesh prototypes
- Detail Distance: 80-150 (how far details render)
- Detail Density: balance visual quality vs performance
- Use GPU instancing for detail meshes (Wind Zone affects grass)

## Terrain LOD
- Pixel Error: 5 (quality) to 50 (performance) — controls mesh simplification
- Base Map Distance: distance at which terrain uses low-res base texture
- Heightmap Resolution: higher = more detail but more memory
- Use **Terrain Groups** for multi-tile terrains (seamless neighboring)

## Neighbor Stitching
```csharp
// Connect terrain tiles for seamless borders
terrain1.SetNeighbors(left: null, top: terrain2, right: terrain3, bottom: null);
terrain1.Flush(); // Apply connections
```

## Procedural Terrain
```csharp
var data = terrain.terrainData;
int res = data.heightmapResolution;
float[,] heights = new float[res, res];
for (int y = 0; y < res; y++)
    for (int x = 0; x < res; x++)
        heights[y, x] = Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 0.5f;
data.SetHeights(0, 0, heights);
```

## Performance Tips
- Use Draw Instanced (Terrain Settings) for GPU instancing
- Limit tree/detail density on mobile
- Use terrain holes for caves/tunnels (Terrain > Paint Holes)
- Consider Mesh Terrain for small/static areas (lower overhead than Terrain system)");
        }

        [McpPrompt("custom_editor",
            "Custom Editor tool development: PropertyDrawer, EditorWindow, IMGUI vs UI Toolkit")]
        public static ToolResult CustomEditor()
        {
            return ToolResult.Text(@"# Custom Editor Tool Development

## PropertyDrawer (Customize Inspector fields)
```csharp
[CustomPropertyDrawer(typeof(RangedFloat))]
public class RangedFloatDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var minProp = property.FindPropertyRelative(""min"");
        var maxProp = property.FindPropertyRelative(""max"");
        float min = minProp.floatValue, max = maxProp.floatValue;

        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);
        EditorGUI.MinMaxSlider(position, ref min, ref max, 0, 100);
        minProp.floatValue = min;
        maxProp.floatValue = max;
        EditorGUI.EndProperty();
    }
}
```

## CustomEditor (Customize entire Inspector)
```csharp
[CustomEditor(typeof(EnemySpawner))]
public class EnemySpawnerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty(""_spawnRate""));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(""_prefabs""));

        if (GUILayout.Button(""Spawn Test Enemy""))
            ((EnemySpawner)target).SpawnTestEnemy();

        serializedObject.ApplyModifiedProperties();
    }
}
```

## EditorWindow
```csharp
public class LevelDesignerWindow : EditorWindow
{
    [MenuItem(""Tools/Level Designer"")]
    static void Open() => GetWindow<LevelDesignerWindow>(""Level Designer"");

    private Vector2 _scrollPos;
    private string _levelName;

    void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        _levelName = EditorGUILayout.TextField(""Level Name"", _levelName);

        if (GUILayout.Button(""Generate Level""))
            GenerateLevel();

        EditorGUILayout.EndScrollView();
    }
}
```

## IMGUI vs UI Toolkit for Editor
| Feature | IMGUI | UI Toolkit |
|---------|-------|-----------|
| Paradigm | Immediate mode (OnGUI) | Retained mode (UXML+USS) |
| Layout | Manual Rect / GUILayout | Flexbox (auto-layout) |
| Styling | Limited (GUIStyle) | Full CSS-like (USS) |
| Performance | Redraws every frame | Only redraws on change |
| Learning curve | Lower (simple) | Higher (web-like) |
| Future | Maintenance mode | Actively developed |

## UI Toolkit for Editor (Recommended for new tools)
```csharp
public class MyToolWindow : EditorWindow
{
    [MenuItem(""Tools/My Tool"")]
    static void Open() => GetWindow<MyToolWindow>();

    void CreateGUI()
    {
        var root = rootVisualElement;
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(""Assets/Editor/MyTool.uxml"");
        uxml.CloneTree(root);

        root.Q<Button>(""generate-btn"").clicked += OnGenerate;
    }
}
```

## Tips
- Always use `SerializedProperty` (not direct field access) for undo support
- Call `serializedObject.ApplyModifiedProperties()` to save changes
- Use `Undo.RecordObject` before modifying non-serialized data
- `EditorGUIUtility.singleLineHeight` for consistent line spacing
- `EditorApplication.delayCall` for operations after GUI layout");
        }

        [McpPrompt("render_pipeline",
            "Render pipeline guide: URP vs HDRP selection, Custom Render Pass, Render Feature")]
        public static ToolResult RenderPipeline()
        {
            return ToolResult.Text(@"# Render Pipeline Guide

## URP vs HDRP Selection
| Criteria | URP | HDRP |
|----------|-----|------|
| Target platforms | All (mobile, PC, console, WebGL) | PC, Console only |
| Visual quality | Good (stylized, mobile) | Photorealistic |
| Performance | Optimized for wide range | Requires powerful GPU |
| Features | Core rendering, 2D renderer | Volumetrics, ray tracing, SSS |
| Recommended for | Most games, mobile, indie | AAA, architectural viz, film |

## URP Setup
- Create URP Asset: Create > Rendering > URP Asset (with Universal Renderer)
- Assign in Project Settings > Graphics > Scriptable Render Pipeline Settings
- Quality levels: create multiple URP Assets (Low/Medium/High)

## Custom Render Pass (URP)
```csharp
public class OutlineRenderPass : ScriptableRenderPass
{
    private Material _outlineMaterial;
    private FilteringSettings _filtering;

    public OutlineRenderPass(Material material, LayerMask layer)
    {
        _outlineMaterial = material;
        _filtering = new FilteringSettings(RenderQueueRange.opaque, layer);
        renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData data)
    {
        var cmd = CommandBufferPool.Get(""Outline"");
        var drawSettings = CreateDrawingSettings(
            new ShaderTagId(""UniversalForward""), ref data, SortingCriteria.CommonOpaque);
        drawSettings.overrideMaterial = _outlineMaterial;
        context.DrawRenderers(data.cullResults, ref drawSettings, ref _filtering);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
```

## Renderer Feature (URP)
```csharp
public class OutlineFeature : ScriptableRendererFeature
{
    [SerializeField] private Material _material;
    [SerializeField] private LayerMask _layer;
    private OutlineRenderPass _pass;

    public override void Create()
    {
        _pass = new OutlineRenderPass(_material, _layer);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        renderer.EnqueuePass(_pass);
    }
}
```
Add to URP Renderer Data: Inspector > Add Renderer Feature > Outline Feature

## Common Render Features
- **Full Screen Pass**: post-processing effects (custom shaders)
- **Render Objects**: render specific layers with override material
- **Decal Renderer Feature**: projective decals (bullet holes, paint)

## Tips
- Don't mix Built-in and SRP shaders — they're incompatible
- Use Shader Graph for cross-pipeline shader development
- URP 2D Renderer: specialized for 2D games (2D lights, shadow casters)
- HDRP: use Volume system for per-area visual settings
- Profile with Frame Debugger to understand render pass order");
        }

        [McpPrompt("multiplayer_setup",
            "Multiplayer game setup guide: Netcode for GameObjects, Lobby, Relay, Transport selection")]
        public static ToolResult MultiplayerSetup()
        {
            return ToolResult.Text(@"# Multiplayer Game Setup Guide

## Unity Gaming Services Stack
```
Netcode for GameObjects (NGO)   ← Game networking framework
Unity Transport                  ← Network transport layer
Unity Relay                      ← NAT traversal (no port forwarding)
Unity Lobby                      ← Matchmaking, room management
Unity Authentication             ← Player identity
```

## Setup Steps
1. Install packages: `com.unity.netcode.gameobjects`, `com.unity.services.relay`, `com.unity.services.lobby`
2. Create NetworkManager GameObject with NetworkManager component
3. Set Transport: Unity Transport (UDP) or WebSocket Transport (WebGL)
4. Configure: Player Prefab, Network Prefabs list

## Basic Host/Client Flow
```csharp
// Host (server + client)
NetworkManager.Singleton.StartHost();

// Client (join)
NetworkManager.Singleton.StartClient();

// Server only (headless)
NetworkManager.Singleton.StartServer();
```

## With Relay (No Port Forwarding)
```csharp
// Host creates relay
var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers: 4);
var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
transport.SetRelayServerData(new RelayServerData(allocation, ""dtls""));
NetworkManager.Singleton.StartHost();

// Client joins with code
var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
transport.SetRelayServerData(new RelayServerData(joinAllocation, ""dtls""));
NetworkManager.Singleton.StartClient();
```

## Lobby (Matchmaking)
```csharp
// Create lobby
var lobby = await LobbyService.Instance.CreateLobbyAsync(""My Game"", maxPlayers: 4,
    new CreateLobbyOptions {
        Data = new Dictionary<string, DataObject> {
            { ""joinCode"", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
        }
    });

// Query lobbies
var lobbies = await LobbyService.Instance.QueryLobbiesAsync();

// Join
var joined = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
string joinCode = joined.Data[""joinCode""].Value;
```

## Transport Comparison
| Transport | Protocol | Platform | Best For |
|-----------|----------|----------|----------|
| Unity Transport | UDP | All | Most games |
| WebSocket | TCP/WS | WebGL + all | Browser games |
| Steam Networking | Valve P2P | PC | Steam games |

## Tips
- Use Relay for all games (eliminates NAT/firewall issues)
- Heartbeat lobbies every 15s to keep them alive
- Use DTLS encryption with Relay for security
- Test with ParrelSync (clone editor) for local multiplayer testing
- Use Network Scene Management for synchronized scene loading");
        }

        [McpPrompt("procedural_generation",
            "Procedural generation guide: noise algorithms, map generation, room layout, seed system")]
        public static ToolResult ProceduralGeneration()
        {
            return ToolResult.Text(@"# Procedural Generation Guide

## Noise Algorithms
| Algorithm | Use Case |
|-----------|----------|
| Perlin Noise | Terrain height, smooth gradients |
| Simplex Noise | Better Perlin (less artifacts, faster in higher dimensions) |
| Voronoi | Cell-based regions, biomes, crystal patterns |
| White Noise | Random placement, star fields |
| Fractal/fBm | Layered noise for natural detail (octaves of Perlin) |

## Unity Noise
```csharp
// Built-in Perlin (2D only)
float value = Mathf.PerlinNoise(x * frequency, y * frequency);

// Fractal Brownian Motion (layered noise)
float FBM(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
{
    float sum = 0, amplitude = 1, frequency = 1;
    for (int i = 0; i < octaves; i++)
    {
        sum += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }
    return sum;
}
```

## Dungeon Generation (BSP)
```
1. Start with full rectangle
2. Split recursively (Binary Space Partition)
3. Create room in each leaf node
4. Connect rooms via corridors (connect siblings)

Split → [Left | Right]
       [Room A] [Room B]
       └── corridor ──┘
```

## Wave Function Collapse (WFC)
- Tile-based generation with adjacency rules
- Each tile defines which neighbors are valid
- Collapse: pick lowest-entropy cell, choose random valid tile, propagate constraints
- Great for: dungeon rooms, city blocks, platformer levels

## Seed System
```csharp
public class WorldGenerator
{
    private System.Random _rng;

    public void Generate(int seed)
    {
        _rng = new System.Random(seed);
        // All Random calls through _rng for reproducibility
        float value = (float)_rng.NextDouble();
    }
}
// Same seed = same world every time
```

## Biome System
- Use Voronoi + noise to assign biome per region
- Temperature (Perlin Y-axis) + Moisture (Perlin different seed) → biome lookup table
- Blend terrain textures at biome borders using splatmaps

## Tips
- Always use seeded Random (not UnityEngine.Random) for reproducible results
- Generate in chunks for infinite worlds (cache generated chunks)
- Use coroutines or Jobs to spread generation across frames
- Preview in Editor with Gizmos for fast iteration
- Store generation parameters in ScriptableObject for designer tweaking");
        }

        [McpPrompt("inventory_system",
            "Inventory/item system design: data model, UI binding, drag & drop, persistence")]
        public static ToolResult InventorySystem()
        {
            return ToolResult.Text(@"# Inventory & Item System Design

## Data Model
```csharp
[CreateAssetMenu(menuName = ""Game/Item Data"")]
public class ItemData : ScriptableObject
{
    public string itemId;
    public string displayName;
    public string description;
    public Sprite icon;
    public ItemType type;
    public int maxStack = 99;
    public float weight;
    // Type-specific data via inheritance or ScriptableObject reference
}

public enum ItemType { Weapon, Armor, Consumable, Material, Quest }

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity;
    public bool IsEmpty => item == null || quantity <= 0;
}

public class Inventory
{
    public InventorySlot[] slots;
    public event Action<int> OnSlotChanged;

    public bool AddItem(ItemData item, int qty = 1) { /* find slot, stack or fill empty */ }
    public bool RemoveItem(ItemData item, int qty = 1) { /* find and reduce */ }
    public void SwapSlots(int from, int to) { /* swap contents */ }
}
```

## UI Binding (UI Toolkit)
```csharp
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    private VisualElement[] _slotElements;

    void OnEnable()
    {
        var root = _document.rootVisualElement;
        var grid = root.Q<VisualElement>(""inventory-grid"");
        _slotElements = new VisualElement[_inventory.slots.Length];

        for (int i = 0; i < _inventory.slots.Length; i++)
        {
            var slot = new VisualElement();
            slot.AddToClassList(""inventory-slot"");
            var icon = new VisualElement();
            icon.AddToClassList(""slot-icon"");
            slot.Add(icon);
            grid.Add(slot);
            _slotElements[i] = slot;
        }

        _inventory.OnSlotChanged += RefreshSlot;
    }

    void RefreshSlot(int index)
    {
        var slot = _inventory.slots[index];
        var icon = _slotElements[index].Q(className: ""slot-icon"");
        icon.style.backgroundImage = slot.IsEmpty ? null
            : new StyleBackground(slot.item.icon);
    }
}
```

## Drag & Drop
- UI Toolkit: use `PointerDownEvent` → track drag → `PointerUpEvent` on target slot
- uGUI: implement `IBeginDragHandler`, `IDragHandler`, `IDropHandler`
- On drop: call `inventory.SwapSlots(fromIndex, toIndex)`
- Visual feedback: ghost icon follows cursor, highlight valid drop targets

## Persistence
```csharp
[Serializable]
public class InventorySaveData
{
    public List<SlotSaveData> slots = new();
}

[Serializable]
public class SlotSaveData
{
    public string itemId;  // Reference to ItemData asset
    public int quantity;
}

// Save: serialize InventorySaveData → JSON → file
// Load: deserialize → look up ItemData by itemId from a registry/database
```

## Tips
- Use ScriptableObject as item database (or Addressables for large catalogs)
- Separate data (Inventory) from presentation (InventoryUI)
- Use events (OnSlotChanged) for reactive UI updates
- Support item comparison tooltips (hover to see stat diff)
- Weight/encumbrance: sum all item.weight * quantity");
        }

        [McpPrompt("dialogue_system",
            "Dialogue system design: node graph structure, localization integration, conditional branching, Timeline integration")]
        public static ToolResult DialogueSystem()
        {
            return ToolResult.Text(@"# Dialogue System Design

## Data Structure
```csharp
[CreateAssetMenu(menuName = ""Dialogue/Conversation"")]
public class Conversation : ScriptableObject
{
    public string conversationId;
    public List<DialogueNode> nodes;
    public string startNodeId;
}

[Serializable]
public class DialogueNode
{
    public string nodeId;
    public string speakerName;
    public string textKey; // Localization key, not raw text
    public List<DialogueChoice> choices;
    public string nextNodeId; // For linear flow (no choices)
    public List<DialogueCondition> conditions; // Show/skip based on game state
    public List<DialogueEvent> events; // Trigger on node enter
}

[Serializable]
public class DialogueChoice
{
    public string textKey;
    public string targetNodeId;
    public List<DialogueCondition> conditions; // Show choice only if met
}
```

## Dialogue Runner
```csharp
public class DialogueRunner : MonoBehaviour
{
    public event Action<DialogueNode> OnNodeEntered;
    public event Action<List<DialogueChoice>> OnChoicesPresented;
    public event Action OnConversationEnded;

    private Conversation _current;
    private DialogueNode _currentNode;

    public void StartConversation(Conversation conversation)
    {
        _current = conversation;
        _currentNode = conversation.nodes.Find(n => n.nodeId == conversation.startNodeId);
        ProcessNode(_currentNode);
    }

    public void SelectChoice(int index)
    {
        var targetId = _currentNode.choices[index].targetNodeId;
        _currentNode = _current.nodes.Find(n => n.nodeId == targetId);
        ProcessNode(_currentNode);
    }

    public void Continue() // For nodes without choices
    {
        if (string.IsNullOrEmpty(_currentNode.nextNodeId))
        { OnConversationEnded?.Invoke(); return; }
        _currentNode = _current.nodes.Find(n => n.nodeId == _currentNode.nextNodeId);
        ProcessNode(_currentNode);
    }

    private void ProcessNode(DialogueNode node)
    {
        // Execute events (give item, set flag, play animation)
        foreach (var evt in node.events) evt.Execute();
        OnNodeEntered?.Invoke(node);
        if (node.choices.Count > 0) OnChoicesPresented?.Invoke(node.choices);
    }
}
```

## Localization Integration
- Store localization keys in nodes, not raw text
- Use Unity Localization: `LocalizedString(""Dialogue"", node.textKey)`
- Translators work with string tables, not node graphs
- Support Smart Strings for variable insertion: `{playerName}`

## Conditional Branching
```csharp
[Serializable]
public class DialogueCondition
{
    public string flagName;
    public ConditionType type; // HasFlag, FlagEquals, QuestComplete, ItemOwned
    public string value;

    public bool Evaluate(IGameState state) => type switch
    {
        ConditionType.HasFlag => state.HasFlag(flagName),
        ConditionType.QuestComplete => state.IsQuestComplete(flagName),
        _ => false
    };
}
```

## Tips
- Use a visual node editor (custom EditorWindow or asset: Yarn Spinner, Ink, Dialogue System)
- Keep dialogue data separate from game logic
- Support typewriter effect in UI (reveal text character by character)
- Use Timeline for cinematic dialogues (camera cuts, animations, audio sync)
- Playtest with all languages to catch text overflow issues");
        }

        [McpPrompt("version_control_unity",
            "Unity version control best practices: .meta files, merge strategy, LFS rules, scene merge tools")]
        public static ToolResult VersionControlUnity()
        {
            return ToolResult.Text(@"# Unity Version Control Best Practices

## .meta Files
- **Always commit .meta files** — they link assets to references
- Missing .meta = broken references (materials, prefabs, scripts lose connections)
- Editor Setting: Visible Meta Files (Edit > Project Settings > Editor > Version Control)
- Asset Serialization: Force Text (enables merge, readable diffs)

## Git LFS Configuration
Track binary files that don't diff well:
```
# .gitattributes
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.asset filter=lfs diff=lfs merge=lfs -text
*.cubemap filter=lfs diff=lfs merge=lfs -text
*.unitypackage filter=lfs diff=lfs merge=lfs -text
```

## Merge Strategy
- **Scenes and Prefabs**: avoid concurrent editing (use file locking or conventions)
- **Smart Merge Tool**: `UnityYAMLMerge` (ships with Unity)
  - Configure in `.gitconfig`:
    ```
    [merge]
    tool = unityyamlmerge
    [mergetool ""unityyamlmerge""]
    trustExitCode = false
    cmd = '/path/to/Unity/Editor/Data/Tools/UnityYAMLMerge' merge -p ""$BASE"" ""$REMOTE"" ""$LOCAL"" ""$MERGED""
    ```
- **Prefab Variants**: reduce merge conflicts (shared base, individual overrides)
- **ScriptableObjects**: one asset per config item (avoid monolithic config files)

## Branch Strategy
- `main`: stable, always builds
- `develop`: integration branch
- `feature/*`: short-lived feature branches
- Merge frequently to reduce conflict size
- Use Pull Requests for code review

## Common Pitfalls
- Don't commit Library/ folder (generated, large, platform-specific)
- Don't commit .csproj/.sln (auto-generated by Unity)
- Don't ignore .meta files (breaks all references)
- Don't use git submodules for UPM packages (use OpenUPM or git URL instead)
- Don't commit UserSettings/ (personal editor prefs)

## Tips
- Use `git lfs install` on every developer machine
- Set up branch protection rules (require PR, passing tests)
- Use `.gitkeep` for empty folders that need to exist
- Consider Unity Version Control (Plastic SCM) for large teams (free for Unity Pro)
- Lock binary assets when editing (Git LFS file locking or Plastic SCM locks)");
        }

        [McpPrompt("asset_bundle_guide",
            "AssetBundle guide: build pipeline, dependency management, incremental updates, comparison with Addressables")]
        public static ToolResult AssetBundleGuide()
        {
            return ToolResult.Text(@"# AssetBundle Guide

## AssetBundle vs Addressables
| Feature | AssetBundles | Addressables |
|---------|-------------|-------------|
| API complexity | Low-level, manual | High-level, managed |
| Dependency tracking | Manual | Automatic |
| Memory management | Manual load/unload | Reference counting |
| Remote loading | Manual URL management | Built-in remote catalog |
| Recommended for | Custom pipelines, legacy | New projects (preferred) |

## Build Pipeline
```csharp
[MenuItem(""Build/Build AssetBundles"")]
static void BuildBundles()
{
    string outputPath = ""AssetBundles/"" + EditorUserBuildSettings.activeBuildTarget;
    Directory.CreateDirectory(outputPath);

    BuildPipeline.BuildAssetBundles(outputPath,
        BuildAssetBundleOptions.ChunkBasedCompression, // LZ4 — fast load
        EditorUserBuildSettings.activeBuildTarget);
}
```

## Loading Assets
```csharp
// Load bundle from file
var bundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, ""mybundle""));
var prefab = bundle.LoadAsset<GameObject>(""Player"");

// Async loading
var request = AssetBundle.LoadFromFileAsync(path);
yield return request;
var asset = request.assetBundle.LoadAsset<GameObject>(""Player"");

// Remote loading
var webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url);
yield return webRequest.SendWebRequest();
var bundle = DownloadHandlerAssetBundle.GetContent(webRequest);
```

## Dependency Management
```csharp
// Load manifest to get dependencies
var manifestBundle = AssetBundle.LoadFromFile(""AssetBundles/AssetBundles"");
var manifest = manifestBundle.LoadAsset<AssetBundleManifest>(""AssetBundleManifest"");

// Load all dependencies first
string[] deps = manifest.GetAllDependencies(""mybundle"");
foreach (var dep in deps)
    AssetBundle.LoadFromFile(Path.Combine(basePath, dep));

// Then load the target bundle
var bundle = AssetBundle.LoadFromFile(Path.Combine(basePath, ""mybundle""));
```

## Incremental Updates
- Use `BuildAssetBundleOptions.DeterministicAssetBundle` for consistent hashing
- Compare bundle hashes: `manifest.GetAssetBundleHash(bundleName)`
- Only download changed bundles (hash comparison on server)
- Use CRC validation: `AssetBundle.LoadFromFile(path, crc)`

## Memory Management
```csharp
// Unload bundle but keep loaded assets in memory
bundle.Unload(false);

// Unload bundle AND all loaded assets (careful — breaks references!)
bundle.Unload(true);

// Best practice: track what's loaded, unload when scene/area changes
```

## Tips
- Use LZ4 compression (ChunkBasedCompression) for fast loading
- LZMA for download size (slower to load but smaller)
- Group assets by load lifecycle (level-based, feature-based)
- Avoid circular dependencies between bundles
- **Prefer Addressables for new projects** — it wraps AssetBundles with better API");
        }

        [McpPrompt("editor_automation",
            "Editor automation guide: MenuItem, BuildPipeline, AssetPostprocessor, scripted import")]
        public static ToolResult EditorAutomation()
        {
            return ToolResult.Text(@"# Editor Automation Guide

## MenuItem (Custom Menu Commands)
```csharp
public static class EditorMenus
{
    [MenuItem(""Tools/Clear PlayerPrefs"")]
    static void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log(""PlayerPrefs cleared"");
    }

    [MenuItem(""Tools/Create/Game Manager"")]
    static void CreateGameManager()
    {
        var go = new GameObject(""GameManager"");
        go.AddComponent<GameManager>();
        Undo.RegisterCreatedObjectUndo(go, ""Create GameManager"");
        Selection.activeGameObject = go;
    }

    // Validation (grayed out if no selection)
    [MenuItem(""Tools/Reset Transform"", true)]
    static bool ValidateResetTransform() => Selection.activeTransform != null;

    [MenuItem(""Tools/Reset Transform"")]
    static void ResetTransform()
    {
        foreach (var t in Selection.transforms)
        {
            Undo.RecordObject(t, ""Reset Transform"");
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
        }
    }
}
```

## AssetPostprocessor (Auto-configure imports)
```csharp
public class TexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;

        // Auto-configure textures in UI folder
        if (assetPath.Contains(""/UI/""))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
        }

        // Pixel art settings
        if (assetPath.Contains(""/PixelArt/""))
        {
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 16;
        }
    }

    void OnPreprocessModel()
    {
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.isReadable = false;
    }
}
```

## BuildPipeline (Automated Builds)
```csharp
public static class BuildAutomation
{
    [MenuItem(""Build/Build All Platforms"")]
    static void BuildAll()
    {
        BuildWindows();
        BuildMacOS();
        BuildWebGL();
    }

    static void BuildWindows()
    {
        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = ""Builds/Windows/Game.exe"",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
    }

    static string[] GetEnabledScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
}
```

## Scripted Import Pipeline
```csharp
// Batch process assets
[MenuItem(""Tools/Batch/Compress All Textures"")]
static void CompressTextures()
{
    var guids = AssetDatabase.FindAssets(""t:Texture2D"", new[] { ""Assets/_Project"" });
    AssetDatabase.StartAssetEditing(); // Batch mode — faster
    try
    {
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
    finally { AssetDatabase.StopAssetEditing(); }
}
```

## Tips
- Use `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` for batch operations
- `EditorUtility.DisplayProgressBar` for long operations (user feedback)
- Use `[InitializeOnLoadMethod]` for startup automation
- `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` to reimport
- Automate with `-executeMethod` for CI/CD integration");
        }
    }
}
