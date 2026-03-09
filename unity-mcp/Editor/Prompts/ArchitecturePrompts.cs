using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("ArchitecturePrompts")]
    public static class ArchitecturePrompts
    {
        [McpPrompt("gameobject_architecture",
            "Component architecture guide: composition over inheritance, ScriptableObject data-driven, single responsibility")]
        public static ToolResult GameObjectArchitecture()
        {
            return ToolResult.Text(@"# Unity Component Architecture Guide

## Composition Over Inheritance
- **Don't**: PlayerMage extends Player extends Character extends MonoBehaviour
- **Do**: GameObject with Health, Movement, MageAbilities, Inventory components
- Each component handles one responsibility
- Communication via events, interfaces, or ScriptableObject channels

## ScriptableObject Data-Driven Design
- **Data containers**: WeaponData, CharacterStats, LevelConfig
- **Event channels**: GameEvent ScriptableObject for decoupled communication
- **Runtime sets**: track active enemies, collectibles without singletons
- **Enum alternatives**: define types as ScriptableObjects for extensibility

## Interface-Based Design
- Define behaviors as interfaces: `IInteractable`, `IDamageable`, `ISaveable`
- Use `GetComponent<IInteractable>()` to query capabilities
- Enables testing with mocks and decouples systems

## Common Patterns
- **Service Locator**: static registry for global services (AudioManager, SaveSystem)
- **Observer**: C# events or ScriptableObject event channels
- **State Machine**: enum + switch for simple, class-based for complex
- **Object Pool**: reuse frequent instantiate/destroy (bullets, VFX)
- **Command**: encapsulate actions for undo, replay, networking

## Anti-Patterns to Avoid
- God objects (one script does everything)
- Deep inheritance hierarchies
- Direct references between unrelated systems
- Singletons for everything (use dependency injection or ScriptableObject services)
- String-based programming (SendMessage, Find, CompareTag with literals)");
        }

        [McpPrompt("code_review_checklist",
            "Code review checklist: common Unity anti-patterns, performance traps, memory leak checks")]
        public static ToolResult CodeReviewChecklist()
        {
            return ToolResult.Text(@"# Unity Code Review Checklist

## Performance
- [ ] No `Find*`, `GetComponent*` calls in Update/FixedUpdate — cache in Awake/Start
- [ ] No string concatenation in hot paths — use StringBuilder or interpolation
- [ ] No LINQ in Update (causes GC allocation)
- [ ] No `new` allocations in Update for delegates, closures, or collections
- [ ] Physics queries use NonAlloc variants (RaycastNonAlloc, OverlapSphereNonAlloc)
- [ ] Coroutines cache WaitForSeconds instances

## Memory
- [ ] Event subscriptions balanced: += in OnEnable, -= in OnDisable
- [ ] No static references to scene objects (prevents garbage collection)
- [ ] Textures/RenderTextures released when no longer needed
- [ ] Addressables: release handles properly

## Correctness
- [ ] UnityEngine.Object null checks use `if (obj)` not `?.`
- [ ] Destroy vs DestroyImmediate used correctly (Destroy in Play, Immediate in Editor)
- [ ] Undo.RecordObject called before modifying in Editor tools
- [ ] [SerializeField] used instead of public for Inspector fields
- [ ] CompareTag() used instead of `== ""tag""`

## Thread Safety
- [ ] Unity API only called from main thread
- [ ] Async/await returns to main thread context or uses UniTask
- [ ] No race conditions in coroutine + async mixed code

## Architecture
- [ ] No circular dependencies between assemblies
- [ ] No business logic in MonoBehaviour callbacks — delegate to services
- [ ] Interfaces used for cross-system communication
- [ ] Magic numbers extracted to constants or ScriptableObject config");
        }

        [McpPrompt("async_programming",
            "Async programming in Unity: async/await, UniTask, coroutines comparison, thread safety")]
        public static ToolResult AsyncProgramming()
        {
            return ToolResult.Text(@"# Async Programming in Unity

## Coroutines vs async/await vs UniTask

| Feature | Coroutines | async/await | UniTask |
|---------|-----------|-------------|---------|
| Return type | IEnumerator | Task | UniTask |
| Cancellation | StopCoroutine | CancellationToken | CancellationToken |
| Error handling | Limited | try/catch | try/catch |
| GC allocation | WaitFor* objects | Task objects | Zero-alloc |
| Requires MonoBehaviour | Yes | No | No |
| Can await Unity events | yield return | No (native) | Yes |

## async/await in Unity
- Unity 2023.1+: experimental Awaitable API
- Use `await Task.Yield()` to return to main thread
- **Danger**: Task continuations may run on thread pool — always sync back
- Use `SynchronizationContext.Current` or UniTask for safety

## UniTask (Recommended)
```csharp
// Install: https://github.com/Cysharp/UniTask
async UniTaskVoid LoadAsync(CancellationToken ct)
{
    await UniTask.Delay(1000, cancellationToken: ct);
    await SceneManager.LoadSceneAsync(""Main"").ToUniTask(cancellationToken: ct);
    // Safe — automatically on main thread
}
```

## Thread Safety Rules
- ALL Unity API calls must be on main thread
- Use MainThreadDispatcher pattern for background → main thread communication
- Don't access Transform, GameObject, or any Component from background threads
- Use `Application.exitCancellationToken` (2022.2+) for app lifetime

## Cancellation
- Always accept CancellationToken for async operations
- Link to `destroyCancellationToken` on MonoBehaviour (2022.2+)
- Cancel on OnDestroy/OnDisable to prevent operating on destroyed objects");
        }

        [McpPrompt("scriptableobject_patterns",
            "ScriptableObject design patterns: data containers, event channels, runtime sets, singleton alternatives")]
        public static ToolResult ScriptableObjectPatterns()
        {
            return ToolResult.Text(@"# ScriptableObject Design Patterns

## 1. Data Container
```csharp
[CreateAssetMenu(menuName = ""Game/Weapon Data"")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int damage;
    public float attackSpeed;
    public GameObject prefab;
}
```
- Reference from MonoBehaviours: `[SerializeField] WeaponData _data`
- Multiple enemies can share same WeaponData asset
- Designers edit in Inspector without touching code

## 2. Event Channel (Decoupled Communication)
```csharp
[CreateAssetMenu(menuName = ""Events/Game Event"")]
public class GameEvent : ScriptableObject
{
    private readonly List<System.Action> _listeners = new();
    public void Raise() => _listeners.ForEach(l => l?.Invoke());
    public void Register(System.Action listener) => _listeners.Add(listener);
    public void Unregister(System.Action listener) => _listeners.Remove(listener);
}
```
- Drag event asset to both sender and receiver in Inspector
- No direct references between systems

## 3. Runtime Set
```csharp
[CreateAssetMenu(menuName = ""Sets/Enemy Set"")]
public class EnemySet : ScriptableObject
{
    public readonly List<Enemy> Items = new();
    public void Add(Enemy e) => Items.Add(e);
    public void Remove(Enemy e) => Items.Remove(e);
}
```
- Enemies register in OnEnable, unregister in OnDisable
- UI reads the set to show enemy count — no singleton needed

## 4. Enum Alternative
- Instead of `enum WeaponType { Sword, Bow }`, create WeaponType ScriptableObjects
- New types added by creating assets, no code changes
- Each type asset can carry data (icon, description, stats modifier)

## 5. App Configuration
- Replace static config classes with ScriptableObject assets
- Loaded via Resources, Addressables, or direct reference
- Different configs for debug/release builds");
        }
    }
}
