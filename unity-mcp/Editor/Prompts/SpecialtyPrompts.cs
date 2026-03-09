using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Prompts
{
    [McpToolGroup("SpecialtyPrompts")]
    public static class SpecialtyPrompts
    {
        [McpPrompt("animation_workflow",
            "Animation workflow: Animator Controller design, Animation Clip creation, state machine splitting")]
        public static ToolResult AnimationWorkflow()
        {
            return ToolResult.Text(@"# Unity Animation Workflow Guide

## Animator Controller Design
- One controller per character archetype (Player, Enemy, NPC)
- Use **Sub-State Machines** to group related states (Locomotion, Combat, Interaction)
- Use **Blend Trees** for continuous motion (walk/run blend by speed parameter)
- Use **Layers** for additive animations (upper body aiming while legs run)

## Animator Controller Structure
```
AC_Player
├── Base Layer (Weight: 1)
│   ├── Locomotion (Blend Tree: Idle → Walk → Run)
│   ├── Jump (States: JumpStart → InAir → Land)
│   ├── Combat (Sub-State Machine)
│   │   ├── Attack1 → Attack2 → Attack3 (combo chain)
│   │   └── Block
│   └── Death
├── Upper Body Layer (Weight: 0.8, Avatar Mask: UpperBody)
│   ├── Empty (default)
│   ├── AimRifle
│   └── Throw
└── Face Layer (Weight: 1, Avatar Mask: Head)
    ├── Neutral
    ├── Happy
    └── Angry
```

## Animation Clip Best Practices
- Set loop time for cyclical animations (walk, idle, run)
- Use Animation Events for gameplay hooks (footstep sounds, damage frames)
- Import settings: Bake Into Pose for root motion clips
- Compress clips: Keyframe Reduction + Optimal compression in import settings

## State Machine Tips
- Use **Any State** transitions sparingly (death, hit reaction)
- Set **transition duration** to 0.1-0.25s for responsive gameplay
- Use **Has Exit Time = false** for player-triggered transitions
- Use **Has Exit Time = true** for animation-driven sequences
- **Parameter naming**: use consistent convention (isGrounded, speed, attackTrigger)

## Animation Clip Creation (Code)
```csharp
var clip = new AnimationClip();
clip.SetCurve("""", typeof(Transform), ""localPosition.x"",
    AnimationCurve.Linear(0, 0, 1, 5));
AssetDatabase.CreateAsset(clip, ""Assets/Animations/Move.anim"");
```

## Root Motion
- Enable on Animator component for character-driven movement
- Check ""Bake Into Pose"" on clips that shouldn't move the character
- Use `OnAnimatorMove()` to override root motion application");
        }

        [McpPrompt("ui_toolkit_guide",
            "UI Toolkit guide: USS styles, VisualElement hierarchy, data binding, comparison with uGUI")]
        public static ToolResult UIToolkitGuide()
        {
            return ToolResult.Text(@"# UI Toolkit Guide

## UI Toolkit vs uGUI Comparison
| Feature | UI Toolkit | uGUI |
|---------|-----------|------|
| Rendering | Retained mode | Immediate rebuild |
| Styling | USS (CSS-like) | Per-component Inspector |
| Layout | Flexbox | RectTransform + Layout Groups |
| Data binding | Built-in (2023.2+) | Manual or third-party |
| Runtime support | Full (Unity 2023+) | Full |
| Best for | Complex UI, tools | Simple game UI |

## UXML Structure
```xml
<ui:UXML xmlns:ui=""UnityEngine.UIElements"">
    <ui:VisualElement class=""panel"">
        <ui:Label text=""Player Stats"" class=""title""/>
        <ui:ProgressBar value=""75"" title=""Health"" class=""health-bar""/>
        <ui:Button text=""Inventory"" name=""btn-inventory""/>
        <ui:ListView name=""item-list""/>
    </ui:VisualElement>
</ui:UXML>
```

## USS Styling
```css
.panel {
    padding: 10px;
    background-color: rgba(0, 0, 0, 0.8);
    border-radius: 8px;
}
.title {
    font-size: 24px;
    color: white;
    -unity-font-style: bold;
    margin-bottom: 10px;
}
.health-bar > .unity-progress-bar__progress {
    background-color: rgb(200, 50, 50);
}
```

## C# Setup
```csharp
public class GameUI : MonoBehaviour
{
    [SerializeField] private UIDocument _document;

    void OnEnable()
    {
        var root = _document.rootVisualElement;
        var btn = root.Q<Button>(""btn-inventory"");
        btn.clicked += OnInventoryClicked;

        var list = root.Q<ListView>(""item-list"");
        list.makeItem = () => new Label();
        list.bindItem = (e, i) => ((Label)e).text = _items[i].name;
        list.itemsSource = _items;
    }
}
```

## Tips
- Use `Q<T>(name)` and `Q<T>(className: ""class"")` for element queries
- USS supports pseudo-classes: `:hover`, `:active`, `:focus`, `:checked`
- Use `VisualElement.schedule` for delayed/repeated callbacks
- For Editor tools: always prefer UI Toolkit over IMGUI for new development
- Use USS variables for theming: `--primary-color: rgb(100, 150, 255);`");
        }

        [McpPrompt("shader_basics",
            "Shader basics: ShaderGraph nodes, URP/HDRP adaptation, custom shader patterns")]
        public static ToolResult ShaderBasics()
        {
            return ToolResult.Text(@"# Unity Shader Basics

## Shader Graph (Visual, Recommended)
- Create: Right-click > Create > Shader Graph > URP/HDRP > Lit/Unlit Shader Graph
- Core nodes: Sample Texture 2D, Fresnel Effect, Lerp, Multiply, Time
- Output: connect to Fragment (Base Color, Normal, Metallic, Smoothness, Emission)

## Common Shader Graph Patterns
| Effect | Key Nodes |
|--------|----------|
| Dissolve | Step(noise, threshold) → Alpha Clip |
| Rim light | Fresnel → Multiply(color) → Add to Emission |
| Scrolling UV | Time × speed + UV → Sample Texture |
| Triplanar | Triplanar node for seamless terrain/rock textures |
| Color gradient | Gradient + UV.y → Base Color |
| Outline | Two-pass: normal mesh + scaled mesh with front-face cull |

## URP vs HDRP Shader Differences
| Feature | URP | HDRP |
|---------|-----|------|
| Lit model | Simplified PBR | Full PBR + Subsurface, Clear Coat |
| Custom pass | Render Feature | Custom Pass Volume |
| Shader Graph targets | Universal target | HD target |
| Performance | Mobile-friendly | Desktop/Console |

## HLSL Custom Shader (URP)
```hlsl
Shader ""Custom/SimpleUnlit""
{
    Properties
    {
        _MainTex (""Texture"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
            }
            ENDHLSL
        }
    }
}
```

## Tips
- Always use `TransformObjectToHClip` (URP) instead of `UnityObjectToClipPos` (Built-in)
- Use `_Time.y` for time-based effects (seconds since start)
- SRP Batcher compatible: use CBUFFER for properties
- Shader variants: use `#pragma shader_feature` for optional features to keep build size small");
        }

        [McpPrompt("testing_strategy",
            "Testing strategy: EditMode/PlayMode test distinction, mock strategies, CI integration")]
        public static ToolResult TestingStrategy()
        {
            return ToolResult.Text(@"# Unity Testing Strategy

## Test Runner (Window > General > Test Runner)
- **Edit Mode Tests**: run without entering Play mode — fast, no scene needed
- **Play Mode Tests**: run in Play mode — test runtime behavior, coroutines, physics

## Edit Mode Tests
```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class HealthSystemTests
{
    [Test]
    public void TakeDamage_ReducesHealth()
    {
        var health = new HealthData(100);
        health.TakeDamage(30);
        Assert.AreEqual(70, health.Current);
    }

    [Test]
    public void TakeDamage_ClampsToZero()
    {
        var health = new HealthData(50);
        health.TakeDamage(999);
        Assert.AreEqual(0, health.Current);
    }
}
```

## Play Mode Tests
```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerMovementTests
{
    [UnityTest]
    public IEnumerator Player_MovesForward_WhenInputApplied()
    {
        var go = new GameObject(""Player"");
        var mover = go.AddComponent<PlayerMover>();
        var startPos = go.transform.position;

        mover.Move(Vector3.forward);
        yield return new WaitForSeconds(0.5f);

        Assert.Greater(go.transform.position.z, startPos.z);
        Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator Projectile_DestroysAfterLifetime()
    {
        var prefab = Resources.Load<GameObject>(""TestBullet"");
        var bullet = Object.Instantiate(prefab);

        yield return new WaitForSeconds(3f);

        Assert.IsTrue(bullet == null); // Unity null check for destroyed objects
    }
}
```

## Test Architecture
- **Separate logic from MonoBehaviour**: pure C# classes are easier to test
- **Dependency injection**: inject interfaces for testable services
- **Test doubles**: use interfaces + simple mocks (or NSubstitute if allowed)

## Assembly Definition for Tests
```json
{
    ""name"": ""Tests.EditMode"",
    ""references"": [""YourGame.Runtime""],
    ""optionalUnityReferences"": [""TestAssemblies""],
    ""includePlatforms"": [""Editor""],
    ""defineConstraints"": [""UNITY_INCLUDE_TESTS""]
}
```

## CI Integration
- Run from command line: `Unity -batchmode -runTests -testPlatform EditMode -testResults results.xml`
- Parse NUnit XML results for CI reporting
- Use `-logFile` to capture Unity logs for debugging failures
- GitHub Actions: use `game-ci/unity-test-runner` action");
        }

        [McpPrompt("debug_workflow",
            "Debug workflow: Profiler usage, Frame Debugger, Memory Profiler, logging strategy")]
        public static ToolResult DebugWorkflow()
        {
            return ToolResult.Text(@"# Unity Debug Workflow

## Profiler (Window > Analysis > Profiler)
- **CPU Module**: identify expensive methods, see call hierarchy
- **GPU Module**: rendering time per pass, overdraw detection
- **Memory Module**: snapshot current allocations, find leaks
- **GC Alloc column**: find per-frame allocations (target: 0 in hot paths)
- **Deep Profile**: enable for full call stacks (slower but complete)
- Profile on target device for accurate mobile performance data

## Frame Debugger (Window > Analysis > Frame Debugger)
- Step through each draw call in a frame
- See exactly what each draw call renders
- Identify: redundant draws, overdraw, batching breaks, shader issues
- Use to verify SRP Batcher / GPU Instancing is working

## Memory Profiler (Package: com.unity.memoryprofiler)
- Take snapshots and compare (find leaks by diff)
- Tree view: see allocation breakdown by type
- Look for: duplicate textures, leaked objects, growing collections
- Compare ""before action"" vs ""after action"" snapshots

## Logging Strategy
```csharp
// Use categories with [HideInCallstack] for clean console
public static class Log
{
    [System.Diagnostics.Conditional(""ENABLE_LOG"")]
    [HideInCallstack]
    public static void Info(string msg) => Debug.Log($""[Game] {msg}"");

    [HideInCallstack]
    public static void Warn(string msg) => Debug.LogWarning($""[Game] {msg}"");

    [HideInCallstack]
    public static void Err(string msg) => Debug.LogError($""[Game] {msg}"");
}
```

## Common Debug Tools
| Tool | Use |
|------|-----|
| Debug.DrawRay/Line | Visualize raycasts, directions in Scene view |
| Gizmos.DrawWireSphere | Visualize ranges, colliders in Scene view |
| Physics.DebugDraw | See collision shapes |
| Debug.Break() | Pause editor at a code point |
| [ContextMenu] | Add right-click debug actions to Inspector |

## Performance Debugging Checklist
1. Profile on target hardware (not just Editor)
2. Check CPU vs GPU bound (Profiler timeline)
3. If CPU: find hottest methods, reduce Update complexity
4. If GPU: check draw calls (Frame Debugger), overdraw, shader complexity
5. If memory: take snapshots, compare, find leaks
6. If GC spikes: find allocations with Profiler GC.Alloc column");
        }

        [McpPrompt("project_setup",
            "Project initialization guide: UPM package structure, asmdef splitting, Git LFS config, EditorSettings")]
        public static ToolResult ProjectSetup()
        {
            return ToolResult.Text(@"# Unity Project Setup Guide

## Initial Configuration
1. **Render Pipeline**: choose URP (most projects) or HDRP (high-end) at creation
2. **Color Space**: Linear (modern standard, required for PBR)
3. **API Compatibility**: .NET Standard 2.1 (or .NET Framework if needed)
4. **Asset Serialization**: Force Text (required for version control)
5. **Editor Settings**: Visible Meta Files (Edit > Project Settings > Editor)

## Assembly Definitions (asmdef)
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Runtime/
│   │   │   ├── Game.Runtime.asmdef    # Core gameplay
│   │   │   ├── Core/                  # Shared utilities
│   │   │   ├── Player/               # Player systems
│   │   │   └── Enemy/                # Enemy systems
│   │   ├── Editor/
│   │   │   └── Game.Editor.asmdef    # Editor tools (references Runtime)
│   │   └── Tests/
│   │       ├── EditMode/
│   │       │   └── Game.Tests.EditMode.asmdef
│   │       └── PlayMode/
│   │           └── Game.Tests.PlayMode.asmdef
```
- asmdef benefits: faster compilation, enforced dependencies, testable
- Rule: no circular references, Editor never referenced by Runtime

## Git Configuration
### .gitignore
```
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
*.csproj
*.sln
*.suo
*.user
*.pidb
*.booproj
```

### .gitattributes (LFS)
```
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.tif filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
```

## Recommended Packages
- **Cinemachine**: camera system
- **TextMeshPro**: text rendering
- **Input System**: modern input handling
- **Addressables**: asset management (medium+ projects)
- **Localization**: multi-language support
- **Test Framework**: unit/integration testing

## Editor Settings Checklist
- [ ] Asset Serialization: Force Text
- [ ] Version Control: Visible Meta Files
- [ ] Enter Play Mode Options: enabled (skip domain/scene reload for faster iteration)
- [ ] Script Compilation: incremental compilation enabled
- [ ] Sprite Packer: Sprite Atlas V2");
        }

        [McpPrompt("2d_game_guide",
            "2D game development guide: Sprite management, Tilemap, 2D physics, pixel perfect")]
        public static ToolResult Game2DGuide()
        {
            return ToolResult.Text(@"# 2D Game Development Guide

## Sprite Management
- **Sprite Atlas**: combine sprites to reduce draw calls (Window > 2D > Sprite Atlas)
- **Pixels Per Unit**: set consistently (16 for pixel art, 100 for HD)
- **Filter Mode**: Point (pixel art) or Bilinear (HD sprites)
- **Compression**: None for pixel art, ASTC/ETC2 for mobile HD

## Tilemap Setup
```
Grid (GameObject with Grid component)
├── Ground (Tilemap + TilemapRenderer, sorting order: 0)
├── Walls (Tilemap + TilemapRenderer + TilemapCollider2D, sorting order: 1)
├── Decoration (Tilemap + TilemapRenderer, sorting order: 2)
└── Collision (Tilemap + TilemapCollider2D + CompositeCollider2D, invisible)
```
- Use **Tile Palette** (Window > 2D > Tile Palette) for painting
- **CompositeCollider2D** on collision tilemap for efficient physics
- **Rule Tiles**: auto-select tile variant based on neighbors (corners, edges)

## 2D Physics
- Use Rigidbody2D + Collider2D (completely separate from 3D physics)
- **Body Types**: Dynamic (player, enemies), Kinematic (platforms), Static (walls)
- **Composite Collider**: merge tilemap colliders for performance
- Gravity Scale: adjust per-object (0 for top-down, 1 for platformer)

## Sorting & Rendering
- Use **Sorting Layers** for major groups: Background, Default, Foreground, UI
- Use **Order in Layer** for fine-tuning within a sorting layer
- For isometric/top-down: use **Transparency Sort Mode** (Custom Axis: Y)
- **Sprite Renderer**: Sort Point = Pivot for accurate depth sorting

## Pixel Perfect (com.unity.2d.pixel-perfect)
- Add `Pixel Perfect Camera` component to camera
- Set Asset PPU to match sprite Pixels Per Unit
- Reference Resolution: target resolution (320x180 for retro)
- Upscale Render Texture: enable for crisp pixel art at any resolution

## 2D Animation
- **Sprite Sheet**: slice in Sprite Editor, use Animator with clip per animation
- **2D Animation Package**: skeletal animation with bones (Sprite Editor > Skinning)
- **Sprite Swap**: swap sprites at runtime for equipment/customization

## Tips
- Use `SpriteRenderer.flipX` instead of negative scale for mirroring
- Use `Physics2D.OverlapCircle` for ground checks in platformers
- Use `Cinemachine + CinemachineConfiner2D` for 2D camera follow with bounds");
        }

        [McpPrompt("3d_modeling_import",
            "3D model import guide: FBX settings, material mapping, LOD configuration, animation splitting")]
        public static ToolResult ModelImport3D()
        {
            return ToolResult.Text(@"# 3D Model Import Guide

## FBX Import Settings
### Model Tab
| Setting | Recommendation |
|---------|---------------|
| Scale Factor | 1 (ensure model is authored at 1 unit = 1 meter) |
| Convert Units | true |
| Mesh Compression | Low/Medium (reduces file size) |
| Read/Write Enabled | false (unless modifying mesh at runtime) |
| Optimize Mesh | true (reorder vertices for GPU cache) |
| Generate Colliders | false (add manually for better control) |
| Normals | Import (use Calculate only if model has no normals) |

### Rig Tab
- **Humanoid**: for characters with standard skeleton (uses Unity retargeting)
- **Generic**: for non-humanoid animated objects (vehicles, doors, animals)
- **None**: for static models
- Configure Avatar: map bones in Avatar Configuration if auto-mapping fails

### Animation Tab
- **Import Animation**: enable for animated FBX
- **Split clips**: define ranges in the clip list (idle: 0-30, walk: 31-60, etc.)
- **Loop Time**: enable for cyclical animations
- **Root Motion**: Bake Into Pose for animations that shouldn't move the root

### Materials Tab
- **Material Creation Mode**: Import via MaterialDescription (standard)
- **Location**: Use External Materials for shared materials across models
- **Remap materials**: assign existing Unity materials to imported model slots

## LOD Configuration
```
LOD Group component
├── LOD 0 (100% - 60%): Full detail mesh
├── LOD 1 (60% - 30%): ~50% polygon count
├── LOD 2 (30% - 10%): ~25% polygon count
└── Culled (< 10%): Don't render
```
- Set LOD thresholds based on screen percentage
- Use LOD Group component on parent GameObject
- Tools: Simplygon, InstaLOD, or Unity's built-in mesh simplification

## Best Practices
- Author at real-world scale (1 unit = 1 meter)
- Triangulate meshes before export (avoid n-gons)
- Clean up: remove hidden faces, merge vertices, delete unused materials
- UV2 for lightmapping: generate in Unity or author in DCC tool
- Use Prefab variants for model instances with different materials/components");
        }

        [McpPrompt("vfx_particle_guide",
            "VFX/particle system guide: Particle System vs VFX Graph, GPU particles, performance budget")]
        public static ToolResult VfxParticleGuide()
        {
            return ToolResult.Text(@"# VFX & Particle System Guide

## Particle System vs VFX Graph
| Feature | Particle System (Shuriken) | VFX Graph |
|---------|---------------------------|-----------|
| Platform | All | Compute Shader required |
| Particle count | Thousands | Millions (GPU) |
| Authoring | Inspector modules | Node graph (visual) |
| Physics | CPU collision, triggers | Limited GPU collision |
| Best for | Mobile, simple VFX | PC/Console, complex VFX |

## Particle System (Shuriken) Key Modules
- **Main**: duration, looping, start lifetime/speed/size/color, gravity
- **Emission**: rate over time/distance, bursts for explosions
- **Shape**: sphere, cone, box — controls spawn area and direction
- **Color/Size over Lifetime**: gradient/curve for fade-in/out
- **Renderer**: Billboard (always face camera), Mesh (3D particles), Trail

## Common VFX Recipes
| Effect | Key Settings |
|--------|-------------|
| Fire | Cone shape, orange→red color, size decrease, noise turbulence |
| Smoke | Large sphere shape, gray color, slow speed, size increase, fade out |
| Explosion | Burst emission (50-200), sphere shape, high start speed, short lifetime |
| Sparks | Cone shape, small size, high speed, gravity, trail renderer |
| Rain | Box shape (wide, thin), downward velocity, stretch billboard |
| Dust | Low emission rate, random direction, very slow, small, fade out |

## VFX Graph (com.unity.visualeffectgraph)
- Create: Right-click > Create > Visual Effects > Visual Effect Graph
- Contexts: Spawn → Initialize → Update → Output
- Use **Property Binders** to connect C# parameters to graph
- **GPU Events**: trigger sub-effects from particle conditions
- **SDF (Signed Distance Field)**: for mesh-conforming particles

## Performance Budget
| Platform | Max Particles | Draw Calls |
|----------|--------------|------------|
| Mobile | 500-2000 | 1-3 per effect |
| PC/Console | 10K-100K | 5-10 per effect |
| VFX Graph (GPU) | 1M+ | 1-2 per system |

## Tips
- **Pool particle systems**: don't instantiate/destroy, use `Play()`/`Stop()`
- **Culling**: enable `Automatic Culling` on renderer for off-screen particles
- **Sub-emitters**: use for secondary effects (sparks from explosion, debris from impact)
- **Prewarm**: enable for effects that should appear full immediately (ambient dust)
- Set **Max Particles** to prevent runaway systems from killing performance
- Use **Simulation Space = World** for effects that should persist after parent moves");
        }

        [McpPrompt("addressables_guide",
            "Addressables asset management guide: Group strategy, remote loading, memory management, version updates")]
        public static ToolResult AddressablesGuide()
        {
            return ToolResult.Text(@"# Addressables Asset Management Guide

## Setup
1. Install: `com.unity.addressables` via Package Manager
2. Window > Asset Management > Addressables > Groups
3. Mark assets as Addressable in Inspector or via Groups window

## Group Strategy
```
Addressable Groups
├── LocalStatic          # Core assets, never change (shaders, core UI)
├── LocalDynamic         # Assets updated with app (levels 1-5)
├── RemoteContent_Maps   # Downloadable maps
├── RemoteContent_DLC    # DLC content
└── PerScene_Level1      # Assets specific to Level 1 (loaded/unloaded together)
```
- Group assets by **load/unload lifecycle** (load together → same group)
- Separate local vs remote for update strategy
- Use Labels for cross-group loading (""level1"", ""characters"", ""audio"")

## Loading Assets
```csharp
// Load single asset
var handle = Addressables.LoadAssetAsync<GameObject>(""Assets/Prefabs/Player.prefab"");
handle.Completed += (op) => {
    if (op.Status == AsyncOperationStatus.Succeeded)
        Instantiate(op.Result);
};

// Load by label
Addressables.LoadAssetsAsync<GameObject>(""enemies"", (enemy) => {
    // Called for each loaded asset
    _enemyPrefabs.Add(enemy);
});

// Instantiate directly
var instHandle = Addressables.InstantiateAsync(""Player"", position, rotation);
```

## Memory Management (Critical!)
```csharp
// ALWAYS release when done
Addressables.Release(handle);           // Release loaded asset
Addressables.ReleaseInstance(instance);  // Release instantiated object

// Scene management
var sceneHandle = Addressables.LoadSceneAsync(""Level1"", LoadSceneMode.Additive);
// Later:
Addressables.UnloadSceneAsync(sceneHandle);
```

## Remote Content Updates
1. Build initial content: Addressables > Build > New Build
2. Update content: Addressables > Build > Update a Previous Build
3. Client checks: `Addressables.CheckForCatalogUpdates()` → `UpdateCatalogs()`
4. Host updated bundles on CDN (S3, CloudFront, etc.)

## Build Settings
- **Build Remote Catalog**: enable for updateable content
- **Bundle Mode**: Pack Together (fewer bundles) vs Pack Separately (granular loading)
- **Compression**: LZ4 for local, LZMA for remote (smaller download, slower load)

## Tips
- Never use `Resources.Load` with Addressables — they're separate systems
- Use `AsyncOperationHandle.IsValid()` before releasing
- Profile memory with Addressables Event Viewer (Window > Asset Management)
- Use `Addressables.DownloadDependenciesAsync` for pre-downloading content
- Track reference counts: every Load needs a Release");
        }

        [McpPrompt("cicd_unity",
            "Unity CI/CD guide: command-line builds, GitHub Actions, test automation, multi-platform publishing")]
        public static ToolResult CicdUnity()
        {
            return ToolResult.Text(@"# Unity CI/CD Guide

## Command-Line Build
```bash
# Basic build
Unity -quit -batchmode -projectPath ./MyProject \
  -executeMethod BuildScript.Build \
  -buildTarget StandaloneWindows64 \
  -logFile build.log

# Run tests
Unity -quit -batchmode -projectPath ./MyProject \
  -runTests -testPlatform EditMode \
  -testResults ./results/editmode.xml \
  -logFile test.log
```

## Build Script
```csharp
public static class BuildScript
{
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ""Build/Game.exe"",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
    }
}
```

## GitHub Actions (game-ci)
```yaml
name: Unity CI
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-test-runner@v4
        with:
          unityVersion: 2022.3.20f1
          testMode: EditMode
          githubToken: ${{ secrets.GITHUB_TOKEN }}
  build:
    needs: test
    runs-on: ubuntu-latest
    strategy:
      matrix:
        targetPlatform: [StandaloneWindows64, StandaloneOSX, WebGL]
    steps:
      - uses: actions/checkout@v4
        with: { lfs: true }
      - uses: game-ci/unity-builder@v4
        with:
          unityVersion: 2022.3.20f1
          targetPlatform: ${{ matrix.targetPlatform }}
      - uses: actions/upload-artifact@v4
        with:
          name: Build-${{ matrix.targetPlatform }}
          path: build/${{ matrix.targetPlatform }}
```

## Activation & Licensing
- game-ci requires Unity license activation
- Personal: use `game-ci/unity-activate` with manual .ulf file
- Pro/Plus: use serial number via secrets (`UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`)

## Best Practices
- Cache Library/ folder between builds for faster iteration
- Use LFS for binary assets, checkout with `lfs: true`
- Separate test and build jobs (fail fast on test failures)
- Use build matrix for multi-platform builds
- Version builds with git tags or commit SHA
- Store build artifacts for QA/deployment pipeline");
        }

        [McpPrompt("mobile_optimization",
            "Mobile optimization guide: thermal management, memory budget, texture compression, shader variant stripping")]
        public static ToolResult MobileOptimization()
        {
            return ToolResult.Text(@"# Mobile Optimization Guide

## Performance Targets
| Metric | Budget |
|--------|--------|
| Frame rate | 30 FPS (stable) or 60 FPS (action games) |
| Frame time | < 33ms (30fps) or < 16ms (60fps) |
| Memory | < 1GB total (< 400MB textures) |
| Draw calls | < 100-200 per frame |
| Triangles | < 100K-300K per frame |
| Texture memory | < 256MB |

## Thermal Management
- **Adaptive Performance** (Samsung): `com.unity.adaptiveperformance`
- Reduce quality when device heats up (lower resolution, fewer particles)
- Use `Application.targetFrameRate = 30` to prevent unnecessary GPU work
- Profile thermal state: monitor FPS drops over 10+ minute sessions

## Texture Optimization
| Format | Platform | Quality | Size |
|--------|----------|---------|------|
| ASTC 6x6 | iOS + Android | Good | Small |
| ETC2 | Android (OpenGL ES 3.0+) | Good | Small |
| PVRTC | iOS (legacy) | Medium | Smallest |

- Disable Read/Write on textures (doubles memory!)
- Use mipmaps for 3D textures, disable for UI
- Max texture size: 2048 for hero assets, 1024 for most, 512 for distant
- Use Sprite Atlas for 2D (reduces draw calls + improves batching)

## Shader Optimization
- **Shader variant stripping**: Project Settings > Graphics > Shader Stripping
- Remove unused shader features (fog, lightmap modes, GPU instancing if unused)
- Use `shader_feature` instead of `multi_compile` for optional features
- Keep fragment shaders simple: avoid expensive math, texture lookups < 4 per pass
- Use **Shader Graph**: optimized output, strip unused nodes automatically

## Memory Management
- **Texture streaming**: enable for large open worlds (auto-load mip levels)
- **Object pooling**: critical on mobile (GC pauses are worse on mobile CPUs)
- **Addressables**: load/unload assets per scene/area
- Profile with **Memory Profiler** package, check for duplicate assets
- `Resources.UnloadUnusedAssets()` between scenes

## Rendering
- Use **URP** (Universal Render Pipeline) for mobile
- Disable HDR if not needed (saves bandwidth)
- Use **Baked lighting** over realtime where possible
- **Shadow Distance**: 20-50 (not 150+)
- **Shadow Cascades**: 1-2 (not 4)
- Use **GPU Instancing** for repeated objects (trees, grass)

## Battery & Heating
- Lower `Application.targetFrameRate` during menus/idle
- Reduce physics simulation rate: `Time.fixedDeltaTime = 0.04f` (25Hz)
- Disable unnecessary camera rendering (UI-only scenes don't need 3D camera)");
        }
    }
}
