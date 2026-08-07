# Architecture Playbook — Unity / layered apps

How I structure a project: layers, assemblies, folders, and the one rule that keeps the core
testable (the async boundary). This is the structural companion to [`CONVENTIONS.md`](CONVENTIONS.md)
(which covers code style, naming, SOLID-in-the-small, and error handling). Written for Unity, but
the layering, package-by-feature, and async-boundary ideas apply to any layered .NET app.

For an empty skeleton to copy, see [`starter-tree.md`](starter-tree.md).

> Verified: Unity 6000.3.20f1 · Addressables 2.11.1 · last reviewed 2026-08-06. Engine/BCL facts
> in this set are version-tagged; the layering rules here are **house defaults with rationale** —
> scope notes in place say where they bend.

---

## 1. Clean architecture — dependencies point inward

```
Domain  ←  Application  ←  Infrastructure / Presentation  ←  Composition
                                     ↑
                         Common (cross-cutting, no deps)
```

- **Domain** — pure C#, **no framework** (no `UnityEngine`); expected failure returns `Result`,
  never an exception — fail-fast `throw` stays legitimate for a broken invariant
  (`CONVENTIONS.md` §4 owns that distinction). Value objects, entities, geometry, the core model.
  Trusts its own internally-validated inputs.
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

**A pure adapter assembly is a legitimate seventh box, and the list above has no slot for it.**
Serialization is the usual case. A file format is an *adapter* concern, so it does not belong in
Application; but if the serializer is a plain .NET library rather than a framework one, the adapter
has no reason to be framework-coupled, and putting it in Infrastructure would forfeit headless
testing for nothing. The honest shape is its own assembly — pure, referencing `Common`, the
third-party library and (where the adapter maps into domain types) `Domain`, and referenced by
Composition — which keeps Application free of third-party dependencies while the schema, the
mapping and every validation stay under `dotnet test`. Reach for it only when the adapter really
is framework-free; a `JsonUtility`-based adapter is framework-coupled by definition and belongs
in Infrastructure, which is the split `starter-tree.md` scaffolds.

**Two adapters that serialize are not one module.** Externally-authored *content* and the player's
own *saved data* are different concerns that happen to share a technology: content ships with the
build and its typos must fail loudly in CI, while save data outlives the build and must tolerate
what it does not recognise. Give them separate assemblies that never reference each other, so
neither can reach into the other's types — content also references `Domain`, because its DTOs map
into domain types; saved data deliberately stays on `Common` alone (`starter-tree.md` scaffolds
exactly this split) — and let each own its serializer settings even though the ~10 lines look
duplicated — hoisting the shared helper into `Common` would drag a third-party dependency into
the dependency-free assembly, and the two contexts legitimately want different strictness (§11).

## 2. Assemblies & folders

- **Folder name = the assembly's last segment; one assembly spans the whole layer.** Each layer's
  top folder matches its `.asmdef`: `Domain/` → `MyGame.Domain`, `Presentation/` →
  `MyGame.Presentation`, …, `Composition/` → `MyGame.Composition`. The `.asmdef` file is named
  after the assembly (`MyGame.Composition.asmdef`).
- **Organisational sub-folders below a layer get no assembly and no namespace segment.** They only
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
    fragment it into one-type folders (`Domain/Levels`, `Domain/Geometry` stay whole);
  - a sub-folder may start with a single file when it's a genuine standalone concern expected to
    gain siblings.
- These sub-folders organise files only — they do **not** add namespace segments (§2).

## 5. The async boundary — the rule people forget

- **Async lives only in framework-coupled layers** (Infrastructure and up). The pure **Domain** and
  **Application** layers stay **synchronous** so they remain headless-testable with `dotnet test`,
  without a Unity PlayerLoop or event loop. *Scope note:* this is the default **profile for a
  fully-local content game** (casual/puzzle, millisecond loads at designed moments). A game with
  real backend surfaces — cloud save, remote config, IAP, live-ops — legitimately grows async
  *orchestration* in its outer flow controllers; the pure rules core stays synchronous either way,
  and that split is the part that generalises.
- In Unity, the preferred async primitive is **UniTask** (`UniTask<Result<T>>`) — and the reasons
  are mechanical, not fashion: `Task` is a class (a heap object per operation, plus state-machine
  boxing on suspension) and resumes through a `SynchronizationContext.Post`; UniTask is
  struct-based with pooled continuations (~zero allocation) and integrates directly with the
  PlayerLoop (`await UniTask.Yield(PlayerLoopTiming.…)`) — under a non-generational GC
  (`PERFORMANCE.md` §10) that allocation difference is the point. But *because* UniTask is
  Unity-coupled, an async port like `ILevelProvider` belongs in **Infrastructure**, not
  Application. The Application layer never returns a `Task`/`UniTask`.
- **UniTask is a preference, not a prerequisite** — adopt it when the async surface earns it, not
  before. Addressables needs no extra library to be awaited: every `AsyncOperationHandle` already
  offers `Completed` callbacks, coroutine `yield`, a built-in `.Task` for plain `await`, and
  synchronous `WaitForCompletion`. UniTask's Addressables extension makes the await
  allocation-free and adds cancellation ergonomics (`WithCancellation(destroyCancellationToken)`)
  — valuable once many concurrent loads, cancellation chains, or loop-timed flows exist; noise as
  a dependency while async remains a handful of load-time awaits (which happen on transition
  frames, where a `Task` allocation is tolerable anyway). The tool arrives together with the
  boundary opening — never ahead of it.
- Same principle outside Unity: keep the algorithmic core synchronous and push `async`/`Task` out to
  the I/O adapters, so the core can be tested without an async host.
- **Don't open the boundary before a real latency exists — a researched minority stance, held on
  its merits.** The ecosystem default is async everywhere; this set deliberately deviates for
  bundled-local content, on this evidence line: Unity's own rationale for async-by-default is
  **latency** ("content might need to be downloaded first… take a long time") — the *network*
  kind is absent for local bundles (local still pays disk, decompression and deserialization on
  the main thread — `PERFORMANCE.md` §14), and for the local/cached case the docs put the sync
  cost at "small"/"minimal" and present `WaitForCompletion` as a supported workflow, not an
  escape hatch. Async on local content introduces **intermediate states that require explicit
  handling**: the moment a flow awaits, its in-between state becomes observable — input arriving
  mid-load, teardown mid-await, placeholder frames when the handling is missed (observed in the
  wild: Unity's own Localization package, async by default, is reported to flash fallback text
  for a frame or two on local tables, and offers a supported *synchronous* mode). And async is
  contagious — every caller of an async signature becomes async (the literature genuinely
  disputes whether that is a cost or a discipline; this set treats it as a cost to defer until
  it buys something). The stance is only legitimate **with
  every guardrail below satisfied and written into the port's contract** — see the checklist
  immediately following this section.
  Future-proofing is done by **placing the seam** (a port the async implementation will later
  stand behind, §7's fallback chain) — with the honest caveat that the true migration cost is not
  signatures but **caller timing assumptions**: call sites quietly accrete same-frame-completion
  expectations, so the port's contract states completion timing explicitly. Unity 6 also ships a
  first-party pooled awaitable (`Awaitable`) — semantics differ from `Task`; see
  [`UNITY.md`](UNITY.md) §6.
- **When content does go remote, split download from load.** `DownloadDependenciesAsync` (with
  `GetDownloadSizeAsync` for the "download N MB?" prompt) pulls bundles into the local cache as an
  explicit, async, UX-owned phase — progress, cancellation, retry and the fallback chain all live in
  that one designed loading state, never mid-gameplay. After it, the actual asset **load is local
  again** (cache-hit) and the synchronous flow shape survives; instantiation of heavy prefabs is a
  separate main-thread cost handled by the usual load-time pooling, not by async.

### 5a. The synchronous-load guardrails — a checklist, not prose

This set **endorses** synchronous loading for bundled-local content (§5). A practice that is
endorsed with conditions must carry those conditions where the reader who was just persuaded will
see them — so they are a list, not a subordinate clause inside the paragraph that argued for the
practice.

**Scope and provenance — read this before skipping the list.** Items 1–4 and 6 restate documented
`WaitForCompletion` limitations and do **not** bind an asynchronous load. Item 5 is this set's own
rule and binds **both**: an async load does not block its caller, but its disk access, bundle
decompression and asset deserialization still land on the main thread in *some* frame, so which
frame pays remains a design decision, not something async makes free. Item 2 is the one we have
shipped a violation of; its cost is in `PERFORMANCE.md` §14.

1. **Local content only.** Remote is a different port. The current docs are already imperative —
   "Don't call `WaitForCompletion` on an operation that's going to fetch and download a remote
   `AssetBundle`" (the 1.x docs merely called it "not recommended") — and the reason stands on
   its own: the failure it invites, a main-thread block whose length is a network's, is not one
   a designed loading moment can bound.
2. **Never call `WaitForCompletion` during `Awake`, or anywhere before the first scene has
   finished loading.** Unity's own remedy is to call it during `Start` instead. **It is the
   synchronous block that is dangerous, not the callback**: an *async* load started from `Awake`
   is fine — it returns to the player loop immediately and resumes later, and it owes the
   intermediate-state handling §5 already describes (a rendered loading state, input off), not
   this list. The failure this item prevents is a **deadlock, not a stall**, and it cannot be
   reproduced in the editor — `PERFORMANCE.md` §14 carries the measured case.
3. **Never for scenes.** On `LoadSceneAsync` it waits for dependencies but scene *activation*
   still completes asynchronously; on a scene unload it unloads nothing and logs a warning; and
   loading two scenes in succession with `WaitForCompletion` on the second **can deadlock** (the
   docs' remedy: load successive scenes asynchronously, or put a delay between the requests).
4. **Not on WebGL** — unsupported, because the platform is single-threaded and the wait loop
   blocks the web request itself.
5. **Worst-case stall measured on target hardware**, and confined to designed loading moments —
   a gameplay or animation frame never blocks on a load.
6. **The port owner knows the concurrent Addressables population.** Calling it on any asset load
   completes **all currently active asset load operations** (`PERFORMANCE.md` §14) — including
   loads issued by packages and SDKs you did not write, such as localisation tables, catalog
   updates or an ad SDK. The docs' guidance follows from this: use it when the current operation
   count is *known* and completing all of them synchronously is what you actually intend.

Any item that cannot be satisfied means the boundary opens for that load: it becomes async, and
the wait becomes a designed, visible loading moment rather than a hidden one.

**Where this list lives is part of the rule.** It belongs in the port's contract — the type that
owns the loading API — not only here. A constraint that lives only in prose has no owner, and a
constraint with no owner is a defect waiting to happen (§8a states the general rule); the port is
the one place every load passes through, so it is the only place the rule cannot be forgotten at a
call site.

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
- **The composition root constructs; it does not wire behaviour.** Anything that runs *after*
  startup — routing input, reacting to gameplay events, driving a flow — lives in its own class
  from the first day, and that class subscribes to its collaborators **in its own constructor**,
  having received them by injection. A bootstrapper that grows an event handler has stopped being
  a composition root and become a controller with a misleading name. The payoff shows up in
  teardown: each such class implements `IDisposable` and unsubscribes there, and the root disposes
  them in **reverse construction order**, so ownership reads the same way in both directions.
- **The composition root also owns lifetimes — including the app-lifetime/rebind model.** In a
  level/round-based game, prefer app-lifetime components that are **re-bound** per level over
  destroy-and-recreate cycles (the performance case is `PERFORMANCE.md` §4). The contract that
  makes it safe: every persistent component exposes one `Configure`/`Bind`-style rebind that
  explicitly resets its clocks, pending callbacks, and stale references — nothing is "trusted to
  die with the level", because nothing dies. And every runtime-created *native* object (`Mesh`,
  `Material`, render texture) has exactly **one named owner** that creates it once and releases it
  in its own teardown — never shared creation with ambient ownership.

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
- **The sim resolves instantly; only presentation takes time.** The core applies a command and
  returns the *final* state in the same call — falling pieces, cascades and refills are already
  resolved in the outcome; the view animates *toward* that truth on its own clock. This is what
  makes "the board stays interactive while things animate" trivial: a new tap queries the logical
  board (already final), not the half-animated view. The alternative — gating input on animation
  completion — is both worse feel and a coupling of rules to rendering.
- **When a rule genuinely needs a presentation-derived fact, the fact travels *in the command*.**
  Sometimes the rules really do depend on something only the view knows — whether a piece is still
  mid-flight, which cells are currently occupied on screen. That is not a reason to open a callback:
  put the fact in the command as data (`ProcessTap(TapCommand)` carrying the set of cells still in
  the air), and the core reads it and calls nobody. Knowledge stays one-way and the sim still
  resolves in one call.
  Two alternatives are worth naming because they look reasonable and are not: threading a predicate
  (`Func<int, bool>`) down through the call chain gives an anonymous callback identity at no call
  site and quietly multiplies overloads; and a port the core calls back into is precisely the
  rules-to-rendering coupling the bullet above rejects, wearing an interface. Also derive anything
  the *view* shows about the rule from the same input — a preview icon computed from a different
  set than the rule uses is a bug the player will find before you do.
- **When the core is deterministic, persist the inputs, not the state.** A synchronous, seeded,
  deterministic core means the command log *is* the save: level id, seed, and the ordered list of
  commands replays the exact state, including derived events the state snapshot would have had to
  encode separately. It also avoids needing a serializable RNG, which the BCL's `Random` does not
  give you. The costs are real and belong in the decision: replay must run **before** the events
  that presenters subscribe to, or boot animates the whole session; and **the core's behaviour
  becomes a save format** — change a rule and old saves diverge on replay, so the version policy
  and the fallback (resume, or restart the level) must be decided at the same time.

### 8a. An ordering constraint gets a single owner

A multi-step operation whose steps must happen in a particular order — *decide the outcome, then
repair the board*; *load, then migrate, then publish* — is a defect waiting to happen for as long
as the order lives in prose. Documenting it, with its reasoning, is not enough: the constraint is
invisible at every individual call site, because each step is a legal call and only their
*relationship* is wrong.

The failure has a shape worth recognising. It needs several call paths that each perform the
operation themselves (gameplay, a replay, a test harness, a bot), no single place where the order
is expressed, and a violation that only manifests under a narrow condition — so ordinary testing
passes. That combination will break the rule at whichever call path was written last.

**The fix is to move the order out of prose and into one method that every path goes through.**
Concretely: the step that used to act now only *reports* (an outcome carries a "this needs
repair" flag), and one resolve method applies the outcome and performs the repair — conditionally,
in the right order, once. Gameplay, replay and the test bot all call it, so the decision cannot
diverge between them.

Where the constraint spans an API you don't own (you cannot stop someone calling it out of order),
the achievable version is **structural unreachability rather than structural impossibility**:
confine the API to a single type, and state the ordering in that type's contract — see §5a, which
is the same rule applied to a loading port.

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
- **A 1:1, re-bound callback is an assignable delegate, not an event — a default with a real
  trade, not a dogma.** C# `event`s model *many* listeners with *matching* unsubscribes; on a
  persistent component whose consumer is replaced every level, `+=` quietly accumulates dead
  subscribers and the leak is one forgotten `-=` away. An
  assignable `Action` property ("latest binding wins") makes the stale subscription **structurally
  impossible** instead of discipline-dependent. Reserve `event` for genuine broadcast seams where
  independent listeners come and go.

  **The trade this makes, stated so the rule can be overridden knowingly.** An assignable delegate
  buys structural safety and *sells reviewability*: there is no `-=` in the source, so a reader
  auditing teardown finds nothing to read, and a binding that should have been dropped looks
  identical to one that should persist. Where a team audits teardown by eye — a written `+=` in
  the bind method and a written `-=` in the mirror callback, checked at review — `event` with a
  visible unsubscribe is the better trade, and expecting the flow to change later strengthens it
  further. This has been overridden in practice on exactly that reasoning. Neither choice is
  wrong; **pick by which failure your process actually catches**, and write the choice down where
  the callback is declared.

## 10. `.meta` hygiene (Unity)

- When you move/rename/delete a `.cs`, move/remove its `.cs.meta` too (**preserve the GUID**) and
  clean orphan `.meta` files. A folder move that only relocates files (no code change) is fine
  precisely because namespaces don't track sub-folders (§2).

## 11. Content schemas — a version is two different facts

Externally-authored content (levels, rules, balance tables) needs a versioning story before it
needs a loader. The trap is that one number gets asked to answer two unrelated questions:

- **"Which schema does this class implement?"** is a *fact about the document*, and belongs on the
  document type as a constant beside the fields it describes.
- **"Which versions does this build accept?"** is a **policy**, and in anything with a live-ops
  path it is a *range*, not an equality — it arrives with the rest of the deployment contract.

Collapsing the two is correct **only** when content ships inside the build, because then file and
reader are atomic and there is no acceptance policy to tune. Say so explicitly rather than by
omission, because the day content stops travelling with the binary, several things change at once:

- the version is **negotiated**, not discovered after download — in the request or the path
  (`/content/v3/…`), so a client never parses something it cannot accept;
- a manifest carries `schemaVersion` and a minimum app version per bundle;
- **unknown fields are ignored**, so an additive server-side change cannot brick clients already
  in the field;
- content is **re-published, not migrated** — migration belongs to player data, which outlives the
  build (`UNITY.md` §7);
- a breaking change is served from a new major endpoint behind a min-version gate that tells the
  player to update, instead of failing silently.

**State the strictness as a posture, not a default.** Failing the parse on an unknown member is the
right call for content that ships in the build and whose typos must break CI; it is the wrong call
for remote content, where it is exactly the mechanism that bricks live clients. Whichever you
choose, write down which situation you chose it for — this is the setting that has to flip on the
day the delivery model changes, and nothing else will remind you.

**Don't group constants by what they are.** A shared `ContentConstants` holder collecting every
version number is the same generic-bucket smell §3 rejects at folder level: it separates a version
from the fields it describes, and two versions that happen to share a value today are still
independent facts. Constants live on the type whose concept they describe.

---

## Checklist for a new layer / feature

1. Does it belong in Domain (pure, no framework, `Result` for expected failure — `CONVENTIONS.md`
   §4), Application (pure, synchronous), or a framework-coupled layer? Put it in the **innermost**
   layer that can hold it.
2. Is there **one assembly per layer**, with references pointing only inward?
3. Is the feature **packaged together** (by feature), and is each folder **one responsibility**?
4. Does any `async` accidentally sit in Domain/Application? Move it out to Infrastructure.
5. Do fallible operations return `Result`/`Result<T>` for *expected* failure — and still fail-fast on
   a broken invariant (see `CONVENTIONS.md` §4)?
6. Does the core boundary take a **command** and return an **immutable outcome** the outer layer
   replays, rather than exposing mutable state to read back (§8)?
7. Is behavioural variation behind an **injected port** (composition over inheritance), with each
   implementation honouring the port's contract (§9)?
8. Does any **ordering constraint** in the feature have a single owner that every call path goes
   through, rather than living in prose (§8a)?
9. If the feature reads externally-authored content, is its **strictness a stated posture** and its
   version a document fact rather than an acceptance policy (§11)?
10. Are the pure parts covered by `dotnet`-runnable tests?
