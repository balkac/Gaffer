# Performance Playbook — Unity

How I keep a Unity project smooth and low-garbage. Most of this is *structural* — the same layering
that makes the core testable (see [`ARCHITECTURE.md`](ARCHITECTURE.md)) also keeps the engine's
per-frame overhead low — plus a handful of concrete Unity habits. These are defaults, not universal
laws: profile before optimising (§11), and drop any of these where a measurement says it doesn't
matter. This doc is about *cost*; the engine's correctness-side semantics (lifecycle, time, object
lifetime) live in [`UNITY.md`](UNITY.md).

---

## 1. Light by construction

The cheapest work is the work you never ask the engine to do.

- **Few `MonoBehaviour`s.** Keep rules, flow and state as **plain C# objects** in the pure layers;
  let only the thin Presentation layer hold `MonoBehaviour`s. Fewer components means fewer
  engine-driven `Update` callbacks and less per-object bookkeeping — the *same* property that lets the
  core run headless. (It's a useful sanity metric: what fraction of your types are `MonoBehaviour`s?)
- **No `ParticleSystem` for simple, countable effects.** A pool of ordinary `SpriteRenderer`s driven
  by one tween library costs only the sprites it draws — no component and no `Update` *per particle*.
  Reach for `ParticleSystem` / VFX Graph when you genuinely need thousands of particles or GPU
  simulation, not for a burst of a dozen.

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
cheaper than a canvas; for a large, densely interactive, auto-laid-out UI, uGUI / UI Toolkit (with
canvases split by update frequency) is the right tool. Choose by the UI, not by reflex.

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

## 4. Low garbage — pool what you spawn at volume

Whatever you create in bursts (effects, projectiles, popups) should be **pooled**, not `new`/
`Destroy`-ed: an `ObjectPool<T>` **prewarmed** to a configured size and **capped** at a maximum;
`Get()` for a burst, `Release()` back. No allocation churn, so repeated effects leave no collectable
garbage.

Aim for **zero allocation on the idle path** — when nothing is happening, the update loops should
allocate nothing. Where allocation is unavoidable, keep it **off the per-frame path**: e.g. one
outcome record *per action* (not per frame), and a per-frame recompute (a drag preview) kept small and
only while it's actually needed. Recompute only when the input actually changes, not every frame,
where it's easy.

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

## 7. Tear down tweens before you unload

Before a scene unload / reload, **kill in-flight tweens** (`DOTween.KillAll()` or per-target kills) so
no tween callback fires against an object the unload has already destroyed — the classic "object has
been destroyed but you are still trying to access it" warning. Anything that outlives its target
(tweens, coroutines, event subscriptions) gets an explicit teardown; the composition root owning
lifetimes (`ARCHITECTURE.md` §6) is where that responsibility lives.

Per-tween discipline, beyond the scene-level kill:

- **Bind a tween to its object's lifetime** — `SetLink(gameObject)` (or kill it in `OnDestroy`), so
  destroying the object kills the tween instead of leaving a callback aimed at a corpse. A tween,
  like an async continuation (`UNITY.md` §6), does not die with its target on its own.
- **Starting a tween is an event, not per-frame work.** A `DOMove` called from an update loop spawns
  a new racing tween every frame — start on the action, let the tween run. (Per-frame `+=` has the
  same repeated-subscription shape, §9.)
- An `OnComplete(...)` lambda that captures locals is a closure allocation per tween (§8) — fine per
  action, wrong in a burst loop; pooled/recycled tweens (§3) keep the rest cheap.
- UI that must animate through a pause (`timeScale = 0`) runs the tween with `SetUpdate(true)`
  (unscaled time, `UNITY.md` §2).

## 8. Know what allocates — the C# mechanics

§4 says "low garbage"; this is the concrete checklist of what actually hits the managed heap.
Worth internalising once — after that, zero-alloc code is mostly habit, not effort.

**Allocates:**

- `new` on any **class** — including arrays (`new int[4]`), `List<T>`, `string`. A `new` **struct**
  does not (it lives on the stack or inline in its container).
- **Strings**: concatenation, `$"..."` interpolation, `Substring`, `ToString()` — every one is a
  fresh string object.
- **Lambdas that capture**: capturing a *local* builds a closure class + a delegate per call;
  capturing only `this`/fields skips the closure class but still allocates a **delegate every
  time**. A lambda that captures *nothing* is cached by the compiler after the first call — and a
  `static` lambda enforces that at compile time.
- **LINQ** — delegates + enumerators on almost every operator. Banned on hot paths (the pure core
  currently contains none — keep it that way).
- **Boxing**: assigning a value type to `object` *or an interface* (`IComparable c = 5;`),
  `string.Format` with value-type args, and the sneaky one — **`foreach` over a collection typed
  as an interface**. `List<T>` has a struct enumerator that `foreach` uses for free, but reach the
  list through `IReadOnlyList<T>`/`IEnumerable<T>` and that enumerator is **boxed on every loop**.
- `params` methods (the compiler builds an array per call).

**Free:** `new` structs, `foreach` directly over an array or concretely-typed `List<T>`,
captureless/`static` lambdas after first use, and indexer access through `IReadOnlyList<T>` —
only the *enumerator* boxes; `list[i]` doesn't.

House rules that follow, for any per-match / per-week / per-frame path:

- **Iterate hot collections by index** (`for (int i = 0; i < list.Count; i++)`) or store them as a
  concrete type. Interface types are for the API surface, not the inner loop.
- **Shared preset data lives in `static readonly` fields, never expression-bodied properties.**
  `public static Formation F442 => new(...)` re-allocates on *every access* — `=>` on a property
  is a method body, not a cached value.
- **Reuse scratch buffers.** A synchronous, single-threaded core can keep one member `List<T>`,
  `Clear()` it per call, and pre-size it once — instead of `new`-ing it per match.
- **Reseed, don't re-`new`, the RNG.** A sub-RNG derived per fixture/player is one `ulong` of
  state; expose `Reseed(seed)` and reuse a single instance.
- **Sorting**: pass a cached `IComparer<T>` singleton — `List.Sort(Comparison<T>)` wraps the
  delegate in a fresh comparer object per call on Mono.
- Compare distances with `sqrMagnitude`, not `Vector3.Distance`, when only the ordering matters.

## 9. Unity API allocation traps

Engine calls that allocate on every use, and their free counterparts:

| Allocates | Use instead |
|---|---|
| `go.tag == "X"` (copies the string from native) | `go.CompareTag("X")` |
| `GetComponents<T>()` (new array per call) | the `GetComponents(cachedList)` overload |
| `Physics.RaycastAll` | `Physics.RaycastNonAlloc` + prewarmed buffer |
| `new WaitForSeconds(...)` inside a loop | cache one instance outside the loop |
| `Debug.Log("hp: " + hp)` on a frame path | log on events, not frames; strip logs from release |
| repeated `GetComponent<T>` lookups | cache in `Awake`; `TryGetComponent` for the miss case |

`Instantiate`, `StartCoroutine`, and `AddListener`/`+=` also allocate — fine per *action*, wrong
per *frame* (a per-frame `+=` is a repeated-subscription bug besides). None of this replaces §4's
pooling rule; it narrows where the remaining allocations may live.

## 10. Unity's GC is Boehm — discipline beats tuning

Unity's runtime does not use the generational .NET GC. It uses **Boehm**: non-generational and
non-compacting — no Gen0 nursery making small short-lived garbage cheap, no compaction undoing
fragmentation, and a managed heap that **grows but rarely shrinks back** to the OS. On a phone,
that expanded heap stays claimed until the app dies. **Incremental GC** slices collection across
frames, which softens the *spike* but does none of the *work* less — total GC cost still scales
with how much you allocate. Both properties point the same way: the fix is allocation discipline
(§4, §8, §9), not GC settings.

## 11. Measure first — on a device

The opening line says profile before optimising; concretely:

- **Profile on the device**, not in the editor. Editor numbers include editor overhead on a
  desktop CPU/GPU and routinely invert which cost dominates.
- First decide **CPU-bound or GPU-bound** (Profiler timeline), then optimise only that side.
- GC: CPU module, **`GC.Alloc` column** (with allocation call stacks / Deep Profile) to find the
  allocating line. The goal state for an idle frame is a flat **0 B**.
- **Frame Debugger** answers "why is this 40 draw calls" step by step; a **Memory Profiler**
  snapshot answers "what is holding this 30 MB".
- Keep the §3 budgets deliberate: on mobile a capped `targetFrameRate` is also a **thermal and
  battery** decision — an uncapped game throttles itself into jank.

## 12. Mobile rendering & memory budget (2D / URP)

The habits that matter for this class of game — a 2D URP scene driven by sprites and text:

- **Batching**: URP batches via the **SRP Batcher** (per shader variant, not per material). Keep
  sprites on the shared sprite shader and pack them into a **sprite atlas**; a stray material or
  un-atlased texture splits the batch. Watch **SetPass calls**, not just draw calls.
- **Overdraw** is the 2D killer on mobile **tile-based GPUs**: stacked full-screen transparent
  sprites shade every pixel they cover, again per layer. Keep backgrounds opaque where possible,
  use tight sprite meshes (not full-rect), and don't stack full-screen alpha fades.
- **Texture memory**: compress with **ASTC**; a 2048² RGBA32 texture is ~16 MB *before* mips —
  budget textures up front, don't discover them in a crash report. Mipmaps **off** for UI and
  sprites rendered 1:1.
- **IL2CPP + stripping**: iOS is IL2CPP-only; code stripping removes "unused" types that
  serialization or reflection actually needed — remember `link.xml` the day a type vanishes only
  in device builds. IL2CPP is also **AOT-only**: no `Reflection.Emit`/runtime codegen, and a
  generic instantiated only via reflection over a *value* type may never get compiled — another
  class of works-in-editor, dies-on-device. Keep reflection-driven serialization behind the one
  adapter and exercise it in a device build early.
- **Asset loading**: prefer direct references or Addressables over `Resources/` — `Resources`
  defeats stripping and memory accounting, and the same asset reached from two roots is resident
  twice.
