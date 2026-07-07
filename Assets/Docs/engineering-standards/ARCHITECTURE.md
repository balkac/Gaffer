# Architecture Playbook — Unity / layered apps

How I structure a project: layers, assemblies, folders, and the one rule that keeps the core
testable (the async boundary). This is the structural companion to [`CONVENTIONS.md`](CONVENTIONS.md)
(which covers code style, naming, SOLID-in-the-small, and error handling). Written for Unity, but
the layering, package-by-feature, and async-boundary ideas apply to any layered .NET app.

For an empty skeleton to copy, see [`starter-tree.md`](starter-tree.md).

---

## 1. Clean architecture — dependencies point inward

```
Domain  ←  Application  ←  Infrastructure / Presentation  ←  Composition
                                     ↑
                         Common (cross-cutting, no deps)
```

- **Domain** — pure C#, **no framework** (no `UnityEngine`), **no exceptions**. Value objects,
  entities, geometry, the core model. Trusts its own internally-validated inputs.
- **Application** — pure & **synchronous**, **headless-testable**, **no framework**. Use cases and
  algorithms orchestrated from small collaborators behind interfaces. Depends only on Domain +
  Common.
- **Infrastructure** — framework-coupled adapters: loading, networking, serialization I/O,
  persistence, configuration `ScriptableObject`s. This is the first layer allowed to be async and
  to touch `UnityEngine`.
- **Presentation** — views, input, rendering, animation. Framework-coupled.
- **Composition** — the manual composition root: wires everything, owns the entry point. No DI
  framework needed; construct and inject by hand. Name it `Composition`, **not** `App` (too close
  to `Application`).
- **Common** — generic cross-cutting primitives with **no dependencies** (e.g. `Result` /
  `Result<T>`). Nothing project-specific goes here.

**One `.asmdef` per layer.** Assembly references encode the arrows above; the compiler then *stops*
you from referencing outward or pulling the framework into the core.

## 2. Assemblies & folders

- **Folder name = the assembly's last segment; one assembly spans the whole layer.** Each layer's
  top folder matches its `.asmdef`: `Domain/` → `MyGame.Domain`, `Presentation/` →
  `MyGame.Presentation`, …, `Composition/` → `MyGame.Composition`. The `.asmdef` file is named
  after the assembly (`MyGame.Composition.asmdef`).
- **Organizational sub-folders below a layer get no assembly and no namespace segment.** They only
  group files (see §4). A file's namespace is declared at the **feature/concern** level under its
  layer (`MyGame.Presentation.Input`, `MyGame.Domain.Levels`); the finer sub-folders inside it keep
  that namespace rather than deepening it — e.g. `Input/Snapping/GridSnapResolver.cs` is still
  `MyGame.Presentation.Input`. This relaxes a strict "folders = namespaces": a file's namespace
  need not include every folder segment. (Some namespaces may legitimately nest deeper where the
  concept genuinely does — don't churn those.)

## 3. Package by feature, not by technical layer

- **No global `Model` / `Services` / `Utils` split.** Keep a concern's data and logic together:
  `Generation/Cutting`, `Generation/Atoms`, `Generation/Partitioning` — not a top-level `Models/`
  bucket that scatters one feature across the tree.

## 4. One responsibility per folder

- **A folder holds a single concern:** an abstraction with its implementation(s) and the small
  types that exist only to support it — e.g. `Input/Snapping/{ISnapResolver, GridSnapResolver,
  SnapResult}`.
- **Different jobs never share a folder.** A view, a layout value-object, and an overlay effect
  each get their own sub-folder (`Board/Views`, `Board/Layout`, `Board/Highlighting`). Split finer
  rather than mixing.
- **Exceptions:**
  - a feature's **orchestrator / entry type may sit at the feature root** above its collaborators'
    sub-folders (e.g. the top-level generator in `Generation/`, the controller in `Gameplay/`);
  - a cohesive set of **value objects that together form one model is one responsibility** — don't
    fragment it into one-type folders (`Domain/Levels`, `Domain/Geometry` stay whole).
  - A sub-folder may start with a single file when it's a genuine standalone concern expected to
    gain siblings.
- These sub-folders organize files only — they do **not** add namespace segments (§2).

## 5. The async boundary — the rule people forget

- **Async lives only in framework-coupled layers** (Infrastructure and up). The pure **Domain** and
  **Application** layers stay **synchronous** so they remain headless-testable with `dotnet test`,
  without a Unity PlayerLoop or event loop.
- In Unity, use **UniTask** (`UniTask<Result<T>>`), which is allocation-free and integrates with the
  PlayerLoop — but *because* UniTask is Unity-coupled, an async port like `ILevelProvider` belongs in
  **Infrastructure**, not Application. The Application layer never returns a `Task`/`UniTask`.
- Same principle outside Unity: keep the algorithmic core synchronous and push `async`/`Task` out to
  the I/O adapters, so the core can be tested without an async host.

## 6. Composition root

- **Manual wiring by default; a DI container only when the graph earns it.** A single `Composition`
  assembly constructs the graph: build the services (provider chains, colour providers, …), inject
  them into the controllers, own the entry point (`GameBootstrapper` / `Program`). Teardown is
  explicit (`IDisposable` drops subscriptions). For a small-to-medium graph, hand-wiring is clearer
  and dependency-free — you can *read* the whole object graph in one file. Reach for a container only
  when the graph's size, lifetime management, or scoping genuinely justifies it; it's a scaling
  decision, not a default.
- Keeping composition in one place means every other layer depends only on abstractions and never
  on the concrete wiring.

## 7. Fallback chains (a reusable I/O pattern)

When a resource can come from several sources, model it as a **chain that falls back**, each source
behind the same interface:

```
Primary (remote)  →  Bundled (local shipped copy)  →  Generated / default
```

Each provider implements the same port (`ILevelProvider`); a `FallbackLevelProvider` composes them
and tries the next on failure. Thin wrappers that differ only in a base URL or root path share one
underlying helper. Decide explicitly whether there's caching (often "one fallback, no cache" is
simplest and clearest).

**Content falls back the same way.** The pattern isn't only for I/O: a configuration asset can fall
back to a built-in default when it's left empty — an empty shape/level catalogue resolves to a
`DefaultShapes`/`DefaultLevels` constant. That makes config an *override*, never a hard dependency, so
the app always runs (a Null-Object flavour of the same "try the next source" idea).

## 8. Command in → outcome out (the core boundary)

Shape the seam between the pure core (Application) and the framework layers as **a command in, an
immutable outcome out** — not shared mutable state the outer layers read back.

- The core takes a **command** (a small value describing intent: *place this piece here*, *apply this
  input*) and returns an **outcome record** describing everything that changed. Presentation
  **replays** that outcome to animate or render; it never reaches into the core to diff what happened.
- Knowledge stays one-way (the core knows nothing about the view), the step is trivially testable
  (assert on the returned record), and it pairs naturally with `Result<T>`: `Result<MoveOutcome>` is
  *either a described change or a reason it failed*.
- Keep the outcome a **read model built for its consumer**, carrying what the view needs to replay and
  nothing more — so it doesn't become the domain entities leaking out under a new name (an
  anaemic-model / leaky-abstraction smell).

## 9. Behavioural seams — vary by injection, not inheritance

Where behaviour must vary, put the variation behind a **port (interface) chosen at the composition
root**, so new behaviour is a new implementation rather than a new branch or a subclass. This is the
structural side of "composition over inheritance" (see [`CONVENTIONS.md`](CONVENTIONS.md) §3). Three
reusable shapes:

- **One driver, many implementations.** A single caller (an input controller, a scheduler) talks to a
  port; several implementations satisfy it and reuse the *same* surrounding machinery. Because the
  contract lives on the interface, **Liskov governs the implementations** — each must honour the
  port's behavioural contract. (That is where LSP lives once you favour composition; it is *not* "moot
  because there's no inheritance" — a substitutable type is a substitutable type, subclass or impl.)
- **Tag commands with their actor.** Carry *who* issued a command on the command itself, and key
  per-actor state (score, turn, stats) by it. Adding a second participant — an AI, a networked peer, a
  replay — is then **additive**: the same command into the same core, not a parallel code path.
- **Model "who supplies the next input" as a port.** An input/move *source* that the loop asks for the
  next command — and that is allowed to **take real time** — lets you swap a local AI for a networked
  peer as a new implementation, with no change to the loop or the rules. It's the fallback-chain idea
  (§7) applied to the *producer* of input rather than to a resource.

## 10. `.meta` hygiene (Unity)

- When you move/rename/delete a `.cs`, move/remove its `.cs.meta` too (**preserve the GUID**) and
  clean orphan `.meta` files. A folder move that only relocates files (no code change) is fine
  precisely because namespaces don't track sub-folders (§2).

---

## Checklist for a new layer / feature

1. Does it belong in Domain (pure, no framework, no exceptions), Application (pure, synchronous),
   or a framework-coupled layer? Put it in the **innermost** layer that can hold it.
2. Is there **one assembly per layer**, with references pointing only inward?
3. Is the feature **packaged together** (by feature), and is each folder **one responsibility**?
4. Does any `async` accidentally sit in Domain/Application? Move it out to Infrastructure.
5. Do fallible operations return `Result`/`Result<T>` for *expected* failure — and still fail-fast on
   a broken invariant (see `CONVENTIONS.md` §4)?
6. Does the core boundary take a **command** and return an **immutable outcome** the outer layer
   replays, rather than exposing mutable state to read back (§8)?
7. Is behavioural variation behind an **injected port** (composition over inheritance), with each
   implementation honouring the port's contract (§9)?
8. Are the pure parts covered by `dotnet`-runnable tests?
