# Commit Conventions — Conventional Commits, adapted

Adapts the [Conventional Commits v1.0.0 specification](https://www.conventionalcommits.org/en/v1.0.0/)
(type list extended per the [Angular convention](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#-commit-message-format))
for this doc set's projects; the adaptation shape follows
[cosgunhalil/HannibalUI's conventions](https://github.com/cosgunhalil/HannibalUI). Where this
document is silent, the upstream spec is authoritative.

## Format

```
<type>(<scope>)<!>: <Description>

<body>

<footer(s)>
```

- `type` — required.
- `(scope)` — required whenever one fits; **omit rather than invent** (see Scopes).
- `!` — only for a breaking change.
- `Description` — required, one line: **imperative mood** (`Add`, `Fix`, `Remove` — not `Added`,
  `Fixes`), first letter capitalized, no trailing period, short enough to scan in
  `git log --oneline`.
- `body` — optional, separated by one blank line: explains **why** (context, constraint,
  trade-off), never a restatement of the diff. Most small commits need none.
- `footer(s)` — optional, `Token: value` form (`Closes: #42`, `Refs: TDD §5.1`). Multi-word
  tokens use a hyphen. No tool-generated co-author/attribution trailers (house rule).

## Types

| Type | Use for |
|---|---|
| `feat` | A new capability or public surface |
| `fix` | A bug fix |
| `refactor` | Restructuring with no behaviour change |
| `perf` | A change whose primary purpose is performance |
| `test` | Adding/correcting tests only |
| `docs` | Documentation only |
| `chore` | Maintenance with no runtime effect (renames, .gitignore, dependency bumps) |
| `style` | Formatting-only changes |
| `build` | Build/project setup: `.csproj`, `.asmdef`, ProjectSettings, packages |
| `ci` | CI/automation config |

For a versioned package, types map to SemVer: `fix` → PATCH, `feat` → MINOR, `!` /
`BREAKING CHANGE:` → MAJOR. In an app repo the mapping is informational.

## Scopes

A scope is a **lowercase, singular noun that maps to the project tree** — layers and feature
folders, not invented concerns. If a commit spans the whole repo, omit the scope. Never coin
one-off scopes (`typo`, `setup`, `misc`).

The worked table for a layered Unity project (Orikami's):

| Scope | Maps to |
|---|---|
| `domain` / `app` / `infra` / `presentation` / `composition` | the architecture layers |
| `board`, `fold`, `obstacle`, `ftue`, `hud`, `level` | feature folders |
| `test` | the test bridge + EditMode tests |
| `build` | `.csproj`, `.asmdef`, ProjectSettings, packages |
| `docs` | design/tech docs; `docs(standards)` = this standards set |

A new project derives its own table the same way when copying this doc; keep it in this section.

## Breaking changes

`!` before the colon for a short break; add a `BREAKING CHANGE:` footer with the migration path
when callers need more than the description.

```
refactor(app)!: Return MoveOutcome from ProcessMove

BREAKING CHANGE: ProcessMove no longer mutates BoardState in place;
callers replay the returned outcome instead of re-reading the board.
```

## Examples

```
docs(standards): Reverify banners for Unity 6000.3.20f1
fix(fold): Restore mesh bounds after rebake
feat(ftue): Add guided first-tap lesson flow
test(level): Cover ftueTap import validation
build(test): Pin LangVersion to C# 9
```

## Exceptions

Auto-generated merge commits (`Merge branch ...`, `Merge pull request ...`) are exempt.
Everything else — including doc-only and config-only commits — follows this format.
