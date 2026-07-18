# Unity Runtime Playbook — lifecycle, time, and object lifetime

Engine-side semantics that cause real bugs when guessed wrong. The architecture keeps most code
out of `MonoBehaviour`s (see [`ARCHITECTURE.md`](ARCHITECTURE.md)); this doc is the contract for
the thin engine-facing shell that remains — Presentation, Composition, Infrastructure. It is the
correctness companion to [`PERFORMANCE.md`](PERFORMANCE.md), which covers the cost side.

---

## 1. Initialisation order

- On scene load, every object's `Awake` → `OnEnable` runs first, and **all** of them finish before
  any `Start` runs. The order *between* objects is undefined.
- The rule that follows: **self-setup in `Awake`** (cache own components, build own state);
  **cross-object reads in `Start`** (everyone's `Awake` is guaranteed done by then). Script
  Execution Order is a last resort, not a design tool.
- `Instantiate` runs the new object's `Awake`/`OnEnable` **synchronously inside the call** —
  before the caller can assign it any data. Anything an object needs at birth goes through an
  explicit `Init(...)` method (or a factory), never through fields set "right after" Instantiate.
- An object that was never activated has run nothing — and if it's destroyed in that state,
  `OnDestroy` never runs either. Teardown must not rely on callbacks that may never fire; the
  composition root owns explicit teardown (`ARCHITECTURE.md` §6).

## 2. Frame order & time

- Per frame: `FixedUpdate` ×0..n (the fixed clock catches up to real time) → `Update` →
  coroutines (`yield return null`) → `LateUpdate` → render. `FixedUpdate` can run **zero** times
  in a fast frame and **several** times after a slow one; `Time.maximumDeltaTime` caps the
  catch-up burst.
- `Time.deltaTime` is context-sensitive: read inside `FixedUpdate`, it returns `fixedDeltaTime`.
- Frame-based input (`GetKeyDown`-style, pointer events) is read in `Update`, never in
  `FixedUpdate` — a fixed step can miss or double-count a frame event. Follow-a-target work goes
  in `LateUpdate`, after the target has moved.
- `timeScale = 0` (pause): `FixedUpdate` stops entirely, `Update` keeps running with
  `deltaTime == 0`, and `WaitForSeconds` never completes. Anything that must animate through a
  pause — menus, transitions — runs on **unscaled time** (`unscaledDeltaTime`; tweens/animators in
  unscaled update mode).

## 3. Enable, disable, destroy

- `gameObject.SetActive(false)` fires `OnDisable` and **kills that object's coroutines dead** —
  they do not resume on re-activation. `script.enabled = false` stops `Update` but that script's
  **coroutines keep running**. The two are not interchangeable; choose deliberately.
- `Destroy(obj)` is deferred to end of frame — the object stays alive for the rest of the frame,
  while Unity's overloaded `==` already reports it equal to `null`.
- **Fake null:** that overloaded `==` is exactly why `?.` and `??` are unsafe on any
  `UnityEngine.Object` — they bypass the overload and dereference a destroyed object. Write the
  explicit `if (obj != null)` check in engine-facing code.

## 4. Mobile app lifecycle

- `OnApplicationPause(true)` (backgrounding) is the **only reliable save point** on mobile — the
  OS may kill a backgrounded app with no further callback. Persist the run there, through the
  normal Infrastructure save path, and treat "resumed after an arbitrary gap" as a normal launch
  state.
- Pause/focus callback order differs between Android and iOS (and between `OnApplicationPause`
  and `OnApplicationFocus`) — verify on device; don't reason from documentation alone.

## 5. Events & teardown

- Every engine-facing subscription (`+=`, `AddListener`) is paired with its removal in the mirror
  callback (`OnEnable`/`OnDisable`, or the composition root's teardown). A subscription to
  anything longer-lived than the subscriber is a leak plus a dead-object callback waiting to fire
  (`CONVENTIONS.md` §6). Same rule as tweens in `PERFORMANCE.md` §7: nothing outlives its target
  without an explicit owner tearing it down.
