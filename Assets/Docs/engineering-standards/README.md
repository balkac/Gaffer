# Engineering Standards

A portable, project-agnostic set of engineering conventions I carry from one project to the
next, so every codebase reads and behaves the same way. Nothing here is tied to a specific
project — copy the whole folder into a new repo and start.

## What's in here

| File | Scope | What it is |
|---|---|---|
| [`CONVENTIONS.md`](CONVENTIONS.md) | **Any C# / .NET project** | Code style, naming, SOLID-in-the-small, error handling, testing & process. Applies with or without Unity. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | **Unity (and layered apps)** | Clean-architecture playbook: layers as assemblies (including pure adapter assemblies), package-by-feature, one-responsibility-per-folder, the async boundary + the synchronous-load guardrails, `Result` instead of `throw`, command→outcome boundary, single-owner ordering constraints, behavioural seams, content schemas. |
| [`PERFORMANCE.md`](PERFORMANCE.md) | **Unity** | Keeping it smooth and low-garbage: light-by-construction, pooling contracts, particle effects, budgets from a config asset, C# allocation mechanics, Boehm GC, API traps, device measurement + honest reporting, mobile rendering/build, procedural meshes, Addressables, canvas/TMP, memory verification. |
| [`UNITY.md`](UNITY.md) | **Unity** | Engine correctness: lifecycle/initialisation order, frame & time semantics, enable/disable/destroy, mobile app lifecycle, events & teardown, async/threading, persistence traps, inspector/asset hygiene, and the build- and device-only failure modes no editor run can reproduce. |
| [`GAME-FEEL.md`](GAME-FEEL.md) | **Unity / casual games** | The design-side standards: reference-pattern research *and how to measure it*, FTUE engineering, authored motion, input feel, budgeted juice, content difficulty as a regression gate — design defaults to validate by playtest. |
| [`starter-tree.md`](starter-tree.md) | **Unity** | An empty layered skeleton (assemblies + folders) to copy and start from, plus the `dotnet test` bridge for the pure layers. |
| [`REVIEW-CHECKLIST.md`](REVIEW-CHECKLIST.md) | **Any project using this set** | The enforcement surface: one screen of questions, each pointing at the section that owns the answer. Run per piece of work, at a gate, and when editing these docs. |
| [`COMMIT-CONVENTIONS.md`](COMMIT-CONVENTIONS.md) | **Any repo** | Conventional Commits adapted: required types, tree-derived scopes, imperative subjects, why-bodies, breaking-change footers. |
| [`.editorconfig`](.editorconfig) | **Any C#** | The mechanical rules (braces, `var`, Allman, import sorting), **tool-enforced** — Rider and `dotnet format` both read it. |
| [`.gitattributes`](.gitattributes) | **Unity** | Unity line-ending/merge template + the "store binaries inline, not in LFS" decision (see the note in the file). |

The split is deliberate: `CONVENTIONS.md` is pure code discipline and holds even in a plain
console library; everything Unity- or structure-specific lives in `ARCHITECTURE.md` and the
skeleton.

## How these docs are written

Four rules about the documents themselves. Each exists because breaking it has already cost us —
and [`REVIEW-CHECKLIST.md`](REVIEW-CHECKLIST.md)'s last section is where they get asked.

- **Every quotation carries the page it came from.** The set's habit is "a claim needs a number";
  its other half is "a quotation needs a source". An unattributed `"…"` reads as authoritative and
  is unfalsifiable — an audit of this set found four places where the *attribution* was wrong or
  missing while the underlying advice was fine, which is the harder error to catch. The same rule
  binds unquoted claims: **a claim never outruns its source** — "documented" only for the
  documented, "always" only for the unconditional, a number with its platform. Where a claim
  is a working model rather than a documented fact, it says so in place, and says what would
  settle it.
- **A practice endorsed *with conditions* carries its conditions as a visible checklist, next to
  the endorsement — never as prose inside it.** The reader who needs the conditions is the reader
  the paragraph just persuaded, and by then they are at the end of a long argument. A condition
  buried in a subordinate clause has no owner and gets skipped; `ARCHITECTURE.md` §5a is the
  worked example, extracted after exactly that happened.
- **Promoting prose to a list means restating it, not copying it.** A list item is read on its own,
  with none of the surrounding argument, and a reader acts on it directly — so a clause that is
  harmlessly loose inside a paragraph becomes an item that fires on correct work. Extracting §5a
  moved "never in `Awake`" into a checklist unchanged, where it would have flagged a correct
  *asynchronous* load; the paragraph had never needed the precision the item does. Raise the
  precision when you raise the prominence.
- **One canonical copy; everything else points at it.** When the same rule is stated in two
  documents, one of them becomes a partial copy and then a wrong one — this set had guardrails
  spread across two files and shipped a violation of them anyway. Decide which section owns a rule,
  and let the others carry only the mechanism or the consequence that belongs to *them*.

## Using it in a new project

1. **Copy the mechanical enforcers to the repo root** so the IDE picks them up:
   ```
   cp engineering-standards/.editorconfig   <new-project>/.editorconfig
   cp engineering-standards/.gitattributes  <new-project>/.gitattributes    # Unity projects
   ```
   `.editorconfig` starts working immediately in Rider / VS / `dotnet format` — the style rules
   below are then enforced, not just documented.

2. **Keep the docs where the team reads them.** Either copy `CONVENTIONS.md` (+ the Unity set:
   `ARCHITECTURE.md`, `PERFORMANCE.md`, `UNITY.md`, `GAME-FEEL.md`, `starter-tree.md`) into the
   project's `Docs/` folder, or link to this repo from the project README. **Bring
   `REVIEW-CHECKLIST.md` either way** — the set only holds if its rules get asked somewhere, and
   that file is the only place they are.
   If you copy them, fix the relative link at the top of `.editorconfig`'s comment to point at
   wherever `CONVENTIONS.md` landed.

3. **Scaffold from `starter-tree.md`** (Unity) — create the layer folders and `.asmdef`s first,
   with dependencies pointing inward, before writing any feature code.

4. **Read `ARCHITECTURE.md` §5 "The async boundary" before adding async** — it's the rule people
   forget, and it's what keeps the core headless-testable. If you load content synchronously,
   **§5a is not optional reading**: it is the guardrail checklist, and skipping its `Awake` item
   is how a project ships a boot deadlock that no editor run can reproduce.

## The one-paragraph version

Braces everywhere, Allman style, `var` only when the type is already apparent. One public type
per file, methods are **a verb or a verb phrase**, names say what a thing *is* (type) vs the
*role* it plays (member). Clean architecture with dependencies pointing inward, one assembly per
layer, package **by feature not by technical layer**, one responsibility per folder. The pure
core (Domain + Application) has **no framework dependency and no async** — expected failure
returns `Result` / `Result<T>` (fail-fast `throw` stays for broken invariants); async lives only
in the framework-coupled layers so the core stays headless-testable. (The no-async half is the
default profile for fully-local content, and `ARCHITECTURE.md` §5 says where it legitimately
bends — a game with real backend surfaces grows async orchestration in its outer flow; the pure
rules core stays synchronous either way.) Every collaborator gets isolated tests; validate
headless before opening the editor. Deliverable docs and code comments are in English.
