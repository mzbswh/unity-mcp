using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("SystemDesignPrompts")]
    public static class SystemDesignPrompts
    {
        [McpPrompt("scene_organization",
            "Scene organization best practices: hierarchy naming, empty GameObject grouping, scene loading strategies")]
        public static ToolResult SceneOrganization()
        {
            return ToolResult.Text(@"# Scene Organization Best Practices

## Hierarchy Naming Conventions
- Use PascalCase for root objects: `Environment`, `Characters`, `UI`, `Systems`
- Prefix dynamic objects: `[Dynamic] SpawnedEnemy`
- Use `---` separators as empty GameObjects for visual grouping in large scenes

## Recommended Hierarchy Structure
```
Scene Root
├── --- ENVIRONMENT ---
│   ├── Terrain
│   ├── Props
│   │   ├── Static
│   │   └── Dynamic
│   └── Lighting
├── --- GAMEPLAY ---
│   ├── Player
│   ├── Enemies
│   ├── Pickups
│   └── SpawnPoints
├── --- UI ---
│   ├── Canvas_HUD
│   ├── Canvas_Menu
│   └── EventSystem
├── --- SYSTEMS ---
│   ├── GameManager
│   ├── AudioManager
│   └── PoolManager
└── --- CAMERAS ---
    └── Main Camera
```

## Scene Loading Strategy
- **Single Scene**: Small games, prototypes — everything in one scene
- **Additive Loading**: Medium games — base scene + additively loaded levels
- **Scene Streaming**: Large open worlds — load/unload scenes by trigger zones
- Use `SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)` for seamless transitions
- Keep a persistent ""Boot"" scene for managers that survive scene loads (`DontDestroyOnLoad`)

## Scene Organization Rules
- Never put game logic on root-level objects — use child GameObjects
- Group static objects under a single parent and mark as Static for batching
- Keep the hierarchy depth under 5 levels for performance
- Use Prefabs for reusable structures — don't duplicate objects manually
- Set sorting layers for 2D/UI rendering order");
        }

        [McpPrompt("asset_naming",
            "Asset naming conventions: folder structure, prefix/suffix rules, case conventions")]
        public static ToolResult AssetNaming()
        {
            return ToolResult.Text(@"# Asset Naming Conventions

## Folder Structure
```
Assets/
├── _Project/              # Project-specific assets (underscore keeps it at top)
│   ├── Animations/
│   ├── Audio/
│   │   ├── Music/
│   │   └── SFX/
│   ├── Materials/
│   ├── Models/
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   └── UI/
│   ├── Scenes/
│   ├── Scripts/
│   │   ├── Runtime/
│   │   └── Editor/
│   ├── Shaders/
│   ├── Textures/
│   └── UI/
├── Plugins/               # Third-party plugins
├── Resources/             # Only for assets loaded at runtime via Resources.Load
└── StreamingAssets/       # Raw files copied to build
```

## Naming Rules
- **PascalCase** for all asset names: `PlayerController.cs`, `ForestGround_Albedo.png`
- **No spaces**: use underscores or PascalCase: `DarkForest_01` not `Dark Forest 01`
- **Descriptive names**: `EnemyGoblin_Walk.anim` not `anim_01.anim`

## Prefix/Suffix Conventions
| Type | Convention | Example |
|------|-----------|---------|
| Texture - Albedo | `_Albedo` / `_BaseColor` | `Wood_Albedo.png` |
| Texture - Normal | `_Normal` | `Wood_Normal.png` |
| Texture - Metallic | `_Metallic` | `Wood_Metallic.png` |
| Material | `M_` or `MAT_` prefix | `M_WoodFloor` |
| Prefab | No prefix needed | `EnemyGoblin.prefab` |
| ScriptableObject | `SO_` or descriptive | `SO_WeaponData_Sword` |
| Animation Clip | `CharName_Action` | `Player_Idle.anim` |
| Animator Controller | `AC_CharName` | `AC_Player` |

## Anti-Patterns
- Don't use `Resources/` folder for everything — use Addressables or direct references
- Don't nest folders more than 4 levels deep
- Don't mix asset types in the same folder
- Don't use generic names like `New Material`, `Script1`, `Untitled`");
        }

        [McpPrompt("performance_optimization",
            "Performance optimization guide: object pooling, LOD, draw call batching, GC optimization")]
        public static ToolResult PerformanceOptimization()
        {
            return ToolResult.Text(@"# Unity Performance Optimization Guide

## CPU Optimization
- **Cache references** in Awake/Start — never use Find/GetComponent in Update
- **Avoid LINQ in hot paths** — causes GC allocations every frame
- **Use NonAlloc physics queries**: `Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc`
- **Object Pooling**: reuse frequently spawned objects (bullets, VFX, enemies)
- **Use Jobs + Burst** for heavy computations (pathfinding, spatial queries)
- **Reduce Update calls**: use InvokeRepeating, timers, or event-driven patterns

## GPU / Draw Call Optimization
- **Static Batching**: mark non-moving objects as Static in Inspector
- **Dynamic Batching**: works for small meshes (<300 vertices) automatically
- **SRP Batcher** (URP/HDRP): use compatible shaders (all URP/HDRP Lit shaders)
- **GPU Instancing**: enable on materials for repeated objects (trees, grass)
- **LOD Groups**: 3-4 LOD levels (100% → 50% → 25% → billboard/cull)
- **Occlusion Culling**: bake occlusion data for indoor/urban scenes
- **Texture Atlasing**: combine small textures to reduce material/draw call count

## Memory / GC Optimization
- **Avoid allocations in Update**: no `new`, no string concat, no LINQ, no closures
- **Cache WaitForSeconds**: `static readonly WaitForSeconds _wait = new(1f)`
- **Use StringBuilder** for string building in loops
- **Pool collections**: reuse List<T> with `.Clear()` instead of `new List<T>()`
- **Use structs** for small, short-lived data (but beware of boxing)
- **Profile with GC.Alloc** column in Profiler to find allocation sources

## Physics Optimization
- **Simplify colliders**: use primitives (Box, Sphere, Capsule) over MeshCollider
- **Layer-based collision matrix**: disable unnecessary layer interactions
- **Fixed Timestep**: increase from 0.02 (50Hz) if physics doesn't need high precision
- **Reduce Rigidbody count**: merge static colliders, use compound colliders

## Asset Optimization
- **Texture compression**: use platform-appropriate formats (ASTC for mobile, BC7 for PC)
- **Mipmap settings**: enable for 3D textures, disable for UI/2D
- **Audio compression**: Vorbis for music, ADPCM for short SFX
- **Mesh optimization**: use Mesh Compression, strip unused vertex data");
        }

        [McpPrompt("physics_setup",
            "Physics configuration guide: collision matrix, Rigidbody settings, Raycast best practices")]
        public static ToolResult PhysicsSetup()
        {
            return ToolResult.Text(@"# Unity Physics Configuration Guide

## Collision Matrix (Edit > Project Settings > Physics)
- Define layers: Player, Enemy, Bullet, Environment, Trigger, Pickup
- Disable unnecessary collisions (e.g., Bullet vs Bullet, Enemy vs Enemy)
- Reduces physics computation significantly in complex scenes

## Rigidbody Configuration
| Setting | Recommendation |
|---------|---------------|
| Interpolation | Interpolate for player-visible objects, None for others |
| Collision Detection | Discrete (default), Continuous for fast-moving objects |
| Drag | 0 for space/projectiles, 1-5 for grounded movement |
| Constraints | Freeze rotation axes to prevent unwanted tilting |
| Is Kinematic | true for objects moved via Transform (platforms, doors) |

## Rigidbody Best Practices
- **Move in FixedUpdate**: `rb.MovePosition()`, `rb.AddForce()`, `rb.velocity =`
- **Never mix Transform and Rigidbody movement** on the same object
- **Use Rigidbody.MovePosition** for kinematic objects (respects interpolation)
- **Set mass realistically**: 1 = 1kg, affects collision responses
- **Sleep threshold**: increase for objects that should settle quickly

## Raycast Best Practices
```csharp
// Use layermask to limit what's checked
int groundLayer = LayerMask.GetMask(""Ground"", ""Platform"");
if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, groundLayer))
{
    // hit.point, hit.normal, hit.collider
}

// NonAlloc for repeated queries (no GC)
private readonly RaycastHit[] _hits = new RaycastHit[10];
int count = Physics.RaycastNonAlloc(ray, _hits, maxDist, layerMask);

// SphereCast for wider detection (pickups, interaction)
Physics.SphereCast(origin, radius, direction, out hit, maxDist, layerMask);
```

## Trigger vs Collider
- **Collider (Is Trigger = false)**: physical collision, OnCollisionEnter/Stay/Exit
- **Trigger (Is Trigger = true)**: overlap detection, OnTriggerEnter/Stay/Exit
- Triggers need at least one Rigidbody on either object
- Use triggers for: damage zones, pickups, area detection, checkpoints

## 2D Physics
- Use Physics2D equivalents: Rigidbody2D, Collider2D, Physics2D.Raycast
- 2D and 3D physics are completely separate systems — don't mix them
- Use `CompositeCollider2D` with Tilemaps for efficient tile collision");
        }

        [McpPrompt("input_system",
            "New Input System guide: Action Map design, PlayerInput component, callback modes")]
        public static ToolResult InputSystem()
        {
            return ToolResult.Text(@"# Unity New Input System Guide

## Setup
1. Install via Package Manager: `com.unity.inputsystem`
2. Set Active Input Handling to ""Input System Package (New)"" in Player Settings
3. Create Input Actions asset: Create > Input Actions

## Action Map Design
```
InputActions.inputactions
├── Player                    # Gameplay action map
│   ├── Move (Value, Vector2)       # WASD / Left Stick
│   ├── Look (Value, Vector2)       # Mouse Delta / Right Stick
│   ├── Jump (Button)               # Space / South Button
│   ├── Attack (Button)             # LMB / West Button
│   └── Interact (Button)           # E / North Button
├── UI                        # Menu navigation
│   ├── Navigate (Value, Vector2)
│   ├── Submit (Button)
│   ├── Cancel (Button)
│   └── Point (Value, Vector2)
└── Debug                     # Development only
    ├── ToggleConsole (Button)
    └── SpeedMultiplier (Button)
```

## PlayerInput Component (Recommended for most cases)
- Add `PlayerInput` component to player GameObject
- Assign Input Actions asset
- Set Default Map to ""Player""
- Behavior modes:
  - **Send Messages**: calls `OnMove(InputValue)`, `OnJump(InputValue)` etc.
  - **Invoke Unity Events**: wire up in Inspector (most flexible)
  - **Invoke C# Events**: subscribe in code (best performance)

## C# Events Pattern (Best for complex projects)
```csharp
public class PlayerInput : MonoBehaviour
{
    private InputActions _input;

    void Awake()
    {
        _input = new InputActions();
    }

    void OnEnable()
    {
        _input.Player.Enable();
        _input.Player.Jump.performed += OnJump;
        _input.Player.Move.performed += OnMove;
        _input.Player.Move.canceled += OnMoveCanceled;
    }

    void OnDisable()
    {
        _input.Player.Jump.performed -= OnJump;
        _input.Player.Move.performed -= OnMove;
        _input.Player.Move.canceled -= OnMoveCanceled;
        _input.Player.Disable();
    }

    private void OnJump(InputAction.CallbackContext ctx) { /* jump logic */ }
    private void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();
    }
    private void OnMoveCanceled(InputAction.CallbackContext ctx) { /* stop */ }
}
```

## Action Map Switching
```csharp
// Switch to UI controls (e.g., when opening menu)
_input.Player.Disable();
_input.UI.Enable();
```

## Tips
- Use Interactions (Hold, Tap, MultiTap) for complex input gestures
- Use Processors (Normalize, Deadzone, Invert) to clean up input
- Generate C# class from Input Actions asset for type-safe access
- Use `InputSystem.onDeviceChange` to detect controller connect/disconnect");
        }

        [McpPrompt("audio_architecture",
            "Audio architecture guide: AudioMixer hierarchy, pooled AudioSources, spatial audio")]
        public static ToolResult AudioArchitecture()
        {
            return ToolResult.Text(@"# Unity Audio Architecture Guide

## AudioMixer Hierarchy
```
MasterMixer
├── Music (Group)
│   ├── MusicDucked        # Auto-duck during dialogue
│   └── Ambient
├── SFX (Group)
│   ├── Weapons
│   ├── Footsteps
│   ├── UI
│   └── Environment
├── Voice (Group)
│   └── Dialogue
└── Master (exposed parameter: ""MasterVolume"")
```
- Expose volume parameters for settings UI: `mixer.SetFloat(""MasterVolume"", dB)`
- Convert linear (0-1) to dB: `Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f`
- Use Snapshots for state transitions (combat, stealth, underwater)

## AudioSource Pooling
```csharp
public class AudioPool : MonoBehaviour
{
    [SerializeField] private int _poolSize = 20;
    private Queue<AudioSource> _pool;

    void Awake()
    {
        _pool = new Queue<AudioSource>();
        for (int i = 0; i < _poolSize; i++)
        {
            var go = new GameObject($""AudioSource_{i}"");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            _pool.Enqueue(src);
        }
    }

    public AudioSource Play(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (_pool.Count == 0) return null;
        var src = _pool.Dequeue();
        src.transform.position = position;
        src.clip = clip;
        src.volume = volume;
        src.Play();
        StartCoroutine(ReturnAfterPlay(src, clip.length));
        return src;
    }
}
```

## Spatial Audio Setup
- **3D Sound**: Set AudioSource Spatial Blend to 1.0 (3D)
- **Rolloff**: Logarithmic for realism, Linear for precise control, Custom for specific needs
- **Min/Max Distance**: Min = full volume radius, Max = inaudible distance
- **Spread**: 0° = point source, 360° = surround — use 30-90° for most effects
- **Doppler Level**: 0 for most games, increase for racing/flight games

## Best Practices
- Use AudioMixer groups for all sounds — never play without routing
- Limit simultaneous sounds: prioritize by importance, distance, and age
- Use one-shot for short SFX: `AudioSource.PlayClipAtPoint(clip, pos)` or pool
- Compress audio: Vorbis (music, long clips), ADPCM (short SFX, low latency)
- Load Type: Decompress on Load (small SFX), Streaming (music), Compressed in Memory (medium)
- Preload Audio Data: enabled for frequently used clips
- Use `AudioListener.pause` to handle game pause (pauses all audio)");
        }

        [McpPrompt("ai_navigation",
            "AI navigation guide: NavMesh baking, Agent configuration, dynamic obstacles, path queries")]
        public static ToolResult AiNavigation()
        {
            return ToolResult.Text(@"# Unity AI Navigation Guide

## NavMesh Baking (Window > AI > Navigation)
- **Agent Radius**: match character collision radius (0.5 for humanoids)
- **Agent Height**: character height (2.0 for humanoids)
- **Max Slope**: maximum walkable angle (45° default, lower for heavy characters)
- **Step Height**: maximum step-up height (0.4 default)
- **Bake only Static objects** or objects with NavMeshModifier component
- Use **NavMeshSurface** component (AI Navigation package) for runtime baking

## NavMeshAgent Configuration
| Setting | Typical Values | Notes |
|---------|---------------|-------|
| Speed | 3.5 (walk), 6 (run) | Units per second |
| Angular Speed | 120-360 | Degrees per second for turning |
| Acceleration | 8-20 | How quickly agent reaches speed |
| Stopping Distance | 0.5-2.0 | Distance to target to stop |
| Auto Braking | true | Slow down when approaching destination |
| Obstacle Avoidance | High Quality | Lower for background NPCs |
| Priority | 0-99 | Lower = higher priority for avoidance |

## Basic Agent Setup
```csharp
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private float _chaseRange = 15f;
    private Transform _target;

    void Update()
    {
        if (_target == null) return;
        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist <= _chaseRange)
            _agent.SetDestination(_target.position);
        else
            _agent.ResetPath();
    }

    // Check if agent reached destination
    bool HasReached => !_agent.pathPending
        && _agent.remainingDistance <= _agent.stoppingDistance;
}
```

## Dynamic Obstacles
- **NavMeshObstacle**: blocks navigation at runtime (barricades, doors)
  - Carve = true: cuts holes in NavMesh (use for stationary obstacles)
  - Carve = false: agents avoid via local avoidance only (use for moving obstacles)
- **NavMeshModifier**: include/exclude objects from baking, override area types
- **NavMesh Areas**: define cost for different surfaces (road=1, mud=3, water=5)

## Path Queries
```csharp
// Check if path exists before moving
NavMeshPath path = new NavMeshPath();
if (NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path))
{
    if (path.status == NavMeshPathStatus.PathComplete)
        agent.SetPath(path);
}

// Sample nearest point on NavMesh
if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, maxDist, NavMesh.AllAreas))
    Vector3 navPos = hit.position;

// Raycast on NavMesh (line-of-sight check on walkable surface)
NavMesh.Raycast(start, end, out NavMeshHit hit, NavMesh.AllAreas);
```

## Tips
- Use **Off Mesh Links** for jumps, ladders, teleports between disconnected NavMesh
- Use **NavMesh.avoidancePredictionTime** to tune how far ahead agents predict
- Disable agent when doing custom movement (ragdoll, cutscene): `agent.enabled = false`
- For large worlds: use NavMeshSurface component per chunk, bake at runtime");
        }

        [McpPrompt("networking_patterns",
            "Multiplayer networking patterns: state sync vs lockstep, network object lifecycle, lag compensation")]
        public static ToolResult NetworkingPatterns()
        {
            return ToolResult.Text(@"# Multiplayer Networking Patterns

## Architecture Comparison
| Pattern | State Synchronization | Lockstep / Deterministic |
|---------|----------------------|--------------------------|
| Model | Server authoritative, replicate state | All clients simulate same inputs |
| Best for | Action, RPG, MMO | RTS, fighting, turn-based |
| Bandwidth | Higher (state updates) | Lower (only inputs) |
| Latency handling | Client prediction + reconciliation | Input delay + rollback |
| Unity support | Netcode for GameObjects | Custom or third-party |

## Netcode for GameObjects (NGO) — Recommended for Unity
- **NetworkObject**: root component for any networked entity
- **NetworkBehaviour**: base class (replaces MonoBehaviour for networked logic)
- **NetworkVariable<T>**: auto-synced state (supports primitives, structs)
- **ServerRpc / ClientRpc**: remote procedure calls

## Network Object Lifecycle
```
Spawn Flow:
  Server: Instantiate → NetworkObject.Spawn() → replicates to all clients
  Client: OnNetworkSpawn() callback → safe to access NetworkVariables

Despawn Flow:
  Server: NetworkObject.Despawn() → clients receive despawn
  Client: OnNetworkDespawn() callback → cleanup

Owner Transfer:
  Server: NetworkObject.ChangeOwnership(clientId)
```

## State Synchronization Pattern
```csharp
public class PlayerState : NetworkBehaviour
{
    // Auto-synced to all clients
    private NetworkVariable<int> _health = new(100, writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> _position = new();

    // Client sends input to server
    [ServerRpc]
    void MoveServerRpc(Vector3 direction)
    {
        // Server validates and applies
        transform.position += direction * _speed * Time.deltaTime;
        _position.Value = transform.position;
    }

    // Server tells all clients to play effect
    [ClientRpc]
    void PlayHitEffectClientRpc() { /* VFX */ }
}
```

## Lag Compensation Strategies
1. **Client-Side Prediction**: client applies input immediately, server reconciles
2. **Server Reconciliation**: server sends authoritative state, client corrects
3. **Entity Interpolation**: render other players slightly in the past (100-200ms)
4. **Lag Compensation**: server rewinds time for hit detection (shooting games)

## Best Practices
- **Server authoritative**: never trust client data — always validate on server
- **Minimize RPCs**: batch state updates, use NetworkVariables for continuous data
- **NetworkTransform**: use for position/rotation sync with built-in interpolation
- **Object pooling**: use NetworkObject pooling to avoid spawn/despawn overhead
- **Relevancy**: only sync objects near the player (use NetworkManager.NetworkConfig)
- **Testing**: use ParrelSync or MPPM for multi-editor testing locally");
        }
    }
}
