# Performance Playbook — Unity

How I keep a Unity project smooth and low-garbage. Most of this is *structural* — the same layering
that makes the core testable (see [`ARCHITECTURE.md`](ARCHITECTURE.md)) also keeps the engine's
per-frame overhead low — plus a handful of concrete Unity habits. These are defaults, not universal
laws: profile before optimising, and drop any of these where a measurement says it doesn't matter.

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
