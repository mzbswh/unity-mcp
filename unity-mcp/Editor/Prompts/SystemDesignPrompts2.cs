using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("SystemDesignPrompts2")]
    public static class SystemDesignPrompts2
    {
        [McpPrompt("save_system",
            "Save system design: serialization strategies, encryption, version migration, cloud storage interface")]
        public static ToolResult SaveSystem()
        {
            return ToolResult.Text(@"# Save System Design Guide

## Architecture Overview
```
SaveManager (singleton/service)
├── ISaveSerializer        # JSON, Binary, or custom
├── ISaveEncryptor         # Optional: AES encryption
├── ISaveStorage           # Local file, PlayerPrefs, or Cloud
└── SaveVersionMigrator    # Handle save format changes
```

## Serialization Strategies
| Strategy | Pros | Cons | Best For |
|----------|------|------|----------|
| JsonUtility | Fast, built-in | No Dictionary, no polymorphism | Simple data |
| Newtonsoft.Json | Full-featured, flexible | Slightly slower, needs package | Complex data |
| BinaryFormatter | Compact | Security risk, deprecated | Avoid |
| Custom Binary | Fastest, smallest | Manual work | Performance-critical |

## Recommended: JSON with Newtonsoft
```csharp
[Serializable]
public class SaveData
{
    public int version = 1;
    public string timestamp;
    public PlayerSaveData player;
    public List<InventoryItem> inventory;
    public Dictionary<string, bool> flags;
}

public static class SaveManager
{
    private static readonly string SavePath =
        Path.Combine(Application.persistentDataPath, ""save.json"");

    public static void Save(SaveData data)
    {
        data.timestamp = DateTime.UtcNow.ToString(""o"");
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(SavePath, json);
    }

    public static SaveData Load()
    {
        if (!File.Exists(SavePath)) return new SaveData();
        string json = File.ReadAllText(SavePath);
        return JsonConvert.DeserializeObject<SaveData>(json);
    }
}
```

## ISaveable Pattern
```csharp
public interface ISaveable
{
    string SaveId { get; }
    object CaptureState();
    void RestoreState(object state);
}
// MonoBehaviours implement ISaveable, SaveManager collects all via FindObjectsByType
```

## Version Migration
```csharp
if (data.version < 2)
{
    // Migrate v1 → v2: rename field, add defaults
    data.player.maxHealth = data.player.maxHealth > 0 ? data.player.maxHealth : 100;
    data.version = 2;
}
```

## Encryption (Optional)
- Use AES-256 for local saves: `Aes.Create()` with derived key from device ID
- Store key securely — don't hardcode in source
- Only encrypt final output, keep internal format as JSON for debugging

## Best Practices
- Save to `Application.persistentDataPath` — survives app updates
- Write to temp file first, then rename (atomic write prevents corruption)
- Auto-save periodically + on key events (level complete, quit)
- Keep save files human-readable during development (JSON), encrypt for release
- Test save/load with every data structure change");
        }

        [McpPrompt("localization",
            "Localization design guide: Localization Package usage, string table management, font handling")]
        public static ToolResult Localization()
        {
            return ToolResult.Text(@"# Unity Localization Guide

## Setup (Unity Localization Package)
1. Install: `com.unity.localization` via Package Manager
2. Window > Asset Management > Localization Tables
3. Create Locale assets for each language (English, Japanese, etc.)
4. Create String Tables and Asset Tables

## String Table Structure
```
StringTable: ""UI""
├── KEY_PLAY          → ""Play"" / ""プレイ""
├── KEY_SETTINGS      → ""Settings"" / ""設定""
├── KEY_HEALTH_FMT    → ""Health: {0}/{1}"" / ""体力: {0}/{1}""
└── KEY_WELCOME       → ""Welcome, {name}!"" / ""{name}さん、ようこそ！""
```

## Usage in Code
```csharp
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

// Simple string lookup
var localizedString = new LocalizedString(""UI"", ""KEY_PLAY"");
localizedString.StringChanged += (value) => textComponent.text = value;

// With arguments (Smart Strings)
var formatted = new LocalizedString(""UI"", ""KEY_HEALTH_FMT"");
formatted.Arguments = new object[] { currentHealth, maxHealth };

// Switch locale at runtime
LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(""ja"");
```

## UI Integration
- Add `LocalizeStringEvent` component to UI Text objects
- Drag String Table Entry reference in Inspector
- Automatic updates when locale changes

## Font Handling
- Use **TextMeshPro** with SDF fonts for quality at any size
- Create font asset with full character sets for CJK languages
- Use **Font Fallback** list for characters missing from primary font
- Dynamic font assets: `TMP_FontAsset` with ""Multi Atlas Textures"" for large character sets
- Test with longest translations to ensure UI layouts don't break

## Asset Localization (images, audio)
- Create Asset Tables for locale-specific assets
- Use `LocalizedAsset<T>` (LocalizedSprite, LocalizedAudioClip, etc.)
- Different splash screens, voice-overs, or culturally appropriate images per locale

## Best Practices
- Never hardcode user-facing strings — always use localization tables
- Use Smart Strings for dynamic content: `{playerName} scored {score} points`
- Plan for text expansion: German/French can be 30% longer than English
- Use pseudo-localization for testing (detect hardcoded/untranslated strings)
- Export tables as CSV/XLIFF for translators
- Support RTL (Right-to-Left) for Arabic/Hebrew if targeting those markets");
        }

        [McpPrompt("dependency_injection",
            "Dependency injection patterns: Service Locator, Zenject/VContainer integration suggestions")]
        public static ToolResult DependencyInjection()
        {
            return ToolResult.Text(@"# Dependency Injection in Unity

## Why DI in Unity?
- Decouple systems (UI doesn't need to know about inventory implementation)
- Enable unit testing with mocks
- Replace singletons with injectable services
- Manage object lifetimes cleanly

## Pattern 1: Service Locator (Simplest)
```csharp
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) => _services[typeof(T)] = service;
    public static T Get<T>() => (T)_services[typeof(T)];
    public static bool TryGet<T>(out T service)
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        { service = (T)obj; return true; }
        service = default; return false;
    }
    public static void Clear() => _services.Clear();
}

// Registration (in boot/init scene)
ServiceLocator.Register<IAudioService>(new AudioService());
ServiceLocator.Register<ISaveService>(new SaveService());

// Usage
var audio = ServiceLocator.Get<IAudioService>();
```

## Pattern 2: VContainer (Recommended for Unity)
```csharp
// Install: com.unity.vcontainer via UPM or OpenUPM
// Lifetime Scope (replaces Zenject Installer)
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Singletons
        builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
        builder.Register<ISaveService, SaveService>(Lifetime.Singleton);

        // Per-scene
        builder.Register<IEnemySpawner, EnemySpawner>(Lifetime.Scoped);

        // MonoBehaviour injection
        builder.RegisterComponentInHierarchy<PlayerController>();
    }
}

// Auto-injected via [Inject] attribute
public class PlayerUI : MonoBehaviour
{
    [Inject] private readonly IAudioService _audio;
    [Inject] private readonly ISaveService _save;
}
```

## Pattern 3: Zenject / Extenject
```csharp
public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<IAudioService>().To<AudioService>().AsSingle();
        Container.Bind<ISaveService>().To<SaveService>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameManager>().AsSingle();
    }
}
```

## VContainer vs Zenject Comparison
| Feature | VContainer | Zenject/Extenject |
|---------|-----------|-------------------|
| Performance | Faster (source-gen) | Slower (reflection) |
| API complexity | Simpler | More features |
| Unity integration | Native LifetimeScope | MonoInstaller + SceneContext |
| Maintenance | Actively maintained | Community maintained |
| Learning curve | Lower | Higher |

## Best Practices
- **Program to interfaces**: `IAudioService`, not `AudioService`
- **Constructor injection** for plain C# classes, `[Inject]` for MonoBehaviours
- **Composition Root**: one place where everything is wired up (LifetimeScope)
- **Don't over-inject**: simple helper classes don't need DI
- **Scoped lifetimes**: per-scene services destroyed with scene
- **Avoid circular dependencies**: use events or mediator pattern instead");
        }

        [McpPrompt("event_architecture",
            "Event system architecture: C# event, UnityEvent, ScriptableObject events, message bus comparison")]
        public static ToolResult EventArchitecture()
        {
            return ToolResult.Text(@"# Event System Architecture

## Comparison of Event Approaches

| Approach | GC Alloc | Inspector | Decoupling | Performance |
|----------|---------|-----------|------------|-------------|
| C# event/Action | None | No | Good | Best |
| UnityEvent | Per invoke | Yes | Medium | Good |
| SO Event Channel | None | Yes | Best | Good |
| Message Bus | Per message | No | Best | Medium |

## 1. C# Events (Best for code-to-code)
```csharp
public class Health : MonoBehaviour
{
    public event Action<float, float> OnHealthChanged; // current, max
    public event Action OnDied;

    public void TakeDamage(float amount)
    {
        _current -= amount;
        OnHealthChanged?.Invoke(_current, _max);
        if (_current <= 0) OnDied?.Invoke();
    }
}

// Subscriber
health.OnHealthChanged += UpdateHealthBar;
health.OnDied += PlayDeathAnimation;
// ALWAYS unsubscribe in OnDisable/OnDestroy
```

## 2. UnityEvent (Best for designer-configured wiring)
```csharp
public class Button : MonoBehaviour
{
    [SerializeField] private UnityEvent _onClick;
    [SerializeField] private UnityEvent<string> _onValueChanged;

    public void Click() => _onClick?.Invoke();
}
// Wire up in Inspector — designers can add multiple listeners without code
```

## 3. ScriptableObject Event Channel (Best for cross-system)
```csharp
[CreateAssetMenu(menuName = ""Events/Void Event"")]
public class VoidEventChannel : ScriptableObject
{
    private readonly List<Action> _listeners = new();
    public void Raise() { for (int i = _listeners.Count - 1; i >= 0; i--) _listeners[i](); }
    public void Register(Action cb) => _listeners.Add(cb);
    public void Unregister(Action cb) => _listeners.Remove(cb);
}

[CreateAssetMenu(menuName = ""Events/Int Event"")]
public class IntEventChannel : ScriptableObject
{
    private readonly List<Action<int>> _listeners = new();
    public void Raise(int value) { for (int i = _listeners.Count - 1; i >= 0; i--) _listeners[i](value); }
    public void Register(Action<int> cb) => _listeners.Add(cb);
    public void Unregister(Action<int> cb) => _listeners.Remove(cb);
}

// Usage: drag same SO asset to both sender and receiver in Inspector
```

## 4. Simple Message Bus
```csharp
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type)) _handlers[type] = new();
        _handlers[type].Add(handler);
    }

    public static void Publish<T>(T message)
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
            foreach (var h in list) ((Action<T>)h)(message);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
            list.Remove(handler);
    }
}

// Usage: EventBus.Publish(new PlayerDiedEvent { PlayerId = 1 });
```

## Guidelines
- **C# events**: within a system (Health → HealthUI on same object)
- **UnityEvents**: designer-facing hooks (buttons, triggers, animations)
- **SO Events**: cross-system (GameOver event → UI, Audio, Analytics)
- **Message Bus**: global events with many subscribers (achievement tracking)
- **Always unsubscribe** to prevent memory leaks and null reference errors");
        }

        [McpPrompt("object_pooling",
            "Object pool design guide: generic pool, warm-up strategy, auto-recycle, Addressables integration")]
        public static ToolResult ObjectPooling()
        {
            return ToolResult.Text(@"# Object Pool Design Guide

## When to Pool
- Frequently instantiated/destroyed objects: bullets, VFX, enemies, coins
- Rule of thumb: if you create/destroy >10 instances per second, pool it
- Signs you need pooling: GC spikes in Profiler, frame hitches during spawns

## Unity Built-in Pool (2021+)
```csharp
using UnityEngine.Pool;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] private Bullet _prefab;

    private ObjectPool<Bullet> _pool;

    void Awake()
    {
        _pool = new ObjectPool<Bullet>(
            createFunc: () => Instantiate(_prefab),
            actionOnGet: b => { b.gameObject.SetActive(true); b.Init(_pool); },
            actionOnRelease: b => b.gameObject.SetActive(false),
            actionOnDestroy: b => Destroy(b.gameObject),
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public Bullet Spawn(Vector3 pos, Vector3 dir)
    {
        var bullet = _pool.Get();
        bullet.transform.position = pos;
        bullet.Fire(dir);
        return bullet;
    }
}

// Bullet returns itself to pool
public class Bullet : MonoBehaviour
{
    private IObjectPool<Bullet> _pool;
    public void Init(IObjectPool<Bullet> pool) => _pool = pool;
    public void OnHitOrExpire() => _pool.Release(this);
}
```

## Warm-Up Strategy
```csharp
void Start()
{
    // Pre-instantiate objects to avoid first-frame hitch
    var warmUp = new List<Bullet>();
    for (int i = 0; i < 20; i++)
        warmUp.Add(_pool.Get());
    foreach (var b in warmUp)
        _pool.Release(b);
}
```

## Auto-Recycle (time-based return)
```csharp
public class PooledVFX : MonoBehaviour
{
    [SerializeField] private float _lifetime = 2f;
    private IObjectPool<PooledVFX> _pool;
    private float _timer;

    void OnEnable() => _timer = _lifetime;
    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) _pool.Release(this);
    }
}
```

## Generic Pool Manager
```csharp
public class PoolManager : MonoBehaviour
{
    private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new();

    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        int id = prefab.GetInstanceID();
        if (!_pools.ContainsKey(id))
        {
            var p = prefab; // capture for closure
            _pools[id] = new ObjectPool<GameObject>(
                () => Instantiate(p, transform),
                go => go.SetActive(true),
                go => go.SetActive(false),
                go => Destroy(go),
                defaultCapacity: 10, maxSize: 50);
        }
        var obj = _pools[id].Get();
        obj.transform.SetPositionAndRotation(pos, rot);
        return obj;
    }

    public void Release(GameObject prefab, GameObject instance)
    {
        _pools[prefab.GetInstanceID()].Release(instance);
    }
}
```

## Addressables Integration
- Use `Addressables.InstantiateAsync` with custom pool: implement `IObjectPool`
- Release with `Addressables.ReleaseInstance` or pool.Release
- Track reference counts to properly unload bundles

## Best Practices
- Reset object state in `actionOnGet` (health, position, velocity)
- Set parent to pool manager to keep hierarchy clean
- Use `maxSize` to prevent unbounded memory growth
- Disable pooled objects (`SetActive(false)`) rather than moving offscreen
- Profile before pooling — don't pool everything, only hot-path objects");
        }

        [McpPrompt("state_machine",
            "State machine design: FSM/HFSM patterns, Animator state machine, custom state machine framework")]
        public static ToolResult StateMachine()
        {
            return ToolResult.Text(@"# State Machine Design Guide

## Pattern 1: Simple Enum FSM (Best for < 5 states)
```csharp
public class EnemyAI : MonoBehaviour
{
    private enum State { Idle, Patrol, Chase, Attack, Dead }
    private State _state = State.Idle;

    void Update()
    {
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
        }
    }

    private void TransitionTo(State newState)
    {
        ExitState(_state);
        _state = newState;
        EnterState(newState);
    }
}
```

## Pattern 2: Class-Based FSM (Best for complex states)
```csharp
public interface IState
{
    void Enter();
    void Update();
    void Exit();
}

public class StateMachine
{
    private IState _current;
    private readonly Dictionary<Type, IState> _states = new();

    public void AddState(IState state) => _states[state.GetType()] = state;

    public void TransitionTo<T>() where T : IState
    {
        _current?.Exit();
        _current = _states[typeof(T)];
        _current.Enter();
    }

    public void Update() => _current?.Update();
}

// State implementation
public class ChaseState : IState
{
    private readonly EnemyAI _owner;
    public ChaseState(EnemyAI owner) => _owner = owner;
    public void Enter() { /* start chase animation */ }
    public void Update() { /* move toward target */ }
    public void Exit() { /* stop chase animation */ }
}
```

## Pattern 3: Hierarchical FSM (HFSM)
```
CombatState (parent)
├── MeleeState
│   ├── ApproachState
│   └── SwingState
└── RangedState
    ├── AimState
    └── FireState
```
- Parent states handle shared behavior (facing target)
- Child states handle specific behavior (swing vs shoot)
- Transitions can go between siblings or up to parent

## Unity Animator as State Machine
- Use Animator Controller for visual state machine design
- **StateMachineBehaviour** for state callbacks:
```csharp
public class AttackState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator anim, AnimatorStateInfo info, int layer)
    { /* enable hitbox */ }
    public override void OnStateExit(Animator anim, AnimatorStateInfo info, int layer)
    { /* disable hitbox */ }
}
```
- Good for: animation-driven state (combat, locomotion)
- Bad for: logic-heavy state (AI decisions, game flow)

## Tips
- **Enum FSM**: prototype, simple enemies, UI states
- **Class-based FSM**: complex AI, player controller, game flow
- **HFSM**: deep AI behavior trees alternative, complex combat systems
- **Animator**: animation-coupled states (blend trees, transitions)
- Always handle edge cases: what if TransitionTo is called during Enter/Exit?
- Use state history stack for ""return to previous state"" (pause menu, stunned)
- Consider **Behavior Trees** for AI with many conditions (use NodeCanvas or custom)");
        }

        [McpPrompt("camera_system",
            "Camera system design: Cinemachine configuration, multi-camera switching, post-processing, split-screen")]
        public static ToolResult CameraSystem()
        {
            return ToolResult.Text(@"# Camera System Design Guide

## Cinemachine (Recommended)
Install: `com.unity.cinemachine` via Package Manager

### Core Concepts
- **CinemachineBrain**: on Main Camera — controls which virtual camera is active
- **Virtual Cameras**: define camera behavior (follow, aim, noise, etc.)
- **Priority**: highest priority virtual camera becomes active
- **Blending**: automatic smooth transition between virtual cameras

### Common Virtual Camera Types
| Type | Use Case | Key Settings |
|------|----------|-------------|
| Follow + LookAt | 3rd person | Body: Transposer, Aim: Composer |
| POV | 1st person | Body: Hard Lock to Target, Aim: POV |
| Framing Transposer | 2D/Side-scroll | Body: Framing Transposer |
| State-Driven | Animation-based | Animator drives camera states |
| Free Look | Orbit camera | 3-rig orbit system |

### 3rd Person Setup
```
CinemachineVirtualCamera
├── Body: Cinemachine3rdPersonFollow
│   ├── Shoulder Offset: (0.5, 0.4, 0)
│   ├── Vertical Arm Length: 0.3
│   └── Camera Distance: 4
├── Aim: Cinemachine Composer
│   ├── Tracked Object Offset: (0, 1.5, 0)
│   └── Dead Zone: small for responsive, large for cinematic
└── Noise: Cinemachine Basic Multi Channel Perlin (handheld feel)
```

## Multi-Camera Switching
```csharp
// Option 1: Priority-based (Cinemachine)
combatCam.Priority = 20;  // higher = active
exploreCam.Priority = 10;

// Option 2: Direct activation
combatCam.gameObject.SetActive(true);
exploreCam.gameObject.SetActive(false);

// Blend settings in CinemachineBrain:
// Default Blend: EaseInOut, 1.5s
// Custom Blends: define specific camera-to-camera transitions
```

## Post-Processing (URP Volume)
```
Global Volume (affects entire scene)
├── Bloom (intensity: 0.5, threshold: 1.0)
├── Color Adjustments (post exposure, contrast, saturation)
├── Tonemapping (ACES)
└── Vignette (intensity: 0.3)

Local Volume (trigger zone — e.g., underwater, dark room)
├── Box Collider (trigger)
├── Color Adjustments (blue tint, lower saturation)
└── Depth of Field
```

## Split-Screen
```csharp
// Camera 1: left half
camera1.rect = new Rect(0, 0, 0.5f, 1);
// Camera 2: right half
camera2.rect = new Rect(0.5f, 0, 0.5f, 1);
// Each camera follows its own player with separate Cinemachine Brain
```

## Tips
- Use **Cinemachine Impulse** for camera shake (explosions, impacts)
- Use **Cinemachine Confiner** to keep camera within level bounds (2D/3D)
- Use **CinemachinePath** for cutscene camera rails (dolly track)
- Set Camera.depth for render order when using multiple cameras
- For UI, use a separate Camera with Culling Mask = UI layer only");
        }

        [McpPrompt("lighting_setup",
            "Lighting configuration guide: baked vs realtime, Light Probe, Reflection Probe, URP/HDRP differences")]
        public static ToolResult LightingSetup()
        {
            return ToolResult.Text(@"# Lighting Configuration Guide

## Baked vs Realtime vs Mixed
| Mode | Performance | Quality | Dynamic Objects | Use Case |
|------|-----------|---------|-----------------|----------|
| Realtime | Expensive | Limited bounces | Full support | Dynamic time-of-day |
| Baked | Free at runtime | Best (many bounces) | No direct light | Static indoor scenes |
| Mixed | Medium | Good | Shadow casting | Most games |

## Recommended Setup (URP)
1. **Directional Light**: Mixed mode, soft shadows
2. **Point/Spot Lights**: Baked for static fill, Realtime for gameplay effects
3. **Environment Lighting**: Skybox or gradient (Lighting Settings)
4. **Bake lightmaps**: Lighting > Generate Lighting (or auto-generate)

## Light Probes
- Place in a grid throughout the scene for dynamic object lighting
- Denser placement near lighting transitions (doorways, shadows)
- Dynamic objects sample nearest probe group for indirect light color
- Edit mode: place Light Probe Group, position probes in 3D grid
```
Usage: automatic — dynamic objects with MeshRenderer use probes by default
Set Renderer.lightProbeUsage = LightProbeUsage.BlendProbes
```

## Reflection Probes
- Capture environment reflections for shiny/metallic surfaces
- **Baked**: cheapest, good for static environments
- **Realtime**: expensive, use for dynamic reflections (water, mirrors)
- **Box Projection**: enable for indoor environments (correct parallax)
- Place one per room/area, set bounding box to match the space

## Lightmap Settings
| Setting | Quality | Performance |
|---------|---------|-------------|
| Lightmapper | Progressive GPU | Fastest baking |
| Lightmap Resolution | 20-40 texels/unit | Balance quality/size |
| Direct/Indirect Samples | 32/128 (draft) → 512/2048 (final) | Higher = less noise |
| Ambient Occlusion | Enable, distance 1-3 | Adds depth to corners |
| Compress Lightmaps | Enable for builds | Reduces memory |

## URP vs HDRP Lighting Differences
| Feature | URP | HDRP |
|---------|-----|------|
| Max realtime lights per object | 8 (default) | Unlimited (tiled/clustered) |
| Global Illumination | Lightmaps, Light Probes | + Screen Space GI, Ray-traced GI |
| Shadows | Shadow Maps | + Contact Shadows, Ray-traced Shadows |
| Volumetric Lighting | Limited (via shader tricks) | Built-in volumetric fog/light |
| Area Lights | Baked only | Realtime area lights |
| Reflection | Probes, Planar (limited) | + Screen Space Reflection, Ray-traced |

## Performance Tips
- Use **Shadow Cascades**: 2 for mobile, 4 for PC/console
- **Shadow Distance**: keep as short as possible (50-100 for most games)
- **Shadow Resolution**: per-light setting, lower for distant/minor lights
- **Light Culling**: use layers to limit which objects each light affects
- **Light Layers** (URP 2022+): fine-grained control over light-object interaction
- Use **Enlighten Realtime GI** only if you need dynamic indirect lighting
- Bake lighting for release — use auto-generate only during development");
        }
    }
}
