# Unity Runtime Playbook — lifecycle, time, and object lifetime

Engine-side semantics that cause real bugs when guessed wrong. The architecture keeps most code
out of `MonoBehaviour`s (see [`ARCHITECTURE.md`](ARCHITECTURE.md)); this doc is the contract for
the thin engine-facing shell that remains — Presentation, Composition, Infrastructure. It is the
correctness companion to [`PERFORMANCE.md`](PERFORMANCE.md), which covers the cost side.

> **Baseline:** Unity 6 (6000.x), C# 9.0. Version-gated rules are tagged; verify against the
> matching manual version before porting a rule to another engine release.
> Verified: Unity 6000.3.20f1 · last reviewed 2026-08-06.
> **2026-08-06 audit:** §1's `OnDestroy` rule, §3's `Destroy` timing, §4's Android keyboard
> focus behaviour and §6's `Awaitable` pooling were re-checked against the Unity 6000.3 scripting
> reference and hold verbatim. A same-day verification against the live pages then corrected the
> audit itself twice: §4's save-on-focus-loss prescription **is** Unity's — it lives on the
> `OnApplicationQuit` page, not the `OnApplicationFocus` page — and §6's `Awaitable` continuation
> threading **is** documented, on the manual's *Awaitable completion and continuation* page, now
> cited. §2's `maximumDeltaTime` default stays corrected: no doc page prints the number; it is
> read from the project's Time settings. §8's asset-writer rules and all of §9 are **project
> measurement**, not documentation — each carries the setup it was observed on; §9's stripping
> entry cites the documented Minimal policy and leaves open only where "user-written" ends.

---

## 1. Initialisation order

- On scene load, each object runs `Awake` → `OnEnable` **as a per-object pair**, and every pair
  finishes before **any** `Start` runs. The pairs **interleave across objects** in undefined
  order — never assume another object's `OnEnable` has run inside your `Awake`/`OnEnable`, and
  never assume "all Awakes, then all OnEnables" (that ordering does not exist).
- The rule that follows: **self-setup in `Awake`** (cache own components, build own state);
  **cross-object reads in `Start`** (everyone's `Awake` is guaranteed done by then). Script
  Execution Order is a last resort, not a design tool.
- `Instantiate` on an active object runs the new object's `Awake`/`OnEnable` **synchronously
  inside the call** — before the caller can assign it any data. Anything an object needs at birth
  goes through an explicit `Init(...)` method (or a factory), never through fields set "right
  after" Instantiate — **and the mirror invariant: `Awake`/`OnEnable` must never *require* what
  `Init` delivers**, because they have already run by the time it's called (self-setup only, per
  the first rule; gameplay behaviour starts when `Init` completes). When a component genuinely
  needs its data before its enable callbacks, instantiate it **inactive** (inactive prefab or
  parent), configure, then activate — or keep the mandatory state in a plain C# object constructed
  before any `MonoBehaviour` is involved.
- A component on an **active** GameObject gets `Awake` even while the component itself is
  disabled; only an **inactive GameObject** defers it. An object that was never activated has run
  nothing — and per the docs, `OnDestroy` "is only called on GameObjects that have previously
  been active". Teardown must not rely on callbacks that may never fire; the composition root
  owns explicit teardown (`ARCHITECTURE.md` §6).

## 2. Frame order & time

- Per frame: `FixedUpdate` ×0..n (the fixed clock catches up to real time) → `Update` →
  coroutines (`yield return null`) → `LateUpdate` → render. `FixedUpdate` can run **zero** times
  in a fast frame and **several** times after a slow one.
- `Time.maximumDeltaTime` bounds the catch-up burst — the scripting docs state it both limits
  `Time.deltaTime` (the docs' wording: "in the following frame" after a very slow one) and bounds
  the number of `FixedUpdate` calls in a frame to `maximumDeltaTime / fixedDeltaTime`, which is
  what stops the spiral where heavy fixed steps lengthen the frame and earn ever more fixed
  steps. Its default (1/3 s) is printed on **no** doc page — the scripting page omits it and the
  Time Manager manual page describes the field without the number; read it from the project's
  Time settings (`Maximum Allowed Timestep`).
- `Time.deltaTime` is context-sensitive: read inside `FixedUpdate`, it returns `fixedDeltaTime`
  (verbatim documented behaviour).
- Frame-based input (`GetKeyDown`-style, pointer events) is read in `Update`, never in
  `FixedUpdate` — a fixed step can miss or double-count a frame event. Follow-a-target work goes
  in `LateUpdate`, after the target has moved.
- `timeScale = 0` (pause): `FixedUpdate` stops entirely, `Update`/`LateUpdate` keep running with
  `deltaTime == 0`, and a `WaitForSeconds` coroutine never completes (`WaitForSecondsRealtime`
  does). Anything that must animate through a pause — menus, transitions — runs on **unscaled
  time** (`unscaledDeltaTime`; tweens/animators in unscaled update mode).

## 3. Enable, disable, destroy

- `gameObject.SetActive(false)` fires `OnDisable` and **stops that object's coroutines
  permanently** — they do not resume on re-activation (deactivating a parent stops them the same
  way). `script.enabled = false` stops `Update` but that script's **coroutines keep running**.
  The two are not interchangeable; choose deliberately.
- `Destroy(obj)` is deferred — the docs put actual destruction "after the current Update loop,
  but always before rendering". The object stays alive for the rest of the update phase, and the
  overloaded `==` reports it equal to `null` only once destruction actually happens — code
  running in the same frame after `Destroy` can still observe `obj != null`.
- **Fake null:** Unity's overloaded `==` is exactly why `?.` and `??` are unsafe on any
  `UnityEngine.Object` — the Unity 6 `Object` docs now state this outright ("not supported with
  Unity Objects because they can't be overridden"). Write the explicit `if (obj != null)` check
  in engine-facing code.

## 4. Mobile app lifecycle

- `OnApplicationQuit` is **not** a mobile save point: the docs say iOS apps "suspend rather than
  quit, so OnApplicationQuit won't be called", and for mobile generally: "don't rely on this
  method to save the state of your application." The same page prescribes the replacement:
  "consider every loss of application focus as the exit of the application and use
  MonoBehaviour.OnApplicationFocus to save any data." So persist in
  **`OnApplicationPause(true)`** and/or **`OnApplicationFocus(false)`** through the normal
  Infrastructure save path, and treat "resumed after an arbitrary gap" as a normal launch state.
  *(Cite the right page: the prescription lives on `OnApplicationQuit`; the `OnApplicationFocus`
  page itself only documents when the callback fires.)*
- Two documented gotchas: `OnApplicationPause` fires once with `false` during startup (right
  after `Awake`) — save logic must ignore that call; and on Android, focus can be lost without a
  pause (on-screen keyboard). The often-quoted exact iOS pause/focus ordering is community lore,
  not documentation — verify on device before depending on it.

## 5. Events & teardown

- Every engine-facing subscription (`+=`, `AddListener`) is paired with its removal in the mirror
  callback (`OnEnable`/`OnDisable`, or the composition root's teardown). A subscription to
  anything longer-lived than the subscriber is a leak plus a dead-object callback waiting to fire
  (`CONVENTIONS.md` §6). Same rule as tweens in `PERFORMANCE.md` §7: nothing outlives its target
  without an explicit owner tearing it down.
- Where a persistent object is **re-bound** to a fresh consumer every level/round, an assignable
  delegate makes the stale subscription structurally impossible instead of discipline-dependent —
  but it also removes the visible `-=` a teardown audit reads for. `ARCHITECTURE.md` §9 states
  both sides and how to choose; whichever you pick, write the choice down where the callback is
  declared.

## 6. Async & threading correctness

The pure core is synchronous (`ARCHITECTURE.md`'s async boundary keeps `async` in
Infrastructure/Presentation); these rules govern the engine-facing side where it does appear.

- **An `async` continuation outlives its GameObject.** Coroutines die with their object — a safety
  AND a limitation (they also return no values, can't `try/catch` across a `yield`, and an
  unhandled exception terminates the coroutine: Unity logs it to the console, but nothing
  propagates to the starter — no structured failure, no way to await the result, and any partial
  side effects stand). An `async`/`UniTask` continuation does not die with its object —
  it resumes after `Destroy` and throws `MissingReferenceException` on the first touch. Guard the
  resume point: check the object explicitly (the real `!= null`, §3) or pass
  `destroyCancellationToken` (Unity 2022.2+) / `Application.exitCancellationToken` into the
  awaited call. Moving from coroutines to async trades automatic death for explicit cancellation —
  same family as tween/event teardown (§5): nothing outlives its target unowned.
- **The Unity API is main-thread-only** ("most Unity APIs aren't thread-safe and can only be
  called from the main thread", per the manual). Off-main-thread access is **unsupported, not
  reliably fail-fast**: many calls are guarded and throw `UnityException`, but that guard is not a
  documented universal contract — an unguarded call may corrupt state or crash instead of
  throwing. Pure C# — math, collections, file IO, the whole core — is fine off-thread; that's
  exactly what the layering isolates. (`Debug.Log` works from any thread — the log pipeline is
  documented as multithread-aware — but Unity never documents the method itself as thread-safe.)
- **`await` on a `Task` resumes on the main thread only when the await runs there.** The
  continuation context is captured **per-await** from `SynchronizationContext.Current`: an await
  executing on the main thread posts back through the `UnitySynchronizationContext` (documented to
  resume on the next frame's Update tick); an await executing on a pool thread — inside
  `Task.Run`, or after a `ConfigureAwait(false)` earlier in the chain — resumes on the thread pool
  with **no** main-thread guarantee. Don't use `ConfigureAwait(false)` in Unity gameplay code; it
  deliberately discards the context capture for no benefit here.
- **Unity 6's `Awaitable` is NOT a `Task`.** Both halves are documented, on two different pages.
  **Pooling** (the scripting page): instances "are pooled and therefore not safe to `await`
  multiple times in the same method" — treat it as single-consumption; "re-awaiting observes
  recycled state" is our reading of what "not safe" means, not the docs' words. **Continuation
  threading** (the manual's *Awaitable completion and continuation* page): unless documented
  otherwise, an `Awaitable` called from the main thread resumes on the main thread; called from
  anywhere else, it resumes on a .NET `ThreadPool` thread — and the page presents `Awaitable` as
  skipping `Task`'s `SynchronizationContext` capture, naming that capture as overhead `Awaitable`
  avoids. When the resume thread matters, **hop explicitly** via `Awaitable.MainThreadAsync()` /
  `BackgroundThreadAsync()` rather than assuming one.
  (UniTask carries the same single-consumption contract — a second await throws unless
  `Preserve()` is called.) Don't carry `Task` assumptions onto either.
- **`async void` only for event handlers.** It can't be awaited and its exceptions bypass the
  caller (they surface on the sync context, not at the call site). Everything else returns
  `Task`/`UniTask` so failures surface.
- **A restartable async operation owns one `CancellationTokenSource`.** On re-trigger, cancel
  AND dispose the previous CTS before creating the new one — re-entering the operation (a
  transition retriggered mid-flight) then cancels the in-flight run instead of racing two
  copies. Cancel + dispose in the owner's teardown too (an in-flight task outliving its owner
  is the async coroutine leak, first bullet). Catch `OperationCanceledException` explicitly at
  the awaited call and `return` — the cancel-and-bail path stays visible in the method body
  instead of relying on the framework to swallow it.

## 7. Persistence traps (mobile)

The Infrastructure save path (`ARCHITECTURE.md`; save-on-pause in §4 above) must respect these:

- **Write saves to `Application.persistentDataPath`** — never `dataPath`, which is read-only
  inside the APK/bundle on device (it works in the editor, then fails on the phone).
- **Atomic-in-practice writes.** The OS can kill a backgrounding app mid-write. Write to a temp
  file, then `File.Replace` over the real save — with the caveats the BCL documents: `Replace`
  throws when the destination doesn't exist yet (first save falls back to `File.Move`), throws
  when source/destination sit on different volumes (keep both inside `persistentDataPath`), and
  its docs reserve an `UnauthorizedAccessException` branch for "this operation is not supported
  on the current platform" — absence of failure reports is not a platform guarantee, so
  smoke-test the save path on every platform/backend combination actually shipped (Android
  Mono/IL2CPP, iOS IL2CPP — iOS has no Mono player, `PERFORMANCE.md` §12). The rename is
  *effectively* atomic on one volume; the BCL does not formally guarantee atomicity — so treat
  `Replace` as an
  *optimisation of the save protocol*, not its correctness: on load, a corrupt/unparsable save is
  an expected `Result.Failure` with a defined fallback (previous file or fresh run), never an
  unhandled crash at boot, and the interruption points (write → flush → replace/move → cleanup)
  are what the device smoke test exercises.
- **Versioned schema, migration chain.** Every save carries a `version` field. Adding a field is
  back-compatible (missing → default); renaming or removing needs a migration step. Never ship a
  schema change without a migration test from the previous version. Persist **enums by name**,
  not ordinal (`CONVENTIONS.md` §6).
- **Serializer limits.** `JsonUtility` silently drops `Dictionary`, `DateTime`, and properties —
  no error, just missing data. Know its whitelist or use a serializer that fails loudly, behind
  one adapter (exceptions → `Result`, `CONVENTIONS.md` §4).
- **`PlayerPrefs` is for small flags only** (settings toggles, first-launch marks): plain
  platform key-value storage (plist / SharedPreferences), no versioning, no atomicity, no
  encryption. Run state never goes there. **`BinaryFormatter` is banned** — a documented
  deserialization-RCE class; .NET 9 removed it outright, and while today's Unity BCL still ships
  a working copy, it is a dead end the runtime migration will close.
- **`ScriptableObject`s are config, not state.** Runtime edits to an SO persist in the editor
  (dirtying the asset) but are session-only in a build — an SO is neither a save medium nor safe
  scratch space. Config data flows SO → pure settings object at load; runtime state lives in the
  core and saves through Persistence.
- **`StreamingAssets` on Android lives inside the APK/AAB archive** — unreadable via
  `File`/`System.IO` (the manual sends you to `UnityWebRequest`); iOS reads it directly. Prefer
  direct references/Addressables (`PERFORMANCE.md` §12) so this asymmetry never bites.

## 8. Inspector & serialization hygiene

- **`[SerializeField] private T _field;` over `public T Field;`** when a field should be
  Inspector-editable but is not public API — serialization visibility and API surface are
  separate decisions; don't widen the second to get the first.
- **`[Tooltip("...")]` instead of a comment** on a serialized field — it reaches the person
  actually tuning the value in the Inspector.
- **`[Range(min, max)]` on bounded numerics** — the Inspector enforces the bound instead of a
  comment pleading for it.
- **Group related serialized fields into a `[Serializable]` struct/class** rather than a flat
  list of loose fields — the Inspector shows the grouping, and rebind code passes one object.
- **One writer at a time for a `ScriptableObject` asset.** With the object loaded, the editor
  treats its in-memory copy as the truth and flushes it back over any edit made to the `.asset`
  file on disk at the next serialize — no warning, and the symptom is "my change did nothing".
  The mirror hazard is a *stale* asset: fields added to the class since the asset was last saved
  are not in the file, so those run from the C# field initializers instead, and the first
  inspector save bakes in whatever was live at that moment. Pick one writer — inspector, or disk
  with a forced reimport before anything else touches it — and keep field initializers identical
  to the shipped asset values so whichever side wins a race, behaviour is the same.
- **Unity rewrites assets under you; read asset diffs before every commit.** Two observed
  mechanisms, both silent, both observed in the Unity 6000.3 editor: re-importing a sprite
  resets a **Sliced** `SpriteRenderer`'s `size`
  back to the sprite's raw canvas (joining an atlas is a re-import, so a batching change can
  quietly resize every prefab that draws that sprite), and an `[ExecuteAlways]` component that
  solves against `Screen.*` in edit mode gets the **focused editor window's** dimensions rather
  than the game view's, and writes the resulting positions into the prefab. Runtime code often
  self-heals both on the next layout pass — the *asset* does not.

## 9. Build-only and device-only failure modes

**The editor is not a target platform; it is an approximation with different mechanics.** Each
item below is a failure class that is *structurally absent* from every editor run — not rare in
the editor, impossible there — so "it works in play mode" is not weak evidence for them, it is no
evidence at all. Budget a device build early for anything in this list.

- **Addressables in the editor resolves through the asset database, with no bundles** — under
  the *Use Asset Database* play-mode script, the standard editor setting (*Use Existing Build* is
  the exception that loads real bundles in play mode). Bundle completion, load ordering and the
  whole family of `WaitForCompletion` hazards therefore cannot reproduce under it
  (`ARCHITECTURE.md` §5a, `PERFORMANCE.md` §14).
- **Managed code stripping does not exist in the editor.** See the case below.
- **A Memory Profiler snapshot taken in a desktop editor reports desktop texture formats**
  (BC/DXT), not the ASTC/ETC2 the device will load — so it measures the wrong thing for a mobile
  memory budget. Take the snapshot on the device.
- **`Screen.*` in edit mode describes the focused editor window**, not the game view (see §8).
- **EditMode tests have no engine clock.** Nothing the engine drives advances: particle systems
  never simulate, so a pooled effect never reports itself finished and its pool drains forever.
  Measured consequence: a pooling allocation test read ~6 KB per action of "production
  allocation" that was entirely an artefact of the harness. Anything whose lifetime the engine
  owns must be ended explicitly by the test, or the measurement is fiction.

**The stripping case, because its diagnosis is the reusable part.** An assembly whose only entry
point was a `[RuntimeInitializeOnLoadMethod]`, and which no other assembly referenced, was removed
by IL2CPP managed stripping in an iOS build. No error, no warning — the feature simply did not
exist at runtime. What makes it worth writing down is that the two obvious places to look both
lie:

- `Data/RuntimeInitializeOnLoads.json` inside the shipped build **still listed the method**. That
  file is written *before* stripping runs, so its contents read like proof the code shipped and
  are nothing of the kind.
- The assembly's own `Runtime/link.xml`, authored for exactly this hazard, **was never handed to
  the linker** — `Library/Bee/Player*-inputdata.json` showed the assembly going into UnityLinker
  as an input while the only `link.xml` files collected were another package's. A `link.xml`
  inside a package is not automatically collected.

What actually settles it: search **`global-metadata.dat`** in the built player for the type names.
Zero hits for the stripped assembly's types against 66 for a live one is unambiguous, and it is
the only check in this list that reads the shipped binary rather than a build artefact.

**Prefer an explicit call over a `link.xml`.** A `link.xml` keeps the assembly alive but leaves
the policy invisible, and its own protection had already failed silently once here. A call from
the composition root *is* the reference that defeats stripping, and it puts "which builds include
this" in the file where wiring is read.

*(Evidence: one project, Unity 6000.3, iOS IL2CPP, `managedStrippingLevel` left at its default.
The documented policy — Minimal, the IL2CPP default, under which "Unity doesn't remove any
user-written code" — did not protect it: an unreferenced **package** assembly evidently sits
outside what "user-written" covers, and the docs never say where that line runs. Verify on the
build you ship rather than reasoning from the rule.)*
