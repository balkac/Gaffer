# Performance Playbook — Unity

How I keep a Unity project smooth and low-garbage. Most of this is *structural* — the same layering
that makes the core testable (see [`ARCHITECTURE.md`](ARCHITECTURE.md)) also keeps the engine's
per-frame overhead low — plus a handful of concrete Unity habits. These are defaults, not universal
laws: profile before optimising (§11), and drop any of these where a measurement says it doesn't
matter. The engine's correctness-side semantics (lifecycle, time, object lifetime) live in
[`UNITY.md`](UNITY.md).

> **Baseline:** Unity 6 (6000.x) · C# 9.0 · URP. Several rules below are version-tagged; in
> particular, do **not** import allocation folklore from modern .NET (C# 11 method-group caching,
> C# 10 interpolation handlers, span-based `Sort`) — none of it is active on this baseline (§8).

---

## 1. Light by construction

The cheapest work is the work you never ask the engine to do.

- **Few `MonoBehaviour`s.** Keep rules, flow and state as **plain C# objects** in the pure layers;
  let only the thin Presentation layer hold `MonoBehaviour`s. Fewer components means fewer
  engine-driven `Update` callbacks and less per-object bookkeeping — the *same* property that lets the
  core run headless. The mechanism is real, not folklore: every engine callback crosses the
  native→managed interop boundary even when the body is trivial (Unity's own "10000 Update() calls"
  measurement; the Unity 6 manual recommends a custom update manager for exactly this reason). One
  manager iterating a plain list beats N engine callbacks.
- **No `ParticleSystem` for simple, countable effects.** A pool of ordinary `SpriteRenderer`s driven
  by one tween library costs only the sprites it draws — no component and no `Update` *per particle*.
  Reach for `ParticleSystem` / VFX Graph when you genuinely need many particles or GPU simulation —
  and then apply a mobile budget per prefab: pooled + prewarmed, bounded `maxParticles`, one shared
  atlas material, collision/lights/trails off, and a `Play()`/`Emit()` path measured to allocate
  nothing.

## 2. Prefer world-space to a UI canvas for a small, dynamic HUD

uGUI's `Canvas` batches its graphics into combined meshes and, whenever **any** element on that
canvas changes — geometry, colour, enable/disable, layout — marks the canvas **dirty** and rebuilds
the batch (a layout pass plus vertex/mesh regeneration). A per-frame change like a **counting-up
score or a ticking timer** re-batches its whole canvas *every frame*; the usual workaround is to split
canvases so the churn is contained.

For a small, fixed HUD you can sidestep the mechanism entirely: draw each element as a world-space
`SpriteRenderer` / `TextMeshPro` (the 3D renderer, **not** `TextMeshProUGUI`) through the same camera,
ordered by sorting order and z.

- **No canvas rebuilds** — each label is an independent renderer, so updating the score regenerates
  just that one text mesh; there's no canvas to dirty and no rebuild cascade.
- **No graphic raycaster / `EventSystem`** — hit-test buttons with a direct
  `renderer.bounds.Contains(pointerWorld)` against the pointer you already read for other input. No
  extra raycast pass, no per-graphic `CanvasRenderer`.
- **One render path** — text and sprites go through the camera exactly like the rest of the scene.

The trade-off is real and bounds the advice: world-space UI has **no automatic anchoring or layout**,
so each view positions itself from the camera's `orthographicSize`/`aspect` and re-anchors **only when
the aspect changes** (a guarded check in `LateUpdate`). For a small fixed HUD that's simpler and
cheaper than a canvas; for a large, densely interactive UI, uGUI / UI Toolkit is the right tool.
Choose by the UI, not by reflex — and when you do choose the canvas, **§15 is that branch's rulebook**.

## 3. Stable runtime — budgets from a config asset

Set frame-rate and animation budgets **once, at boot**, from a `PerformanceSettings`
`ScriptableObject` so they're tuned without touching code:

```csharp
Application.targetFrameRate = TargetFrameRate;   // e.g. 60
QualitySettings.vSyncCount   = VSyncCount;
DOTween.defaultRecyclable    = true;             // reuse tween objects instead of re-allocating
DOTween.SetTweensCapacity(TweenCapacity, SequenceCapacity);
```

Pre-sizing the tween/sequence pools means the animation system never grows its internal arrays
mid-game (which would spike a frame), and `defaultRecyclable` lets it reuse completed tweens.

**This boot step is not optional on mobile:** Unity's default `targetFrameRate` (−1) means the
*platform default*, which on Android/iOS is a **fixed 30 fps** "to conserve battery power,
independent of the native refresh rate" (verbatim, Unity 6 docs) — and mobile platforms **ignore
`vSyncCount`** entirely. A game that skips the explicit set ships rock-steady 30 fps no matter how
light the frame is. Two documented details: the effective rate rounds **down** to a divisor of the
display refresh, and iOS ProMotion (120 Hz) needs the Xcode/Info.plist opt-in — default is 60.

**Write the frame budget down and hold systems to it.** 60 fps is 16.7 ms, but Unity's own mobile
guidance budgets only **~65% of the frame (~11 ms at 60 fps)**, leaving the rest as thermal and
battery headroom — an uncapped/full-budget game throttles itself into jank. Split that budget across
systems (sim, view/tween, render) and attach the profiler numbers to "done" (§11).

## 4. Low garbage — lifetime, pooling, and the action path

**Two pool contracts — name which one you mean:**

- **Burst pools** (effects, projectiles, popups): `ObjectPool<T>` **prewarmed** to a configured size
  and **capped** at a maximum; `Get()` for a burst, `Release()` back; overflow beyond the cap is
  dropped, not allocated.
- **Content pools** (board cells, per-level visuals, anything sized by authored content):
  **grow-only**, prewarmed at boot to the **largest content in the catalogue** (a `MaxContentHint`
  derived from the shipped data, not a guessed nominal size). If the prewarm target is smaller than
  the real maximum, the first late encounter with the biggest content pays the whole growth spike
  mid-session — measured on device as a one-time multi-hundred-KB hit.

**Rebake in place; don't rebuild.** Level/round-scoped visuals (board meshes, decks, obstacle
overlays, tutorial dressing) are **app-lifetime components that re-bake in place** for each level —
grow-only buffers, count-limited mesh writes (§13) — never a `Destroy`-everything +
recreate-everything cycle. The Destroy/rebuild pattern was measured at **170–250 KB of GC per level
transition**, and because `Destroy` is deferred to end of frame, the old and new copies coexist for a
frame and the *peak* doubles. A rebake transition allocates nothing once buffer capacities cover the
largest board. (Per-level components that must exist get a `Configure(...)`-style rebind that
explicitly resets every clock, pending action, and stale handle — see `UNITY.md` §5 and
`ARCHITECTURE.md` §6 for the ownership side.)

Aim for **zero allocation on the idle path** — when nothing is happening, the update loops should
allocate nothing. Where allocation is unavoidable, keep it **off the per-frame path**: e.g. one
outcome record *per action* (not per frame), and a per-frame recompute (a drag preview) kept small and
only while it's actually needed.

**The action frame is a hot path too.** "Zero per frame" is not enough: a tap that fans an effect
over N elements can burst tens of KB in one frame and trigger collections mid-play. Discipline for
bursts (each of these was measured as a real multi-KB offender before being banned):

- **No per-element tween/closure/`DelayedCall`.** An N-element ripple is ONE clocked `Update` (or one
  tween) driving all elements from a schedule computed up front — never N tweens each capturing a
  closure. This also removes the tween-library allocation entirely.
- **Reuse scratch collections.** BFS/search/schedule dictionaries, sets, queues and lists are fields:
  `Clear()` and refill per action, never `new` per action. After capacities settle, the action
  allocates only its outcome record.
- **Hot-loop APIs must not return collections.** An array-returning helper (`GetNeighbours()`)
  called per node of a search allocates per node; give hot loops an indexed/out-parameter variant
  (`GetNeighbour(i)`) and keep the convenient one for cold paths and tests.
- **Cache every long-lived delegate once.** A method group passed as a callback
  (`Subscribe(OnThing)`) creates a **new delegate object at every call** on this baseline (§8) — flow
  continuations, rebind callbacks, and input handlers are created once at construction and stored in
  fields, not re-passed per level or per frame.
- **Logs are allocations (and I/O).** Interpolated `Debug.Log` on action paths is editor-only
  (`#if UNITY_EDITOR`); a device development build pays both the string and the logcat write.
- **No runtime IMGUI (`OnGUI`) — dev overlays included.** IMGUI allocates tens of KB per frame just
  to pump events and repaint; a debug HUD drawn with it poisons the very numbers it reports. Overlays
  are code-built uGUI (or equivalent) with cached strings.

**Schedule the writes, too.** Disk writes are frame cost even when the bytes are small: never save
on a frame that is already opening UI or playing a landing beat; **coalesce** multiple state changes
of one flow moment (reward + stars + progress pointer) into a single write; and reuse the serializer
machinery (cached serializer instance + `StringBuilder` + `StreamWriter.Write(StringBuilder)`)
instead of rebuilding it per save. Atomicity and platform rules for the save file live in
`UNITY.md` §7.

## 5. Safe update loops

`Update` methods do the least work possible and never busy-spin.

- **Early-return** immediately when there's nothing to do — an input handler with no pointer event
  this frame, an animator that has reached its target — so a still frame costs almost nothing.
- **Step toward a target and stop** (`Mathf.MoveTowards`) rather than running an easing update forever.
- **Don't poll game state each frame.** State changes in response to an action; the view animates from
  that action's outcome (§8 of `ARCHITECTURE.md`), not by diffing the world every frame.

## 6. Scalable core algorithms

Keep the hot core operations **linear in the data** — a straight pass over cells/entities, O(n), not a
combinatorial search — so cost grows predictably with size rather than blowing up. Because the core is
pure and synchronous, this is also exactly what makes it fast (and cheap) to test.

- **Constructive, never retry-random.** When the game must *repair* a state (reshuffle a deadlocked
  board, seed a guaranteed move), build the valid state deterministically in one pass — never "shuffle
  and re-test until valid", which has unbounded worst case and untestable behaviour. Detection should
  fall out of work you already do (e.g. "zero groups found during the post-move recompute" *is* the
  deadlock test — no extra scan).
- **Recompute incrementally, propagate by event.** Derived per-cell state (group sizes, icon tiers)
  updates when the board changes and touches only affected cells — not a full rescan per frame.
- **Iterative, never recursive, traversals.** Flood fill / region search on content-sized data uses
  an explicit (reusable) `Stack<T>`/queue. `StackOverflowException` is uncatchable and kills the
  process, and thread stacks are small on mobile (assume 0.5–1 MB; iOS secondary threads default to
  512 KB) — a deep board is enough to reach it.

## 7. Tear down tweens before you unload

Before a scene unload / reload, **kill in-flight tweens** (`DOTween.KillAll()` or per-target kills) so
no tween callback fires against an object the unload has already destroyed — the classic "object has
been destroyed but you are still trying to access it" warning. Anything that outlives its target
(tweens, coroutines, event subscriptions) gets an explicit teardown; the composition root owning
lifetimes (`ARCHITECTURE.md` §6) is where that responsibility lives.

- **Bind a tween to its object's lifetime** — `SetLink(gameObject)` (or kill it in `OnDestroy`), so
  destroying the object kills the tween instead of leaving a callback aimed at a corpse. A tween,
  like an async continuation (`UNITY.md` §6), does not die with its target on its own.
- **Starting a tween is an event, not per-frame work.** A `DOMove` called from an update loop spawns
  a new racing tween every frame — start on the action, let the tween run.
- An `OnComplete(...)` lambda that captures locals is a closure allocation per tween (§8) — fine per
  action, wrong in a burst loop; pooled/recycled tweens (§3) keep the rest cheap.
- UI that must animate through a pause (`timeScale = 0`) runs the tween with `SetUpdate(true)`
  (unscaled time, `UNITY.md` §2).

## 8. Know what allocates — the C# mechanics

§4 says "low garbage"; this is the concrete checklist of what actually hits the managed heap on the
**Unity 6 baseline (C# 9.0 + a .NET Standard 2.1-era BCL)**. Worth internalising once — after that,
zero-alloc code is mostly habit, not effort.

**Allocates:**

- `new` on any **class** — including arrays (`new int[4]`), `List<T>`, `string`. A `new` **struct**
  does not (it lives on the stack or inline in its container).
- **Strings**: concatenation, `Substring`, `ToString()` — every one is a fresh string object. And
  **`$"..."` interpolation with value-type holes additionally BOXES each value** on this baseline
  (it lowers to `string.Format` with `object` args; the C# 10 handler optimisation needs both C# 10
  and a .NET 6+ BCL — Unity 6 has neither). Plain `+` concatenation with a value type does *not* box
  on current compilers, but still allocates the strings.
- **Method groups**: passing `Foo` where a delegate is expected creates a **new delegate per
  conversion, every time**. (C# 11 caches *static* method groups; Unity 6 is C# 9 — no caching, and
  instance method groups are never cached anyway.) Cache the delegate in a field.
- **Lambdas that capture**: capturing a *local* builds a closure class + a delegate; capturing only
  `this`/fields skips the closure class but still allocates a **delegate every time**. A lambda that
  captures *nothing* is cached by the compiler after the first call — and a `static` lambda (C# 9)
  turns accidental capture into a compile error.
- **LINQ** — delegates + enumerators on almost every operator. Banned on hot paths.
- **Boxing**: assigning a value type to `object` *or an interface* (`IComparable c = 5;`),
  `string.Format` with value-type args, and the sneaky one — **`foreach` over a collection typed
  as an interface**. `List<T>` has a struct enumerator that `foreach` uses for free, but reach the
  list through `IReadOnlyList<T>`/`IEnumerable<T>` and that enumerator is **boxed on every loop**.
- `params` methods (the compiler builds an array per call).

**Free:** `new` structs, `foreach` directly over an array or concretely-typed `List<T>`,
captureless/`static` lambdas after first use, and indexer access through `IReadOnlyList<T>` —
only the *enumerator* boxes; `list[i]` doesn't.

House rules that follow, for any per-action / per-frame path:

- **Iterate hot collections by index** (`for (int i = 0; i < list.Count; i++)`) or store them as a
  concrete type. Interface types are for the API surface, not the inner loop.
- **Shared preset data lives in `static readonly` fields, never expression-bodied properties.**
  `public static Palette Default => new(...)` re-allocates on *every access* — `=>` on a property
  is a method body, not a cached value. (The confusable that IS cached: `{ get; } = new(...)`, an
  auto-property initializer, evaluates once.)
- **Reuse scratch buffers.** A synchronous, single-threaded core can keep one member `List<T>`,
  `Clear()` it per call, and pre-size it once — instead of `new`-ing it per action.
- **Reseed, don't re-`new`, the RNG.** A sub-RNG derived per unit of work is a few bytes of state;
  expose `Reseed(seed)` and reuse a single instance.
- **Sorting**: `List.Sort(Comparison<T>)` wraps the delegate in a fresh comparer object per call on
  this BCL — implement `IComparable<T>` and use the parameterless `Sort()` (`Comparer<T>.Default`
  is a cached singleton), or pass a cached `IComparer<T>`.
- Compare distances with `sqrMagnitude`, not `Vector3.Distance`, when only the ordering matters.

## 9. Unity API allocation traps

Engine calls that allocate on every use, and their free counterparts:

| Allocates | Use instead |
|---|---|
| `go.tag == "X"` / reading `.name` (copies the string from native) | `go.CompareTag("X")`; Unity 2023.1+ adds `CompareTag(TagHandle)` for cached repeated checks |
| `GetComponents<T>()` (new array per call) | the `GetComponents(cachedList)` overload |
| `Physics.RaycastAll` | `Physics.RaycastNonAlloc` + prewarmed buffer — **2D note:** `Physics2D.*NonAlloc` is deprecated in Unity 6; use the `ContactFilter2D` + results-list overloads |
| `new WaitForSeconds(...)` inside a loop | cache one instance outside the loop |
| `Debug.Log("hp: " + hp)` on a frame path | log on events, not frames; strip logs from release |
| repeated `GetComponent<T>` lookups | cache in `Awake`; `TryGetComponent` for the miss case |

**String-keyed engine APIs are CPU traps even when they don't allocate:** `animator.SetFloat("Speed",
v)` pays the hash/lookup per call — cache `Animator.StringToHash` / `Shader.PropertyToID` ids once.
Mind the documented asymmetry: `StringToHash` ids are stable (CRC) and safe to persist;
`PropertyToID` ids are **per-run only** — never store or send them. `Invoke("Name", t)` and
`SendMessage` are string dispatch — orders of magnitude slower than a direct call in benchmarks;
banned on runtime paths.

`Instantiate`, `StartCoroutine`, and `AddListener`/`+=` also allocate — fine per *action*, wrong
per *frame* (a per-frame `+=` is a repeated-subscription bug besides). None of this replaces §4's
pooling rule; it narrows where the remaining allocations may live.

## 10. Unity's GC is Boehm — discipline beats tuning

Unity's runtime does not use the generational .NET GC. It uses **Boehm** (both Mono and IL2CPP):
non-generational and non-compacting — no Gen0 nursery making small short-lived garbage cheap, no
compaction undoing fragmentation. The managed heap **retains its expansion**: per the Unity 6 docs,
the expanded heap is kept "even if a large section of the heap is empty", and while empty *full
pages* are "eventually" released back to the OS on most platforms, the interval "isn't guaranteed
and is unreliable" — so **never budget on the heap shrinking**, and treat an allocation burst as a
permanent raise of the memory floor. **Incremental GC** (default on) slices collection across
frames, which softens the *spike* but does none of the *work* less — it even adds write-barrier
cost. Both properties point the same way: the fix is allocation discipline (§4, §8, §9), not GC
settings.

## 11. Measure first — on a device

The opening line says profile before optimising; concretely:

- **Profile on the device**, not in the editor. Editor numbers include editor overhead on a
  desktop CPU/GPU, a different GC/backend, and routinely invert which cost dominates.
- First decide **CPU-bound or GPU-bound** (Profiler timeline), then optimise only that side.
- GC: CPU module, **`GC.Alloc` column** (with allocation call stacks / Deep Profile) to find the
  allocating line. The goal state for an idle frame is a flat **0 B** — and for a full play session
  of actions, no steady heap growth (§16).
- **Frame Debugger** answers "why is this 40 draw calls / why did this batch break" step by step; a
  **Memory Profiler snapshot** answers "what is holding this 30 MB".
- **Mind the instrument.** A dev overlay or profiler hook that itself allocates puts a false floor
  under every reading — the instrument's own cost must be excluded from what it reports.
- Keep the §3 budgets deliberate: on mobile a capped `targetFrameRate` is also a **thermal and
  battery** decision — an uncapped game throttles itself into jank.
- **Claims need numbers.** "Optimised" means the before/after capture exists — a profiler screenshot
  or counter reading attached to the change, not an assertion.

## 12. Mobile rendering & memory budget (2D / URP)

The habits that matter for this class of game — a 2D URP scene driven by sprites and text:

- **Batching**: URP batches via the **SRP Batcher** (per shader *variant*, not per material — many
  materials on one shader are cheap). Keep sprites on the shared sprite shader and pack them into a
  **sprite atlas**; a stray material or un-atlased texture splits the batch, and a per-renderer
  `MaterialPropertyBlock` opts that renderer out of the SRP Batcher. Watch **SetPass calls**, not
  just draw calls. (2D `SpriteRenderer`s also flow through the sprite/dynamic batching path — the
  practical rule is the same: one atlas + one material ⇒ a handful of draws; the Frame Debugger
  names the exact reason whenever a batch breaks.)
- **Overdraw** is the 2D killer on mobile **tile-based GPUs** (effectively all of them): stacked
  full-screen transparent sprites shade every pixel they cover, again per layer. Keep backgrounds
  opaque where possible, use tight sprite meshes (not full-rect), and don't stack full-screen alpha
  fades. (**MSAA is the desktop-intuition exception**: tile-based GPUs resolve it on-tile, so 2x–4x
  is cheap — for a flat 2D game it's simply unnecessary, not costly.)
- **Texture memory**: compress with **ASTC** (Unity's recommended default for iOS and modern
  Android; ETC2 remains the fallback for old GLES3.0 devices — Google cites >80% ASTC coverage on
  Play, higher in practice on active devices as of 2026). A 2048² RGBA32 texture is ~16 MB *before*
  the ~33% mip overhead — budget textures up front, don't discover them in a crash report. Mipmaps
  **off** for UI and sprites rendered 1:1; **Read/Write Enabled off** (it keeps a CPU copy —
  doubles the memory).
- **IL2CPP + stripping**: iOS is IL2CPP-only, and Google Play requires ARM64. Code stripping
  removes "unused" types that serialization or reflection actually needed — remember `link.xml` /
  `[Preserve]` the day a type vanishes only in device builds. IL2CPP is also **AOT-only**: no
  `Reflection.Emit`/runtime codegen, and a generic instantiated only via reflection over a *value*
  type may never get compiled — another class of works-in-editor, dies-on-device. Keep
  reflection-driven serialization behind the one adapter and exercise it in a device build early.
- **URP settings that matter on mobile 2D**: SRP Batcher on; HDR off; MSAA off (see above); Depth
  Texture / Opaque Texture off unless a feature needs them (each is an extra copy + tile load);
  minimal or no post-processing on low-end; single camera. Unity 6 runs URP on **Render Graph** —
  leave it on (it culls unused passes and merges work); Compatibility Mode is only for unported
  custom render features.
- **Asset loading**: prefer direct references or Addressables over `Resources/` — `Resources`
  defeats stripping and memory accounting, and the same asset reached from two roots is resident
  twice (§14).
- Micro-hygiene: disable sensor sampling you don't use (legacy input: `Accelerometer Frequency`;
  new Input System: sensors are off until enabled) — a battery win, not a frame-time one.

## 13. Procedural meshes — rewrite in place

Runtime-built geometry (boards, sheets, overlays) follows the §4 lifetime rule: one persistent
`Mesh` per view, rewritten per rebake — never a `new Mesh` per level.

- **Count-limited writes over grow-only buffers.** Vertex/index/colour/uv arrays are fields sized to
  the largest content seen; each rebake writes the active slice with the
  `SetVertices/SetIndices/SetColors/SetUVs(buffer, start, length)` overloads so a smaller board
  never drags a stale tail into the mesh. `Mesh.Clear(keepVertexLayout: true)` first — shrinking the
  vertex range while the previous, larger index list still references it is rejected.
- **The bounds trap.** `calculateBounds: false` keeps the mesh's *existing* bounding box; after a
  `Clear` (or on a fresh mesh) that box is degenerate or stale, and the renderer gets silently
  **frustum-culled** — geometry that exists but never draws. Every rebake that skips recalculation
  must restore a bounds value explicitly. For meshes whose vertices move every frame (fold/flap
  animation, shader deformation), assign one **fixed, generous bounds** and skip per-frame
  recalculation entirely (the same idea the docs sanction via `Renderer.localBounds` for
  shader-deformed geometry).
- Meshes created with `new Mesh` are native objects the GC does not manage — each owner destroys its
  mesh in `OnDestroy` (`UNITY.md` §1: which may never fire for never-activated objects — create
  meshes lazily on first real use).

## 14. Addressables discipline

- **Implicit shared dependencies get duplicated.** An asset referenced by entries in two groups but
  not itself addressable is baked into **both** bundles — resident twice when both are loaded. Make
  shared art an explicit entry (its own shared group) so both bundles reference one copy. Audit:
  the *Check Duplicate Bundle Dependencies* Analyze rule (Addressables 1.x and again 2.3.1+ — the
  tool was removed in the 2.0–2.2 window) and, on 2.x, the **Addressables Build Report / build
  layout report**.
- **Every handle is released on every path.** `LoadAssetAsync` results are ref-counted: mirror each
  load with a `Release` (a `try/finally` around synchronous-style use), and remember memory only
  returns when the owning *bundle's* count hits zero. Handles kept deliberately for the app's
  lifetime are fine — but bounded, documented at their declaration, and never re-acquired per use.
- **Probe existence with `LoadResourceLocationsAsync`**, never with a full load — it resolves
  catalogue locations without loading the asset (its `Result` needs no release; the handle does).
- `WaitForCompletion` (sync) is supported with documented caveats: local content only (never a
  remote/undownloaded bundle), it can pump *other* in-flight operations to completion, scenes can't
  load synchronously, and it's unsupported on WebGL. On local bundles it is the sanctioned
  load-time-only stall that keeps loading ports synchronous (`ARCHITECTURE.md` §5) — remote content
  changes the answer: pre-download via `DownloadDependenciesAsync` in a dedicated loading state,
  then load from cache.

## 15. Canvas UI & TextMeshPro — when you do choose uGUI

The missing branch of §2: once a screen *is* canvas UI, these rules keep it flat.

- **Visibility = `Canvas` component toggle, not `SetActive`.** Disabling the Canvas stops drawing
  without discarding the batch or firing an `OnEnable`/`OnDisable` storm; reactivating a whole
  hierarchy forces a full rebuild. Honest caveat from Unity's own guidance: it's a pragmatic
  workaround — MonoBehaviours under a disabled canvas keep running, so their update loops must
  early-return on the canvas state (§5).
- **Split canvases by update frequency.** Static chrome and per-frame counters never share a
  canvas; a sub-canvas isolates its children's rebuilds from the parent.
- **No layout groups on runtime-updating UI.** Layout components are "relatively expensive"
  (Unity's words), nested ones multiply recomputation, and any child change re-runs the pass.
  Author fixed slots with anchors; re-centre a variable-count row by moving its **container**, not
  by letting a layout group reflow the children.
- **Placement helpers own the pivot.** Any `Place`/`Stretch`-style helper that assigns
  `anchorMin/Max` *and* `pivot` silently overwrites a custom pivot set before it — order matters
  (pivot after placement), and the trap deserves a comment at the helper.
- **Full-screen input gates are explicit objects**: an invisible raycast-target image (a "catcher")
  toggled with its lesson/modal — never a global "disable all input" flag scattered through
  handlers. Raycast reach is controlled at the `GraphicRaycaster`/`raycastTarget` level.
- **TMP auto-size is for unpredictable text only.** Auto Size re-lays the text "multiple times to
  find a good fit" (Unity's docs: "resource intensive… avoid auto-sizing dynamic text that changes
  frequently"). Bounded numeric labels (score, moves, cost) get a fixed size that fits their widest
  case; auto-size stays on genuinely variable strings.
- **Dynamic font atlases grow at runtime.** Each first-seen glyph rasterizes into the atlas
  (new pages under multi-atlas) — on the frame that first shows it. Warm the app's bounded charset
  once at boot (`TMP_FontAsset.TryAddCharacters`), enable *Clear Dynamic Data on Build*, and bake a
  **static** atlas once the text set (and localisation) stabilises — static atlases only cover the
  glyphs actually baked in.

## 16. Verifying memory — trends, not single numbers

- **Know what the top-line counter is.** "System Used Memory" is the **OS's view of the app** (the
  task-manager number), not Unity's tracked total. It *can* fall when pages are genuinely returned;
  it *tends to ratchet* because Unity retains allocator pools and Boehm releases pages unreliably
  (§10). A rising sys line during the first minutes of a run is normal warm-up (shader variants,
  render targets, bundle mmap, heap reserve) — expect a **plateau** for bounded content, and only
  investigate a line that keeps climbing.
- **Judge leaks by per-category trends**: managed (`GC.GetTotalMemory` — compare the
  **post-collection floors** of the sawtooth, not instantaneous values), texture, and mesh counters
  over a long session. These miss native/plugin leaks — which is why the **arbiter is the Memory
  Profiler package**: two snapshots minutes apart, diff, inspect what's *New* (Unity's documented
  leak workflow).
- **The leak audit checklist** — the five vectors that actually leak in a pooled, app-lifetime
  architecture: (1) Addressables handles not released on some path (§14); (2) runtime
  `UnityEngine.Object`s (meshes, materials, textures) without a single owner + `OnDestroy` (§13);
  (3) delegate/event subscriptions accumulating across rebinds (`ARCHITECTURE.md` §9, `UNITY.md`
  §5); (4) collections that only ever grow (anything keyed by level/run id is a red flag; pools
  bounded by content are fine); (5) `Update`-loop residue — clocks and pending actions on
  persistent components not reset by their per-level rebind.
