using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("ScriptPrompts")]
    public static class ScriptPrompts
    {
        [McpPrompt("unity_script_conventions",
            "C# coding conventions for Unity: naming rules, access modifiers, serialization fields, Unity-specific patterns")]
        public static ToolResult UnityScriptConventions()
        {
            return ToolResult.Text(@"# Unity C# Script Conventions

## Naming
- **Classes/Structs**: PascalCase (`PlayerController`, `EnemySpawner`)
- **Public methods/properties**: PascalCase (`GetHealth()`, `MaxSpeed`)
- **Private fields**: camelCase with underscore prefix (`_health`, `_moveSpeed`)
- **Serialized private fields**: use `[SerializeField]` instead of making public
- **Constants**: PascalCase (`MaxHealth`) or UPPER_SNAKE for true constants
- **Enums**: PascalCase for type and values (`WeaponType.Sword`)
- **Interfaces**: prefix with I (`IInteractable`, `IDamageable`)

## Structure
- One MonoBehaviour per file, filename matches class name
- Order: fields → Unity callbacks → public methods → private methods
- Group related fields with `[Header(""Section Name"")]`
- Use `[Tooltip(""..."")]` for inspector documentation
- Use `[RequireComponent(typeof(...))]` to enforce dependencies

## Serialization
- Prefer `[SerializeField] private` over `public` for inspector-exposed fields
- Use `[HideInInspector]` for public fields that shouldn't show in inspector
- Use `[Range(min, max)]` for numeric constraints
- ScriptableObjects for shared configuration data

## Patterns
- Avoid `Find*` methods in Update — cache references in Awake/Start
- Use `TryGetComponent<T>` (Unity 2019.2+) instead of `GetComponent<T>` + null check
- Prefer `CompareTag(""tag"")` over `gameObject.tag == ""tag""` (no GC alloc)
- Use `[DisallowMultipleComponent]` when appropriate
- Always null-check UnityEngine.Object with `if (obj != null)` or `if (obj)`, not `?.`");
        }

        [McpPrompt("monobehaviour_lifecycle",
            "MonoBehaviour lifecycle best practices: Awake/Start/Update order, coroutines, FixedUpdate vs Update")]
        public static ToolResult MonoBehaviourLifecycle()
        {
            return ToolResult.Text(@"# MonoBehaviour Lifecycle Best Practices

## Initialization Order
1. **Awake()** — Self-initialization, GetComponent references, called even if disabled
2. **OnEnable()** — Subscribe to events, called each time enabled
3. **Start()** — Cross-reference initialization, called once before first Update
4. **Use Script Execution Order** for inter-script dependencies (Edit > Project Settings > Script Execution Order)

## Update Loops
- **Update()** — Game logic, input, frame-dependent behavior. Use `Time.deltaTime`
- **FixedUpdate()** — Physics calculations, Rigidbody manipulation. Uses `Time.fixedDeltaTime`
- **LateUpdate()** — Camera follow, post-processing of positions after Update

## Coroutines
- Use for sequences, timed actions, spreading work across frames
- Always stop coroutines on disable: `StopAllCoroutines()` in `OnDisable()`
- Prefer `yield return null` over `yield return new WaitForEndOfFrame()` (less overhead)
- Cache `WaitForSeconds` instances to avoid GC: `static readonly WaitForSeconds Wait = new(1f)`

## Cleanup
- **OnDisable()** — Unsubscribe from events, stop coroutines
- **OnDestroy()** — Final cleanup, release native resources
- Always unsubscribe from events to prevent memory leaks

## Common Pitfalls
- Don't call `Destroy(gameObject)` in Awake — use `DestroyImmediate` only in Editor
- Don't rely on Awake order between objects — use events or explicit init
- Avoid heavy operations in Update — use timers or InvokeRepeating for periodic tasks");
        }

        [McpPrompt("error_handling",
            "Unity C# error handling: try-catch boundaries, Debug.LogException, null check strategies")]
        public static ToolResult ErrorHandling()
        {
            return ToolResult.Text(@"# Unity C# Error Handling

## Null Checks
- Unity overrides `==` for destroyed objects: `if (obj != null)` checks both null AND destroyed
- **Never use** `?.` or `??` with UnityEngine.Object — they bypass Unity's null check
- Use pattern: `if (obj) { ... }` — implicit bool conversion handles both cases
- Use `TryGetComponent<T>(out var comp)` instead of `GetComponent<T>()` + null check

## Try-Catch Strategy
- **DO** wrap: external API calls, file I/O, reflection, JSON parsing, network calls
- **DON'T** wrap: normal Unity API calls (handle null returns instead)
- Catch specific exceptions, not bare `catch (Exception)`
- Use `Debug.LogException(ex)` to preserve stack trace in console

## Debug Logging
- `Debug.Log()` — informational
- `Debug.LogWarning()` — recoverable issues
- `Debug.LogError()` — serious problems
- `Debug.LogException(ex)` — exceptions with full stack trace
- Use `[HideInCallstack]` attribute on wrapper methods to clean up console stack
- Strip logs from builds with `#if UNITY_EDITOR` or Conditional attribute

## Assertions
- `Debug.Assert(condition, message)` — development-time checks
- Use `[System.Diagnostics.Conditional(""UNITY_ASSERTIONS"")]` for assertion methods

## Error Recovery
- Prefer graceful degradation over crashes
- Return default values when possible
- Use Result<T> pattern for operations that can fail: return success/error instead of throwing");
        }

        [McpPrompt("serialization_guide",
            "Serialization guide: [SerializeField], ScriptableObject, JsonUtility, custom Inspector")]
        public static ToolResult SerializationGuide()
        {
            return ToolResult.Text(@"# Unity Serialization Guide

## Unity Serialization Rules
- **Serialized**: public fields, [SerializeField] private fields
- **Not serialized**: static, const, readonly, properties, [NonSerialized] fields
- Supported types: primitives, string, Vector2/3/4, Color, enums, AnimationCurve,
  arrays/Lists of serializable types, [Serializable] structs/classes, UnityEngine.Object refs

## [SerializeField] Best Practices
- Always prefer `[SerializeField] private float _speed` over `public float speed`
- Use `[field: SerializeField]` for auto-properties (Unity 2020.1+)
- Use `[SerializeReference]` for polymorphic serialization (interfaces, base classes)

## ScriptableObject
- Use for shared data (weapon stats, dialog, config)
- Create with `[CreateAssetMenu(fileName = ""New..."", menuName = ""Game/..."")]`
- Reference from MonoBehaviours — multiple objects share same data asset
- Runtime changes are persistent in Editor, reset on build — use Instantiate() for runtime copies

## JsonUtility
- Fast but limited: no Dictionary, no polymorphism, no properties
- Use `JsonUtility.ToJson(obj, prettyPrint)` / `JsonUtility.FromJson<T>(json)`
- For complex serialization: use Newtonsoft.Json (com.unity.nuget.newtonsoft-json)

## Custom Inspectors
- `[CustomEditor(typeof(T))]` — for MonoBehaviour/ScriptableObject
- `[CustomPropertyDrawer(typeof(T))]` — for serializable classes/structs/attributes
- Use `SerializedProperty` + `EditorGUILayout.PropertyField()` for undo support
- Always call `serializedObject.ApplyModifiedProperties()` at the end");
        }
    }
}
