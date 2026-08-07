// The marker type the C# 9 `init` accessor compiles against.
//
// Every settings/balance object in this assembly is `{ get; init; }` — immutable after construction,
// so `X.Default` can be one cached shared instance without becoming shared *mutable* state (see
// SettingsDefaultContractTests for the convention and why it is a convention).
//
// `init` needs `System.Runtime.CompilerServices.IsExternalInit` to exist: the compiler emits it as a
// required modifier on the setter's signature. It ships in the .NET 5+ BCL, which is why the headless
// `dotnet test` bridge (net8.0) finds it — but Unity 6 compiles this assembly against .NET Standard 2.1
// (`apiCompatibilityLevel: 6`, ref assembly
// `Unity.app/Contents/Resources/Scripting/NetStandard/ref/2.1.0/netstandard.dll`), which does NOT define
// it. Without this file the pure core builds green under `dotnet test` and fails in the Editor with
// CS0518 — the exact divergence the test bridge exists to prevent. Declaring the type in source is the
// documented workaround and costs nothing at runtime: it is only ever read from metadata.
//
// Only the assembly that *declares* init accessors needs it; assemblies that merely assign init-only
// properties in an object initializer (Infrastructure's balance SOs, the test assemblies) do not. Should
// Domain or Common ever take an `init` property, they need their own copy — `internal` deliberately does
// not leak across assembly boundaries, so each one states its own dependency.
//
// The guard keeps the net8.0 bridge from seeing two definitions of the same type (CS0436).
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
