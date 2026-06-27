# Using NExpect

NExpect is a fluent assertion library for .NET, used in this project as the sole assertion framework for all test projects.

## Version

`NExpect 2.0.126` — the latest published version (targets `net462`, compatible with `net10.0`).

## Critical: Required using directives

Every test file **must** include **both** of these:

```csharp
using NExpect;
using static NExpect.Expectations;
```

- `using NExpect;` imports the namespace so that **extension methods** (`Equal`, `Contain`, `Match`, `Null`, `True`, etc.) are discoverable by the C# compiler.
- `using static NExpect.Expectations;` imports the static `Expect()` method itself.

`using static` alone does **not** import the enclosing namespace for extension method resolution. Without the separate `using NExpect;`, every fluent assertion fails with `CS1061` or `CS1955`.

## Assertion syntax: verified working patterns

All patterns below were verified to compile and pass at runtime on `net10.0` with NExpect 2.0.126.

| Purpose | Syntax |
|---|---|
| Equality (value/string) | `Expect(actual).To.Equal(expected)` |
| Negated equality | `Expect(actual).Not.To.Equal(unexpected)` |
| Null | `Expect(value).To.Be.Null()` |
| Not null | `Expect(value).Not.To.Be.Null()` |
| True | `Expect(condition).To.Be.True()` |
| False | `Expect(condition).To.Be.False()` |
| String contains | `Expect(str).To.Contain(substring)` |
| String regex | `Expect(str).To.Match(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")` |
| Not empty (string) | `Expect(str).Not.To.Be.Empty()` |
| Collection count | `Expect(coll).To.Contain.Exactly(n)` |
| Collection element | `Expect(coll).To.Contain.At.Least(1).Matched.By(predicate)` |

## How the fluent API works

The `Expect()` static method returns an `IExpectationContext<T>`. Calling `.To` or `.Not.To` progresses through a chain of continuation interfaces:

```
Expect(val) → IExpectationContext<T>
  .To       → ITo<T>
    .Equal(expected)       — extension on ITo<T> (EqualityProviderMatchers)
    .Be                    → IBe<T>
      .Null()              — extension on IBe<T> (TruthMatchers)
      .True()              — extension on IBe<T> (TruthMatchers)
      .False()             — extension on IBe<T> (TruthMatchers)
  .Not.To   → IToAfterNot<T>
    .Equal(unexpected)     — extension on IToAfterNot<T>
    .Be                    → IBe<T>
      .Null()
  .To.Contain              — property on ITo<string> returning IStringContain
    .Exactly(n)            — extension on ICollectionContain<T> (CollectionMatchers)
  .To.Contain(substring)   — extension on IStringTo (StringMatchers) — method call, NOT property
  .To.Match(pattern)       — extension on IStringTo (StringMatchers)
```

The key insight: `Contain` acts as both a **property** (returning `IStringContain`/`ICollectionContain`) and as an **extension method** (taking an argument). The compiler resolves based on whether arguments are present.

## Common pitfalls

1. **Missing `using NExpect;`** — This is the single biggest issue. Every assertion extension method lives in the `NExpect` namespace, and `using static NExpect.Expectations;` alone does not import it. Always add both usings.

2. **`Contain` method vs property** — `Expect("hello").To.Contain("el")` calls the extension method (correct). `Expect("hello").To.Contain` without arguments accesses the `IStringContain` property — if you then try to chain `.And("el")`, it fails because `And()` expects `IStringPropertyContinuation`, not `IStringContain`. Always pass the argument directly to `Contain()`.

3. **`.Be.Null()` is a method call, `.Null` is a property** — Always use `()`: `Expect(x).To.Be.Null()`. Omitting the parentheses gives a compile error (`CS1955` on a non-invocable member).

4. **Collection count uses `.Exactly(n)` after `.Contain`** — The chain is `Expect(coll).To.Contain.Exactly(n)`, not `.To.Be.Exactly(n)` or similar.

5. **No equivalent for all NUnit patterns** — Some NUnit-specific patterns like `Assert.ThrowsAsync<T>` are not available in NExpect. Fall back to `Assert.ThrowsAsync<T>` (from NUnit) for exception-throwing assertions only; use NExpect for everything else.

## Package reference

Test projects reference NExpect via NuGet. No explicit `PackageReference` is needed in the solution's `Directory.Packages.props` or individual test `.csproj` files if central package management is configured.

```xml
<PackageReference Include="NExpect" Version="2.0.126" />
```
