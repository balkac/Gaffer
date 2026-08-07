# Performance Playbook — Unity

How I keep a Unity project smooth and low-garbage. Most of this is *structural* — the same layering
that makes the core testable (see [`ARCHITECTURE.md`](ARCHITECTURE.md)) also keeps the engine's
per-frame overhead low — plus a handful of concrete Unity habits. These are defaults, not universal
laws: profile before optimising (§11), and drop any of these where a measurement says it doesn't
matter. The engine's correctness-side semantics (lifecycle, time, object lifetime) live in
[`UNITY.md`](UNITY.md).

> **Baseline:** Unity 6 (6000.x) · C# 9.0 · URP. Several rules below are version-tagged; in
> particular, do **not** import allocation folklore from modern .NET (C# 11 method-group caching,
> C# 10 interpolation handlers) — none of it is active on this baseline (§8), and BCL allocation
> claims are verified against this Unity version's shipped IL, not against web lore.
> Verified: Unity 6000.3.20f1 (mscorlib byte-identical to 6000.3.16f1, the IL-inspected build) ·
> Addressables 2.11.1 · last reviewed 2026-08-06.
> **2026-08-06 audit:** §3's frame-rate and budget figures, §14's `WaitForCompletion` semantics and
> §10's heap-retention wording were re-checked against Unity 6000.3 / Addressables docs and hold
> verbatim (§14's quote refreshed to the 2.11 wording). A same-day verification against the live
> pages then reversed two of this audit's own corrections and refreshed a third: §9 —
> `Physics2D.*NonAlloc` **is** deprecated in 6000.x (the "will be deprecated" note was 2022.3
> wording; the 6000.x pages are gone and the runtime marks the methods obsolete); §15 — the
> auto-size cost quotation **is** current (the Unity 6 TMP manual carries it verbatim; restored
> with its source); §12 — ASTC/ETC2 coverage updated to the live page's >80% / >95%. §2's
> hit-test guidance was measured and rewritten — the previous snippet could not work as written.
> Some sections rest on **project measurement rather than documentation**; each states its
> platform and build in place. Those figures are evidence for a mechanism — never portable
> constants, and never Unity's numbers.

---

## 1. Light by construction

The cheapest work is the work you never ask the engine to do.

- **Few `MonoBehaviour`s.** Keep rules, flow and state as **plain C# objects** in the pure layers;
  let only the thin Presentation layer hold `MonoBehaviour`s. Fewer components means fewer
  engine-driven `Update` callbacks and less per-object bookkeeping — the *same* property that lets the
  core run headless. The mechanism is real, not folklore: every engine callback crosses the
  native→managed interop boundary even when the body is trivial (Unity's own "10000 Update() calls"
  measurement; the Unity 6 manual recommends a custom update manager for exactly this reason). One
  manager iterating a plain list beats N engine callbacks — **at scale**: ordinary `Update` methods
  are fine for a small, stable population; introduce a centralised update manager when callback
  count, ordering needs, subscription churn, or profiler evidence justifies its added routing and
  ownership complexity, not on principle.
- **Sprites vs `ParticleSystem` is a control decision, not a cost law.** A `ParticleSystem`
  simulates all its particles native-side in one component and draws them batched — for many
  stochastic particles it is usually the *cheaper* path, not the expensive one. A pool of
  `SpriteRenderer`s driven by one clocked driver earns its place where the effect is **few,
  countable, and choreographed** (authored trajectories, exact landings — a coin flying to the
  wallet, a star to the HUD); that is a *control* argument, and any "cheaper" claim belongs to a
  device measurement, not to this doc. Whichever is chosen, the mobile budget applies per prefab:
  pooled + prewarmed, bounded `maxParticles`, one shared atlas material, collision/lights/trails
  off, and a `Play()`/`Emit()` path measured to allocate nothing. **§4a is the operational half** —
  the pooling, lifetime and atlas mechanics that decide whether that budget survives contact.

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
  just that one text mesh; there's no canvas to dirty and no rebuild cascade. *Expect a profiler
  marker that contradicts this and doesn't:*
  `Canvas.SendWillRenderCanvases → TMP_UpdateManager.DoRebuilds` appears even in a scene with
  **zero** `Canvas` components, because TMP's update
  manager subscribes to that engine event as its per-frame hook and world-space `TextMeshPro` uses
  the same manager. The player-loop stage runs in every Unity app; the cost beneath it is TMP's own
  mesh work, not a canvas. Grep the scene for `Canvas` before believing the marker.
- **No graphic raycaster / `EventSystem`** — hit-test buttons against the pointer you already read
  for other input. No extra raycast pass, no per-graphic `CanvasRenderer`. **Two traps, both
  measured on this baseline:** (1) `Camera.ScreenToWorldPoint` given a `Vector2` (or any input with
  `z = 0`) returns a point at the **camera's own depth**, not at your content plane — with the
  camera at `z = -10` it returns `z = -10` while the sprites sit at `z = 0`; (2) a
  `SpriteRenderer`'s `bounds` is **not flat** — a unit sprite measured `size = (1, 1, 0.2)`, so the
  box tolerates ±0.1 of depth error and nothing more. Combine the two and
  `renderer.bounds.Contains(pointerWorld)` is `false` on every press in any ordinary 2D setup —
  silently, with no error: the button simply never responds. (It would only pass if the camera
  sat within 0.1 units of the sprite plane, which no real 2D camera does.) Either pass the
  camera-to-plane distance as the `z` argument to `ScreenToWorldPoint`, or — preferred — test
  **x/y only against an authored hit size**:

  ```csharp
  Vector3 localPoint = transform.InverseTransformPoint(pointerWorld);

  return Mathf.Abs(localPoint.x) <= _hitSize.x / 2f
      && Mathf.Abs(localPoint.y) <= _hitSize.y / 2f;
  ```

  The authored size is preferred because it decouples the touchable area from art bounds — which
  is also what `GAME-FEEL.md` §4 asks for (generous hit targets), and it sidesteps the phantom
  0.2 depth entirely. Grid content is a different problem with a better answer: pointer → cell by
  coordinate arithmetic, no bounds test at all (`GAME-FEEL.md` §4).
- **One render path** — text and sprites go through the camera exactly like the rest of the scene.

The trade-off is real and bounds the advice: world-space UI gives up everything uGUI automates —
anchoring/layout, `Screen.safeArea` handling, localisation-driven reflow, accessibility/navigation
semantics — so each view positions itself from the camera's `orthographicSize`/`aspect` and
re-anchors on a guarded `LateUpdate` check watching **both aspect and `Screen.safeArea`** (safe
area can change with no aspect change — rotation, foldables, split-screen multitasking). For a
small fixed HUD that's simpler and cheaper than a canvas; for a large, densely interactive or
localised UI, uGUI / UI Toolkit is the right tool. Choose by the UI, not by reflex — and when you
do choose the canvas, **§15 is that branch's rulebook**.

## 3. Stable runtime — budgets from a config asset

Set frame-rate and animation budgets **once, at boot**, from a `PerformanceSettings`
`ScriptableObject` so they're tuned without touching code:

```csharp
Application.targetFrameRate = TargetFrameRate;   // e.g. 60
QualitySettings.vSyncCount   = VSyncCount;

// Only if the project uses DOTween. A tween library is a choice, not part of this boot step —
// GAME-FEEL.md §3 prefers a clocked driver for hot, retargetable, N-element motion, and a
// project with no tween library simply omits these two lines.
DOTween.defaultRecyclable    = true;             // reuse tween objects instead of re-allocating
DOTween.SetTweensCapacity(TweenCapacity, SequenceCapacity);
```

Where a tween library *is* used, pre-sizing its tween/sequence pools means the animation system
never grows its internal arrays mid-game (which would spike a frame), and `defaultRecyclable` lets
it reuse completed tweens.

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
  mid-session — measured on device as a one-time multi-hundred-KB hit. Prewarm-to-max presumes the
  maximum is *small* (a puzzle board, not an open world): the target trades a mid-session spike for
  boot time and permanent RAM, so it must fit the boot and memory budgets — for a large catalogue,
  prewarm to a representative peak instead and accept measured, controlled growth.

**Rebake in place; don't rebuild.** Level/round-scoped visuals (board meshes, decks, obstacle
overlays, tutorial dressing) are **app-lifetime components that re-bake in place** for each level —
grow-only buffers, count-limited mesh writes (§13) — never a `Destroy`-everything +
recreate-everything cycle. The Destroy/rebuild pattern was measured at **170–250 KB of GC per level
transition** (measured case: a small 2D puzzle on this baseline, low-end Android device build —
evidence for the mechanism, not a portable constant), and because `Destroy` is deferred to end of
frame, the old and new copies coexist for a frame and the *peak* doubles. A rebake transition
allocates nothing once buffer capacities cover the largest board. (Per-level components that
must exist get a `Configure(...)`-style rebind that
explicitly resets every clock, pending action, and stale handle — see `UNITY.md` §5 and
`ARCHITECTURE.md` §6 for the ownership side.)

**Name the path a guarantee covers.** "Zero allocation" with no named path is marketing; "zero
allocation on the steady-state path — the per-frame and per-action path, guarded by a test" is a
claim someone can check and you can defend. It also makes the deliberate exceptions statable
instead of embarrassing: a debug panel or configurator that rebuilds a validated model on every
refresh is neither per-frame nor per-action, and paying a few hundred bytes there can be the right
call when the alternatives — reusing one live instance, collapsing types to structs, validating
without building — each trade a real ownership or correctness property for the allocation. Write
down which alternatives were rejected and why, next to the guarantee.

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
- **Runtime IMGUI (`OnGUI`) is a measured budget item, not a blanket ban — but a naive overlay does
  poison the numbers it reports.** Measured on this baseline (empty scene, development player, Mono
  and IL2CPP agree; `com.balkac.perfhud` 0.3.0 `Measure~/` harness): a straightforward IMGUI HUD
  costs **~0.7–3 KB/frame** of GC — the layout pump (~370 B/frame even with zero `GUILayout` calls)
  plus a `GUIContent.Temp` copy of every string passed to `GUI.Label`/`GUI.Button` — *not* the
  "tens of KB per frame" folklore, which player measurements do not reproduce. The discipline:
  `useGUILayout = false`, draw through cached `GUIContent` instances, cache value-strings and
  rebuild text only when a displayed value changed. The floor after that lives in the native draw
  bindings and is text-proportional (~140–160 B/frame for a two-line badge, ~630 B/frame for an
  8-line panel), plus a text re-mesh spike on the frame a label actually changes
  (`GUIStyle.GetMeshInfo`, ~24 KB for a panel of text). Whatever remains, **document it so readers
  can subtract it, and give the overlay a hidden state that draws nothing (measured 0 B/frame) for
  capture windows**. A code-built uGUI overlay stays a fine alternative where uGUI is already in
  the project — a preference, not a performance necessity.

**Schedule the writes, too.** Disk writes are frame cost even when the bytes are small: never save
on a frame that is already opening UI or playing a landing beat; **coalesce** multiple state changes
of one flow moment (reward + stars + progress pointer) into a single write; and reuse the serializer
machinery (cached serializer instance + `StringBuilder` + `StreamWriter.Write(StringBuilder)`)
instead of rebuilding it per save. Atomicity and platform rules for the save file live in
`UNITY.md` §7.

## 4a. Particle effects — ownership, pooling, and the atlas

§1 says a `ParticleSystem` is usually the cheaper path and names the per-prefab budget. This is
the operational half: the mechanics that decide whether that budget actually holds. Everything
here was measured on a 2D mobile project (Unity 6000.3, URP, ASTC/ETC2 targets); the numbers are
evidence for the mechanism, not portable constants.

- **Author the effect, don't hand-roll it.** Where the engine has a subsystem for the effect, one
  authored prefab beats bespoke components: the tuning surface is the Inspector, an artist can
  touch it, and the simulation runs native-side. Hand-written spark/flash `MonoBehaviour`s were
  replaced by a single authored prefab here and nothing was lost but code.
- **Pool the view, not the system, and cache the array once.** The pooled unit is a component
  holding `GetComponentsInChildren<ParticleSystem>(true)`, resolved on **first** lease and kept —
  which also means the first `Get()` of a pooled effect allocates and every later one does not, so
  an allocation measurement must run **warm** (and after the pool has cycled once, so the release
  path is measured too).
- **Runtime modulation must compose with the authored value, not overwrite it.** A pooled object
  carries its previous configuration into its next use. Where a caller adds a runtime offset — a
  ripple delay staggered per cell — store the **authored** value at lease time and add to it; the
  naive version writes the total back into the prefab's field and the second play inherits the
  first play's delay. Same family: clear emitted particles on release
  (`StopEmittingAndClear`), or the returned object shows the previous burst's remains.
- **Let the engine end the effect; don't poll for "is it done".** Reclaiming lazily at the next
  lease costs zero per-frame code but leaves finished effects active in the hierarchy until the
  next one is asked for. The fix belongs in the **prefab, not the code**: the lead system's
  `Stop Action: Disable` hides a finished effect by itself. Prefer an engine-owned lifetime signal
  over a code-owned poll (§5).
- **If "is it playing" reads one system, that system must outlive its children — write the
  invariant down.** Querying `IsAlive` on the first system is cheap and correct only while it is
  the longest-lived; a polish pass that stretches an accent past the lead cuts the effect short,
  silently. Measured here: lead 1.08 s against children at 0.80 / 0.35 (blast), 1.15 against
  0.82 / 0.35 (debris). The array's first element is carrying a contract — say so next to the
  prefab.
- **Size the pools by sweeping the shipped content, not by guessing.** A test that plays every
  shipped level N times and records the largest simultaneous demand the content can *offer* (not
  merely what a bot took) turns pool size into a regression gate: it fails when authored content
  outgrows the pool, which is the case that would otherwise instantiate mid-action. Measured: a
  hand-guessed pool of 24 against a real maximum of 23 — one spare, by luck — beside a pool of 8
  against a real maximum of 2, whose correction removed 12 particle systems from boot.
- **Prewarm is a boot cost, not a frame cost** — 24 copies × 3 systems measured 2.7 ms in the
  editor. It is paid against the boot budget under §4's prewarm-to-max rule, not the frame budget.
- **FX sprites belong in the atlas — with two importer settings that sprite-mode particles
  require.** The instinct to keep FX out ("a particle system resolves an atlased sprite's page at
  runtime, so measure first") is a good instinct that measurement overturned here: seven FX
  sprites shipped unatlased **and uncompressed** (~2.5 MB RGBA32) made a single blast bind a
  different texture per system, and atlasing collapsed every gameplay particle into **one draw
  call** at 0.45 MB resident (1024², ASTC 6×6). The two settings that must carry over from the
  main sprite atlas are `enableRotation: 0` and `enableTightPacking: 0` — sprite-mode particles
  break without them.
- **Batching is a budget, not a goal — name the frame where it is enforced.** Interleaved sorting
  orders split the batch, and that is sometimes the intended art (confetti in front of a panel,
  a glow behind a star). A frozen celebration screen is not where the frame-rate gate lives;
  spend the batching discipline on the frames that are actually contended.

## 5. Safe update loops

`Update` methods do the least work possible and never busy-spin.

- **Early-return** immediately when there's nothing to do — an input handler with no pointer event
  this frame, an animator that has reached its target — so a still frame costs almost nothing.
- **Step toward a target and stop** (`Mathf.MoveTowards`) rather than running an easing update forever.
- **Don't poll game state each frame.** State changes in response to an action; the view animates from
  that action's outcome (§8 of `ARCHITECTURE.md`), not by diffing the world every frame.
- **A new clocked hook names what forces it to be per-frame.** An `Update`/`LateUpdate`/coroutine
  is a standing per-frame cost; the justification ("input must be sampled every frame", "N
  elements animate toward targets") is written where the hook is added. If an existing flow point
  already gives the guarantee — an action outcome, an engine lifetime signal (§4a) — hook there
  instead of adding a clock.

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
- **The cost of a query is the contract of the API you asked it through.** Reusing a production
  analyser to answer a narrower question buys correctness and pays its full contract every call.
  Measured instance: a "does any adjacent pair match?" check was answered by a full
  connected-component labelling — flood fill, group records, tier bookkeeping — because that
  analyser already existed. Fusing the check into the fill that was already running and stopping
  at the first match made it **106× faster** (40.23 → 0.38 ms at the largest board size, measured
  headless on .NET 8 — an algorithmic ratio, not a device figure) — with *half* of that gain
  coming from finding neighbours by index arithmetic (`i - 1`, `i - columnCount` in a row-major
  scan) instead of routing through a coordinate type, a bounds check and an indexer. The lesson
  is not "write a faster analyser": it is that a wide contract
  invoked for a narrow question is a cost with no symptom until you profile it.

## 7. Tear down tweens before you unload

*Applies only where a tween library is in use — see §3; a project driving its motion from a
clocked player class (`GAME-FEEL.md` §3) has no tween lifetimes to manage, but owes the same
teardown for its own drivers.*

Before a scene unload / reload, **kill in-flight tweens — per-target and lifetime-linked by
default** (`SetLink`, target kills, a scoped id); reserve `DOTween.KillAll()` for whole-app
teardown, since in an app-lifetime architecture it also kills persistent tweens that were never
meant to die with the scene. The point either way: no tween callback may fire against an object the
unload has already destroyed — Unity's runtime error, "object has been destroyed but you are
still trying to access it". Anything that outlives its target (tweens, coroutines, event
subscriptions) gets an explicit teardown; the composition root owning
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
- **Sorting — verified against this Unity version's shipped IL (Editor Mono, Android AOT, iOS AOT
  alike):** the one allocation-free overload is `List<T>.Sort(Comparison<T>)` with a **cached**
  delegate — the comparison is passed raw through the sort, no wrapper object. The intuitive
  alternatives are the trap: parameterless `Sort()` and `Sort(IComparer<T>)` allocate a
  `Comparison<T>` delegate on **every call** (a `comparer.Compare` method-group conversion inside
  `ArraySortHelper`), even for a struct implementing `IComparable<T>` — the CoreFX specialization
  that would avoid it ships in the assembly but is never selected. `Array.Sort(array,
  Comparison<T>)` still allocates a wrapper comparer on this BCL, too. Rule: hot-path sorting goes
  through one `static readonly Comparison<T>` field and `List.Sort(Comparison<T>)`. (Evidence
  level: shipped-IL inspection — confirm with the Profiler on the target backend before quoting it
  as a device-measured number.)
- Compare distances with `sqrMagnitude`, not `Vector3.Distance`, when only the ordering matters.

## 9. Unity API allocation traps

Engine calls that allocate on every use, and their free counterparts:

| Allocates | Use instead |
|---|---|
| `go.tag == "X"` / reading `.name` (copies the string from native) | `go.CompareTag("X")`; Unity 2023.2+ adds `CompareTag(TagHandle)` for cached repeated checks |
| `GetComponents<T>()` (new array per call) | the `GetComponents(cachedList)` overload |
| `Physics.RaycastAll` | `Physics.RaycastNonAlloc` + prewarmed buffer — **2D note:** `Physics2D.*NonAlloc` is deprecated in Unity 6 (the 6000.x scripting reference no longer carries their pages, and the runtime's obsolete messages read "has been deprecated. Please use Raycast/CircleCast/…"); use the `ContactFilter2D` + results array/list overloads, which avoid the allocation when the results list needs no resize |
| `new WaitForSeconds(...)` inside a loop | cache one instance outside the loop |
| `Debug.Log("hp: " + hp)` on a frame path | log on events, not frames; strip logs from release |
| repeated `GetComponent<T>` lookups | cache in `Awake`; `TryGetComponent` for the miss case |

**String-keyed engine APIs are CPU traps even when they don't allocate:** `animator.SetFloat("Speed",
v)` pays the hash/lookup per call — cache `Animator.StringToHash` / `Shader.PropertyToID` ids once,
in `static readonly` fields. Treat **both** as runtime-only values: `PropertyToID` is documented as
per-session, and `StringToHash`'s stability is an *undocumented implementation detail*, not a
persistence contract — neither goes into save data, content JSON, analytics, or network payloads;
persist the source string or a project-owned stable ID instead (the general rule for any
engine-generated id without a documented persistence guarantee). `Invoke("Name", t)` and
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
- **Mind the instrument — and take it out when the measuring is done.** A dev overlay or profiler
  hook that itself allocates puts a false floor under every reading, so its own cost must be
  excluded from what it reports. The lifecycle rule is the other half: once the numbers are
  written down, **the evidence stays and the apparatus goes.** A read-out kept past its purpose
  accumulates its own maintenance — a flag that must ship off, a test whose only job is to catch
  you forgetting to switch it back — and a guard protecting a setting nobody needs is cheaper to
  delete than to keep. The sharp version, learned the hard way: if the most expensive thing in
  your profile is a statistics read-out, that read-out is an argument against the case it exists
  to make. The insight belongs in the document, not in the running game.
- **Attribute allocation by owner, not by total.** A capture's headline number answers the wrong
  question. Measured device run (1441 frames, IL2CPP): 44,062 B allocated across 9 frames — and
  none of it written by the project. Broken down: the input system reading touch events (~32 KB,
  one frame per tap), TMP's first-render buffer growth (~6 KB), an `Instantiate` (~5 KB), and
  Addressables' permanent `LateUpdate` hook (424 B in a single frame). The honest claim is not
  "0 B" — it is **"0 B from our code on the action path, with every remaining byte named, bounded
  and third-party"**, which is both stronger and checkable.
  Two habits make that claim cheap to produce: **classify samples into buckets** (game / engine /
  editor / runtime) rather than reading method names, and **produce the reading twice with
  different code** — a throwaway analyser and a committed one agreeing on 1441 frames, a 16.67 ms
  median and the same two outlier frames is what turns a number into evidence. Read a deep
  capture's absolute times as a ceiling, not a figure: deep profiling inflates them, so what the
  run establishes is *who*, not *how much*.
- **A zero-allocation claim needs a guard test in the suite, and three false positives will fool
  it first.** All three were observed here before the measurement was trustworthy: the measured
  block included a helper that allocates an array (`GetComponentsInChildren`); the test's pools
  were smaller than production, so the pool grew mid-measurement; and EditMode never simulates
  particles, so pooled effects never reported themselves finished and the pool drained forever —
  which read as thousands of bytes per action of pure artefact (`UNITY.md` §9). Assert the
  constraint **both while warm and again after the run**, so a leak that only appears with play
  time fails too.
- Keep the §3 budgets deliberate: on mobile a capped `targetFrameRate` is also a **thermal and
  battery** decision — an uncapped game throttles itself into jank.
- **Claims need numbers.** "Optimised" means the before/after capture exists — a profiler screenshot
  or counter reading attached to the change, not an assertion.

## 12. Mobile rendering & memory budget (2D / URP)

The habits that matter for this class of game — a 2D URP scene driven by sprites and text:

- **Batching — two different mechanisms, don't conflate them.** The **SRP Batcher** cuts the *CPU
  setup cost per draw* (material data persists on the GPU; compatibility is per shader *variant*,
  not per material — many materials on one shader are cheap); it does **not** merge draw calls.
  Actual draw *merging* for 2D comes from the sprite/dynamic-batching path — one atlas + one
  shared material ⇒ a handful of draws. Keep sprites on the shared sprite shader and pack them
  into a **sprite atlas**; a stray material or un-atlased texture splits the batch, and a
  per-renderer `MaterialPropertyBlock` opts that renderer out of the SRP Batcher. Watch **SetPass
  calls**, not just draw calls, and let the **Frame Debugger** name which path a renderer actually
  took and the exact reason whenever a batch breaks.
  **The consequence for a world-space HUD (§2): your label count is your draw count.** Text does
  not merge — a `TextMeshPro` is a plain `MeshRenderer`, only classic dynamic batching could merge
  it — off by default in URP, bounded by its documented limit (at most 300 vertices / 900 vertex
  attributes per mesh), which at TMP's 4 vertices per glyph puts the ceiling around 75 characters
  per label before the attribute cap lowers it, and a feature Unity 6's docs now mark "no longer
  recommended" besides — and the SRP Batcher never merges draws at all. Sprites *do* merge
  through the 2D renderer's own path. Measured on this baseline: a board scene drew 11 (6 sprite
  batches + 5 text meshes), and opening a debug panel of
  ~43 labels took it to 49 — while SetPass stayed at 3–4, which is the number that would actually
  hurt. Budget text objects deliberately; don't be surprised by them.
- **Overdraw** is the 2D killer on mobile **tile-based GPUs** (the dominant mobile GPU
  architecture): stacked full-screen transparent sprites shade every pixel they cover, again per
  layer. Keep backgrounds opaque where possible, use tight sprite meshes (not full-rect), and don't
  stack full-screen alpha fades. (**MSAA is the desktop-intuition exception**: tile-based GPUs
  resolve it on-tile, so 2x–4x is *typically* cheap — bandwidth and store costs still exist, so
  confirm on a representative device; for a flat 2D game it's simply unnecessary either way.)
- **Texture memory**: compress with **ASTC** (Unity's recommended format for iOS — A8 and newer
  — and the preferred format on modern Android; ETC2 remains the fallback for older devices). The
  sourced figures are Google's texture-compression-format-targeting page (read 2026-08): **ASTC
  on >80% of active Android devices, ETC2 on >95%** — the page calls ETC2's reach "nearly all
  active Android mobile devices" — with ASTC as the primary format and ETC2/ETC1 as the targeted
  fallback. *(Coverage figures drift; re-read the page before quoting them in a deliverable.)*
  A 2048² RGBA32 texture is ~16 MB *before* the ~33% mip overhead — budget textures up front,
  don't discover them in a crash report. Mipmaps
  **off** for UI and sprites rendered 1:1; **Read/Write Enabled off** (it keeps a CPU copy —
  roughly doubles the memory).
- **IL2CPP + stripping**: iOS is IL2CPP-only, and Google Play requires ARM64. Code stripping
  removes "unused" types that serialization or reflection actually needed — remember `link.xml` /
  `[Preserve]` the day a type vanishes only in device builds, and read `UNITY.md` §9 for how to
  *diagnose* it, because the two artefacts you would naturally check both report the code as
  present. IL2CPP is also **AOT-only**: no `Reflection.Emit`/runtime codegen, and a generic
  instantiated only via reflection over a *value*
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
  catalogue locations without loading the asset, and is documented to never fail (unresolvable
  keys return an empty list); release the handle as usual.
- **`WaitForCompletion` is a constrained mode of an async-shaped API — know its documented costs
  before choosing it.** They are heavier than "it blocks", and the full list of conditions is
  **`ARCHITECTURE.md` §5a**, which owns the decision; that checklist is the canonical copy and
  this section does not keep a second, partial one. The cost mechanics that belong here: the call
  completes **all currently active asset load operations** — the 2.11 docs' wording is "All
  active asset load operations are completed when WaitForCompletion is called on any asset load
  operation" — and **local ≠ free**: a local load still pays disk access, bundle decompression,
  dependency resolution and asset deserialization on the main thread. Those two mechanics are why
  §5a's items 5 and 6 exist; the rules themselves live there, and the remote path is
  `ARCHITECTURE.md` §5's split-download-from-load bullet.

  **The `Awake` guardrail (`ARCHITECTURE.md` §5a item 2) is a deadlock guard, and the editor
  cannot show you the failure.** The docs describe the hazard as "can block the main thread";
  measured on this baseline it was stronger: a **synchronous** content load issued from `Awake`
  in the first scene re-entered the engine's own wait for that scene — the semaphore never
  signalled, no frame was ever drawn, and iOS killed the app with a `0x8BADF00D` watchdog
  termination after 25 s. **Play mode never reproduces it** — the editor resolves Addressables
  through the asset database with no bundles involved, so the entire class of bundle-completion
  hazards is absent from every editor run. Treat "it works in play mode" as no evidence at all
  here. The remedy that shipped went past the docs' `Start` advice: the load became async
  behind a boot splash, so the wait is a *designed* loading moment with a rendered frame behind
  it, rather than a hidden stall in the first frames.
  *(Evidence: one project, iOS IL2CPP device build, Addressables 2.11.1. The mechanism is
  documented; the watchdog outcome is our measurement, not a documented guarantee.)*

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
- **Auto-fitting text is for unpredictable text only.** TMP's own manual states the cost: with
  Auto Size on, "TextMesh Pro lays out the text multiple times to find a good fit. This is a
  resource intensive process, so avoid auto-sizing dynamic text that changes frequently" (the
  Unity 6 TMP manual, in com.unity.ugui 2.0) — and the fit re-runs whenever the string or the box
  changes, which is exactly the case for a per-frame counter. Bounded numeric labels (score,
  moves, cost) get a fixed size that fits their widest case; a fit search stays on genuinely
  variable strings. uGUI's **Best Fit** is the same trap with its own source: Unity's UI
  optimisation guide names it a hotspot (`Text_OnPopulateMesh` dominates when it is on) and says
  it "should never be used".
- **TMP's per-label buffer growth is bounded, not a leak — recognise it before you build a fix.**
  Each label grows its internal arrays (character info, mesh vertex buffers, line info) the first
  time it renders text longer than it ever has, then goes permanently quiet: growth
  block-allocates to the next power of two — in +256-element blocks past 1024 — and never shrinks
  (read from the package source, not from documentation — the auto-size-reduction path defaults
  off). The total is therefore bounded by the longest string each label will ever show.
  Measured: ~72 KB across ~30 labels in a
  2000-frame capture, all in `SetArraySizes → TMP_TextInfo.Resize`. Before engineering a warm-up
  for it, **map each growth event to its frame type** — here they landed on click frames and
  level-start frames, never on the action path, because mid-level counters only count down. A
  warm-up that moves a bounded one-time cost off frames that already pay `Instantiate` is not
  obviously worth permanent machinery. Two facts if you do build one: it must render a **visible**
  glyph (whitespace builds no quads, so the vertex buffers never grow), and `Start` — not `Awake`
  — is the order-safe hook.
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
