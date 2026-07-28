# Starter Tree — layered Unity skeleton

An empty skeleton to copy when starting a new Unity project. Create the layer folders and their
`.asmdef`s **first**, with references pointing inward, before writing any feature code. Replace
`MyGame` with your project name throughout. See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the rules
this encodes.

```
Assets/_Project/Scripts/
  Common/            MyGame.Common          (no deps)              — Result, Result<T>, generic primitives
  Domain/            MyGame.Domain          → Common               — pure C#, no UnityEngine; Result for expected failure (CONVENTIONS §4)
    <Model>/         (value objects that form one model = one folder)
    <Geometry>/
  Application/       MyGame.Application     → Domain, Common       — pure & synchronous, headless-testable
    <Feature>/                              — orchestrator at the feature root…
    <Feature>/<Step>/                       — …collaborators behind interfaces, one concern per folder
    Serialization/                          — ISerializer port + wire DTOs + DTO→domain mapping (consumer-owned contract; pure)   (only if you serialize)
  Infrastructure/    MyGame.Infrastructure  → Application, Domain, Common, (UniTask), UnityEngine
    Loading/                                — async ports (ILevelProvider : UniTask<Result<T>>), providers, fallback chain   (only if you have async I/O)
    Serialization/                          — the CONCRETE serializer adapter (JsonUtility / a lib) behind the Application port
    Configuration/                          — ScriptableObject configs
  Presentation/      MyGame.Presentation    → Application, Domain, UnityEngine
    <Feature>/Views/                        — MonoBehaviour views
    <Feature>/Layout/                       — pure layout value objects
    Input/Pointer/   Input/Picking/  Input/Snapping/
    Configuration/                          — feel/tuning ScriptableObjects
  Composition/       MyGame.Composition     → all above            — manual composition root, no DI framework
    GameBootstrapper (entry + serialized assets), GameServices (wiring), <Flow>Controller
  Editor/            MyGame.Editor          → Common, Domain, Application, Presentation
  Tests/
    EditMode/        MyGame.Tests           → Domain, Application, Common   — pure, run headless via dotnet
    EditModeUnity/   MyGame.Tests.Unity     → + UnityEngine                 — Unity Test Runner only
```

## `.asmdef` reference rules

The reference list in each `.asmdef` is what enforces the inward-pointing arrows — get these right
and the compiler prevents architecture drift:

| Assembly | References (and nothing outward) |
|---|---|
| `MyGame.Common` | — |
| `MyGame.Domain` | `MyGame.Common` |
| `MyGame.Application` | `MyGame.Domain`, `MyGame.Common` |
| `MyGame.Infrastructure` | `MyGame.Application`, `MyGame.Domain`, `MyGame.Common` (+ `UniTask` only if async) |
| `MyGame.Presentation` | `MyGame.Application`, `MyGame.Domain` (+ `MyGame.Common`) |
| `MyGame.Composition` | all of the above |
| `MyGame.Editor` | `MyGame.Common`, `MyGame.Domain`, `MyGame.Application`, `MyGame.Presentation` |
| `MyGame.Tests` | `MyGame.Domain`, `MyGame.Application`, `MyGame.Common` |

Keep `MyGame.Domain` and `MyGame.Application` **without any UnityEngine reference** — that's what
lets their tests run under plain `dotnet`. Mark them "no engine references" in the `.asmdef` where
the option exists.

`UniTask`, the `Loading/` async ports and `Serialization/` are **there only if you need them** — a
fully synchronous project (no async I/O, no serialization) simply omits them, and the async-boundary
rule (`ARCHITECTURE.md` §5) still holds by having nothing to push out. Don't scaffold layers you have
no work for.

## Running the pure tests under `dotnet`

The point of keeping Domain + Application framework-free is to test them **without opening the editor**.
The clean way to get *one* set of test files that both the Unity Test Runner and `dotnet test` compile
is a small SDK-style `.csproj` at the repo root (outside `Assets/`) that **`<Compile Include>`s the
same `.cs` files** the EditMode asmdef builds:

```xml
<!-- tests/MyGame.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Pin to the engine's C# level (Unity 6 = 9.0) so syntax Unity can't compile fails HERE first. -->
    <LangVersion>9.0</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <!--
    Single source of truth: the pure layers AND the EditMode tests compile here from the SAME .cs
    files the Unity Test Runner builds (asmdef MyGame.Tests). This catches a large class of
    divergence — framework references, and most syntax via the LangVersion pin — but it is NOT a
    complete Unity-compatibility guard: net8.0 still exposes BCL APIs Unity's runtime lacks, and
    desktop NUnit is broader than Unity's NUnit integration. Unity compilation + EditMode stay a
    required gate. Only framework-free production code is included; anything touching UnityEngine
    (Infrastructure, Presentation, Composition, Editor) stays OUT — which also *enforces* that these
    layers remain framework-free (they wouldn't compile here if they weren't). Because both compilers
    build the same test files, tests must use the public API only — no reliance on `internal` members.
  -->
  <ItemGroup>
    <Compile Include="..\Assets\_Project\Scripts\Common\**\*.cs" />
    <Compile Include="..\Assets\_Project\Scripts\Domain\**\*.cs" />
    <Compile Include="..\Assets\_Project\Scripts\Application\**\*.cs" />
    <Compile Include="..\Assets\_Project\Scripts\Tests\EditMode\**\*.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
</Project>
```

`dotnet test tests/` then runs the whole pure suite in seconds, in CI, with no Unity install — and if
someone accidentally pulls `UnityEngine` into Domain or Application, this project stops compiling, so
the framework-free boundary is checked, not just documented. Treat it as the fast **inner loop** of a
three-layer validation, not the whole story: (1) `dotnet test` per change; (2) Unity compilation +
EditMode tests as the compatibility gate (they catch Unity-absent BCL APIs and NUnit-surface
differences the bridge cannot); (3) device smoke/profile runs for platform-risk work (`PERFORMANCE.md`
§11, `UNITY.md` §7).

## Example `.asmdef`

```json
{
  "name": "MyGame.Application",
  "references": ["MyGame.Domain", "MyGame.Common"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "noEngineReferences": true
}
```

## First steps in a new repo

1. Copy `.editorconfig` and `.gitattributes` to the repo root (they live at the SOURCE repo's root,
   not inside this docs folder — bring them along whenever this folder is copied to a new project;
   `.editorconfig` is the enforcement mechanism `CONVENTIONS.md` §1 relies on).
2. Create the folder + `.asmdef` skeleton above (empty layers are fine).
3. Add `Result` / `Result<T>` to `Common` first (everything else returns them).
4. Add the root `tests/MyGame.Tests.csproj` bridge above, then write one Domain value object + its
   test and run `dotnet test tests/` to confirm the headless path works before opening the Unity
   editor.
5. Read the companion docs before feature work: [`ARCHITECTURE.md`](ARCHITECTURE.md) (layers,
   seams), [`CONVENTIONS.md`](CONVENTIONS.md) (style, C# traps), [`UNITY.md`](UNITY.md) (engine
   correctness), [`PERFORMANCE.md`](PERFORMANCE.md) (cost discipline), and
   [`GAME-FEEL.md`](GAME-FEEL.md) (UX craft) — they cross-reference each other by section number.
   Commits follow [`COMMIT-CONVENTIONS.md`](COMMIT-CONVENTIONS.md) (Conventional Commits; derive
   the scope table from your tree).
