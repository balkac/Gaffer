# Unity Runtime Playbook — lifecycle, time, and object lifetime

Engine-side semantics that cause real bugs when guessed wrong. The architecture keeps most code
out of `MonoBehaviour`s (see [`ARCHITECTURE.md`](ARCHITECTURE.md)); this doc is the contract for
the thin engine-facing shell that remains — Presentation, Composition, Infrastructure. It is the
correctness companion to [`PERFORMANCE.md`](PERFORMANCE.md), which covers the cost side.

> **Baseline:** Unity 6 (6000.x), C# 9.0. Version-gated rules are tagged; verify against the
> matching manual version before porting a rule to another engine release.
> Verified: Unity 6000.3.16f1 · last reviewed 2026-07-28.

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
- `Time.maximumDeltaTime` (default 1/3 s) bounds the catch-up burst — it caps how many
  `FixedUpdate`s a long frame can trigger AND clamps `Time.deltaTime` itself, which is what stops
  the spiral where heavy fixed steps lengthen the frame and earn ever more fixed steps.
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
  method to save the state of your application." Persist in **`OnApplicationPause(true)`** and/or
  **`OnApplicationFocus(false)`** — Unity 6's docs explicitly name focus loss as the mobile save
  signal — through the normal Infrastructure save path, and treat "resumed after an arbitrary
  gap" as a normal launch state.
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
- Where a persistent object is **re-bound** to a fresh consumer every level/round, prefer an
  assignable delegate over a multicast event — see `ARCHITECTURE.md` §9; it makes the stale
  subscription structurally impossible instead of discipline-dependent.

## 6. Async & threading correctness

The pure core is synchronous (`ARCHITECTURE.md`'s async boundary keeps `async` in Infrastructure/
Presentation); these rules govern the engine-facing side where it does appear.

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
- **Unity 6's `Awaitable` is NOT a `Task`:** its continuations bypass the
  `SynchronizationContext` and run synchronously where they were triggered — main thread only if
  completed there, otherwise a ThreadPool thread (hop back explicitly via
  `Awaitable.MainThreadAsync()`). And it is **pooled: await an `Awaitable` exactly once** — after
  completion it returns to the pool, so storing and re-awaiting one observes recycled state.
  (UniTask carries the same single-consumption contract — a second await throws unless
  `Preserve()` is called.) Don't carry `Task` assumptions onto either.
- **`async void` only for event handlers.** It can't be awaited and its exceptions bypass the
  caller (they surface on the sync context, not at the call site). Everything else returns
  `Task`/`UniTask` so failures surface.

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
  Mono/IL2CPP, iOS IL2CPP — iOS has no Mono player, §12). The rename is *effectively* atomic on
  one volume; the BCL does not formally guarantee atomicity — so treat `Replace` as an
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
  direct references/Addressables (`PERFORMANCE.md` §14) so this asymmetry never bites.
