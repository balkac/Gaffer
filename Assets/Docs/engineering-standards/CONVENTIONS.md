# Engineering Conventions — C# / .NET

The rules I follow in every C# project, and the reasoning behind them. The mechanical ones
(braces, `var`, spacing, import order) are enforced by [`.editorconfig`](.editorconfig), which
Rider and `dotnet format` both read — so they are tool-checked, not just documented here. For
project structure (layers, assemblies, folders, the async boundary) see
[`ARCHITECTURE.md`](ARCHITECTURE.md).

> When in doubt, take Rider's / ReSharper's suggestion — `.editorconfig` is the shared baseline
> for them.

These conventions are framework-agnostic: they hold in a Unity project, an ASP.NET service, or a
plain console library.

---

## 1. Code style (enforced by `.editorconfig`)

- **Braces everywhere.** Every `if`/`for`/`foreach`/`while` uses `{ }`, even for a single
  statement. No inline bodies, no brace-less stacked loops.
- **Allman braces.** The opening brace goes on its own next line; the body follows on the next
  line:
  ```csharp
  for (int x = 0; x < size; x++)
  {
      Step(x);
  }
  ```
- **`var` when the type is apparent** from the right-hand side (`new T(...)`, casts, array
  literals). Keep the **explicit type** otherwise — e.g. a method result
  (`Coverage coverage = fill.GetCoverage();`) — and for built-in types, because it reads better.
- **Readability over cleverness / micro-optimization.** Prefer a clear structure to a terse trick:
  - Use a real collection type for an edge/pair set (`HashSet<(int, int)>`), not a hand-packed
    `long` key.
  - Don't set an enum's underlying type (`: byte`) unless there's a real reason; the default `int`
    is clearer.
  - Spell out flag values (`1, 2, 4, 8`) instead of `1 << n` when the constants read plainly.

## 2. Naming

- **One public type per file**, file name = type name. (A single small file grouping a set of DTOs
  that only exist as one payload is the rare exception.)
- **Methods are verb + object** — they say what they *do*: `GetCoverage`, `BuildFillPolygon`,
  `FindRoot`, `ChooseAdjacentRegion`, `Merge`, `EnumerateShapes`. Avoid noun-only (`Union` →
  `Merge`) and vague (`Find` → `FindRoot`) names. This holds for **every** method, including
  `private` helpers and test data-builders — a method name is never a bare noun:
  - **Computed values prefer a property; a `Get`/`Compute` *method* is for the rest.** For a cheap,
    side-effect-free value, expose an idiomatic **property** with a noun name (`Area`, `Count`) — that
    is the .NET-guidelines default, not a `GetArea()` method. Reach for a `Get`/`Compute`-prefixed
    *method* when it is genuinely a method: non-trivial or expensive work, a possible side effect, it
    can fail, or it returns a fresh array/collection each call (`ComputeAngleFromReverse()`,
    `GetNeighbours()`). The prefix rule governs **methods**; it never forces a good property into a
    method.
  - **Factories** use `Create`/`Build`, including test builders: `Shape(...)` → `CreateShape(...)`.
  - **Predicates** use `Is`/`Are`/`Has`/`Can`: `RegionConnected(...)` → `IsRegionConnected(...)`.
  - *Idiomatic exceptions kept:* conversions (`ToDto`, `FromDto`, `CornerToWorld`), the RNG
    `Next*` family, and `Result.Success` / `Result.Failure` factories.
  - Test **method names** keep the `Scenario_Condition_Result` form (§5) — that convention
    overrides verb+object for the `[Test]` methods themselves, not for the helpers they call.
- **A method does what its name says — no more, no hidden side effects.** If an operation also
  mutates or triggers extra work, name the *whole* operation and let the body read as explicit, named
  steps (`ProcessMove` — which validates, places, merges, clears, scores and refills — not
  `PlacePiece`, which hides the rest). Don't smuggle a side effect into a narrowly-named method;
  either widen the name to the real operation or extract the step (`RefillTrayIfEmpty`).
- **Type names what it *is*; the member/parameter name carries the *role*.** If the type is
  `Coordinate`, the role lives in the member name: `Offset`, `Solution`, `origin`. Don't bake the
  role into the type name (`CellOffset` → `Coordinate`, used everywhere with role-named members).
- **Data members follow the same rules, made concrete.** A boolean reads as a predicate —
  `IsGameOver`, `HasClears`, `WasTrayRefilled` (`Is`/`Has`/`Are`/`Can`/`Was`) — never a bare past
  participle (`TrayRefilled`). A primitive that is really an identifier or index carries the suffix —
  `ActorId`, `TraySlotIndex` — not `Actor`/`TraySlot` for an `int`. A quantity names what it measures
  — `PointsScored`, `ClearedValue`, `SlotCount` — not a vague `TotalValue`/`Data`/`Info`.
- **Names must reflect the real algorithm / scope.** Name the concrete algorithm, not a family
  (`SplitMix64RandomNumberGenerator`, not `XorShift`); name the real granularity (`CellGeometry`,
  not `GridGeometry`, when it's per-cell); drop redundant noise (`PolygonLoop`, not `Polygon2D` —
  it's a single loop and `2D` says nothing). Avoid `Type`/`Info`/`Data` suffixes that don't say
  what varies (`CellType` → `CellShape`).
- **Descriptive variable names — no cryptic abbreviations.** Spell the intent out: `boardSize`
  not `n`, `itemCount` not `k`, `coverage` not `cov`, `neighbour` not `nb`, `rootA`/`rootB` not
  `ra`/`rb`, `bottomLeft` not `bl`. *Allowed shorthands:* `i`/`j` for loop counters and `x`/`y`
  for coordinates (domain-standard); `left`/`right` for binary-operator operands; `other` for the
  equality/`Equals` parameter.
- **Private instance fields use `_camelCase`** (`_cells`, `_state`). The leading underscore
  distinguishes a field from locals and parameters at a glance and removes any need for `this.`.
  `const`s and `static readonly` fields that read like constants stay `PascalCase` (`EmptyValue`,
  `OrthogonalDirections`).
- **`[Flags]` enums:** values are powers of two; aggregate with OR (`Full = Bottom | Right | Top |
  Left`).

## 3. Design / SOLID

- **Thin orchestrators + single-responsibility collaborators behind interfaces.** An orchestrator
  wires a sequence of small injected steps (`IPlanner` → `IPartitioner` → `IColorer` → `IBuilder`
  → `IValidator`), each swappable (OCP/DIP) and unit-tested in isolation. Small, focused
  interfaces (ISP); depend on abstractions (DIP); one responsibility per type (SRP).
- **Separate the data structure from the policy.** A generic union-find (`DisjointSet`) is
  independent of any particular "grow the smallest region" merging *policy* that uses it. The data
  structure doesn't know why it's being merged.
- **Composition over inheritance — the default, not a dogma.** Model behavioural variation first as
  an **injected interface**: the same call site drives different implementations because they're
  *composed in*, not subclassed. This is usually more flexible and more testable than a class
  hierarchy, and it keeps Liskov trivially satisfied — the substitutability contract lives on a small
  interface that implementations honour, rather than on a base class they might weaken. Inheritance is
  **not banned**: a genuine "is-a" with real shared behaviour, or a framework base type you must
  derive from, is a legitimate use — reach for it deliberately, then `seal` where you are not
  designing for further extension. Treat **`sealed` as the default and `abstract` as a considered
  choice**; a low count of abstract types is a *result* of composition winning, not a quota to hit.
  Sealing a concrete type does **not** violate the open/closed principle — extension happens through
  new interface implementations and injection, not through subclassing the concrete.
- **Prefer a `private` method over a closure-heavy local function.** When a method grows a named
  helper inside it — especially one that closes over several mutable locals — that shared state is
  usually the signal to extract: promote the helper to a `private` method, or, when the steps share
  real working state, encapsulate that state in a small single-purpose (often `private`, sometimes
  nested) class whose methods operate on fields rather than closures. **This is a bias, not a ban.** A
  local function is the right tool when it genuinely improves encapsulation — scoping a helper to its
  single caller instead of widening it to the whole type — or avoids a delegate allocation (a `static`
  local function), or guards arguments *before* an iterator/`async` body runs. Extract when the local
  function is really shared state in disguise; keep it when it earns its scope. *(Inline lambda
  **arguments** — a comparator passed to `Array.Sort`, a LINQ predicate — are always fine; this is
  about named local functions, not expressions.)*
- **Avoid premature abstraction (YAGNI).** Extract an interface or a helper only on a real
  single-responsibility or readability win, not "in case we need it". Keep a type next to its only
  consumer and promote it (e.g. to a shared `Common`) only when a second consumer actually appears.
- **A state machine only where control actually branches.** A straight, non-branching sequence —
  `validate → act → resolve → record` — is a pipeline; wrapping it in an explicit finite-state
  machine is ceremony, not design. Reserve a state machine for flow that genuinely has states and
  transitions (a turn that alternates, a screen that gates input, a protocol handshake), and then keep
  it minimal and explicit. "We might branch later" is YAGNI — add the machine when the branch is real.

## 4. Error handling & determinism

- **`Result` for expected failure; fail-fast `throw` for a broken invariant.** An *expected*,
  recoverable outcome — an invalid move, a value out of range — returns `Result` / `Result<T>` (a
  small cross-cutting type) that the caller must handle; validate defensively (e.g. clamp sizes)
  rather than throwing for these, and value objects trust their own internally-validated inputs. This
  is **not** a ban on exceptions: a genuinely *exceptional* condition — a violated invariant, an
  unreachable branch, a corrupt state that means the program already has a bug — should still
  fail-fast with a guard clause and `throw` (or an assert). Converting a real bug into a `Result` hides
  it. The distinction is *expected outcome* vs. *should-never-happen*, not "exceptions are banned."
- **Catch third-party exceptions at the boundary** (e.g. JSON parsing in a serializer, a network
  call in a provider) and convert them to `Result.Failure`. Exceptions from libraries don't leak
  past the adapter that calls the library.
- **Deterministic where it matters.** Any generation/simulation uses a **seeded RNG** — the same
  seed yields the same output. Refactors are behaviour-neutral unless a change is intended, which
  makes them testable by golden output.

A minimal `Result` shape (put it in a dependency-free `Common`/shared assembly):

```csharp
public readonly struct Result
{
    public bool IsSuccess { get; }
    public string Error { get; }
    private Result(bool ok, string error) { IsSuccess = ok; Error = error; }
    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }
    private Result(bool ok, T value, string error) { IsSuccess = ok; Value = value; Error = error; }
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
```

> Read `Value` only after checking `IsSuccess` — on a failure it is `default`. If a codebase prefers
> that enforced rather than conventional, have the `Value` getter throw when `!IsSuccess`; the shape
> above is the minimal version.

## 5. Testing & process

- **Every collaborator / data structure gets isolated tests.** Because collaborators are small and
  behind interfaces (§3), each is unit-tested on its own.
- **Validate headless first.** Run the pure tests with `dotnet test` before opening the editor /
  running the app, and keep the suite green after every change. This only works if the core stays
  framework-free and synchronous (see [`ARCHITECTURE.md`](ARCHITECTURE.md)).
- **Test method names use `Scenario_Condition_Result`** (e.g.
  `Generate_WithSeed_IsDeterministic`) — this is the one place a method name isn't verb+object.
- **Deliverable docs, code, and comments are in English** (day-to-day chat can be in any
  language). Keep the docs in sync: update the architecture doc after any structural change.

## 6. C# semantics that bite

Language-level traps that produce real bugs; none are style preferences. (The *allocation*-side
mechanics — boxing, closures, LINQ — are performance, and live in `PERFORMANCE.md` §8.)

- **Never call a virtual method from a constructor.** The override runs before the derived
  constructor body has initialised its fields (field *initializers* have run; the ctor body has
  not), so the override observes default values. Construction only constructs.
- **Struct copy semantics.** `list[0].hp = 5` doesn't compile (a `List<T>` indexer returns a
  *copy* — CS1612), and `var s = list[0]; s.hp = 5;` silently edits the copy. Unity's
  `transform.position.x = 5` fails for the same reason. The pattern is read–modify–write: take
  the copy, change it, assign it back. Arrays are the exception — element access is a direct
  reference, so `arr[0].hp = 5` works.
- **Every `+=` has a `-=`.** An object subscribed to a longer-lived (worst: `static`) event stays
  reachable — it is never collected, and the stale handler fires on a dead object. Subscribe and
  unsubscribe in symmetric places (ctor/`Dispose`, `OnEnable`/`OnDisable`); teardown ownership
  belongs to the composition root (`ARCHITECTURE.md` §6).
- **`const` is baked into the *calling* assembly** at compile time; changing a library `const`
  doesn't reach already-compiled callers until they recompile. A cross-assembly constant that may
  ever change is `static readonly`.
- **Static initialisation order is lazy and subtle.** A static ctor runs on first touch of the
  type; two types whose static fields reference each other initialise in whichever order they
  happen to be touched. Don't build dependency chains between static initialisers — one more
  reason §3 prefers instances wired at the composition root over statics.
- **Culture-sensitive string operations — the Turkish-i bug.** `"info".ToUpper()` on a Turkish-
  locale device yields `"İNFO"`: culture-default `ToUpper`/`ToLower`/`Equals`/`StartsWith` follow
  the OS locale and silently break identifier comparisons. For *machine* strings — localization
  keys, ids, file names, save fields — always use `ToUpperInvariant`/`ToLowerInvariant` and
  `StringComparison.Ordinal(IgnoreCase)`. Culture-aware comparison is only for text shown to the
  user. This project ships a `tr` locale — this bug is not hypothetical here.
- **Never mutate a collection inside its own `foreach`** — `Remove` during enumeration throws
  `InvalidOperationException` at runtime. Iterate backwards with an index `for` when removing,
  use `RemoveAll`, or collect-then-remove (a reused scratch list, `PERFORMANCE.md` §8).
- **Float division never throws — it poisons.** `0f/0f` is `NaN`, `x/0f` is `Infinity`, and
  `NaN != NaN`, so one bad division silently corrupts every downstream rating or probability
  with no exception at the source. Guard denominators at the boundary of a calculation (the sim
  already does: possession, line averages), and test suspect values with `double.IsNaN`/
  `IsInfinity` — never `== NaN`. Compare floats for "equality" with an epsilon, not `==`.

---

## Example transformations

These are the kinds of edits the conventions produce, kept as generic before → after pairs so the
*rule* is visible without tying it to any one codebase. Use them as a checklist during review.

| Rule (§) | Before | After | Why |
|---|---|---|---|
| Errors (§4) | `throw new InvalidOperationException(...)` in core logic | return `Result.Failure("...")` | Expected failure isn't exceptional; handle at the call site. |
| SOLID (§3) | one 400-line generator method | orchestrator + `IPlanner`/`IPartitioner`/`IColorer`/`IValidator` | Each step swappable and unit-tested in isolation. |
| SOLID (§3) | `partitioner` owns its own union-find internals | extract generic `DisjointSet`; partitioner holds the *policy* | Separate the data structure from the policy that drives it. |
| SOLID (§3) | a closure-heavy local function sharing mutable locals | a `private` method, or a small state-holding nested class | Extract when the local is shared state in disguise; keep local functions that genuinely earn their scope. |
| SOLID (§3) | a `sealed` concrete subclassed to vary behaviour | an injected interface with N implementations | Composition over inheritance by default; sealing doesn't violate OCP — extension is a new implementation. |
| Naming (§2) | `Union(a, b)` | `Merge(a, b)` | Methods are verb+object, never a bare noun. |
| Naming (§2) | `BoardArea()`, `WorldRect()` | `GetBoardArea()`, `GetWorldRect()` | Computed-value helpers take `Get`/`Compute`. |
| Naming (§2) | `RegionConnected(r)` | `IsRegionConnected(r)` | Predicates take `Is`/`Are`/`Has`/`Can`. |
| Naming (§2) | `Shape(...)`, `FullCellPiece(...)` (test builders) | `CreateShape(...)`, `CreateFullCellPiece(...)` | Factories — including test builders — use `Create`/`Build`. |
| Naming (§2) | `XorShiftRng` (wrong algorithm) | `SplitMix64RandomNumberGenerator` | Name the concrete algorithm, not a vague family. |
| Naming (§2) | `Polygon2D` (one loop) | `PolygonLoop` | Name the real granularity; drop redundant `2D` noise. |
| Naming (§2) | `CellOffset` (role baked into type) | `Coordinate` with a member named `Offset` | Type says what it *is*; the member carries the *role*. |
| Naming (§2) | `n`, `k`, `cov`, `ra`/`rb`, `bl` | `boardSize`, `itemCount`, `coverage`, `rootA`/`rootB`, `bottomLeft` | Descriptive names; keep only `i`/`j`, `x`/`y`, `left`/`right`, `other`. |
| Readability (§1) | `enum Coverage : byte { A = 1 << 0, ... }` | `enum Coverage { A = 1, B = 2, C = 4, D = 8 }` | Default `int` and explicit flag values read more plainly. |
| Style (§1) | brace-less single-statement `if`/`for` | braces on every block, Allman | Uniform, diff-friendly, enforced by `.editorconfig`. |
