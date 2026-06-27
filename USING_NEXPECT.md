# Using NExpect

NExpect is a fluent assertion library for .NET, inspired by Chai and Jasmine. Used in this project as the sole assertion framework for all test projects.

## Version

`NExpect 2.0.126` — the latest published version (targets `net462`, compatible with `net10.0`).

## Getting started

### Required using directives

Every test file **must** include **both** of these:

```csharp
using NExpect;
using static NExpect.Expectations;
```

- `using NExpect;` imports the namespace so that **extension methods** (`Equal`, `Contain`, `Match`, `Null`, `True`, etc.) are discoverable by the C# compiler.
- `using static NExpect.Expectations;` imports the static `Expect()` method itself.

`using static` alone does **not** import the enclosing namespace for extension method resolution. Without the separate `using NExpect;`, every fluent assertion fails with `CS1061` (extension method not found) or `CS1955` (non-invocable member used as method).

### How the fluent API works

The `Expect()` static method returns an `IExpectationContext<T>`. Calling `.To` or `.Not.To` progresses through a chain of continuation interfaces:

```
Expect(val) → IExpectationContext<T>
  .To       → ITo<T>
    .Equal(expected)       — method on ITo<T>
    .Be                    — property → IBe<T>
      .Null()              — extension method on IBe<T>
      .True()              — extension method on IBe<T>
      .False()             — extension method on IBe<T>
      .Empty()             — extension method on IBe<T> (collections)
  .Not.To   → IToAfterNot<T>
    .Equal(unexpected)     — method on IToAfterNot<T>
    .Be                    — property → IBe<T>
      .Null()
  .To.Contain              — property on ITo<T> returning IContain<T> (when T is string: IStringContain)
    .Exactly(n)            — extension method on IContain<T> (collections)
    .Any()                 — extension method on IContain<T> (collections)
    .Only(n)               — extension method on IContain<T> (collections)
  .To.Contain(substring)   — extension method on IStringTo — method call, NOT property
  .To.Match(pattern)       — extension method on IStringTo
  .To.Start.With(prefix)   — extension method on IStringTo
  .To.End.With(suffix)     — extension method on IStringTo
  .To.Throw<T>()           — extension method on ITo<Func<T>> or ITo<Action> (exception assertions)
```

**Key insight:** `Contain` acts as both a **property** (returning `IContain<T>` for chaining like `.Exactly(n)`) and as an **extension method** (taking an argument like `.Contain("substring")`). The compiler resolves based on whether arguments are present.

## Assertion patterns

All patterns below verified to compile and pass at runtime on `net10.0` with NExpect 2.0.126.

### Primitives

| Purpose | Syntax |
|---|---|
| Equality | `Expect(actual).To.Equal(expected)` |
| Negated equality | `Expect(actual).Not.To.Equal(unexpected)` |
| True | `Expect(condition).To.Be.True()` |
| False | `Expect(condition).To.Be.False()` |
| Null | `Expect(value).To.Be.Null()` |
| Not null | `Expect(value).Not.To.Be.Null()` |
| Null or empty | `Expect(value).Not.To.Be.Null.Or.Empty()` |

### Strings

| Purpose | Syntax |
|---|---|
| Equality | `Expect(str).To.Equal(expected)` |
| Contains | `Expect(str).To.Contain(substring)` |
| Starts with | `Expect(str).To.Start.With(prefix)` |
| Ends with | `Expect(str).To.End.With(suffix)` |
| Regex match | `Expect(str).To.Match(pattern)` — accepts `string` or `Regex` |
| Not empty | `Expect(str).Not.To.Be.Empty()` |
| Not null or empty | `Expect(str).Not.To.Be.Null.Or.Empty()` |

### Collections

| Purpose | Syntax |
|---|---|
| Exact count | `Expect(coll).To.Contain.Exactly(n)` |
| Exact count + Items() | `Expect(coll).To.Contain.Exactly(n).Items()` |
| Only n items | `Expect(coll).To.Contain.Only(n).Items()` |
| Empty | `Expect(coll).To.Be.Empty()` |
| Element matching predicate | `Expect(coll).To.Contain.Any().Matched.By(predicate)` |
| Exactly 1 equal to value | `Expect(coll).To.Contain.Exactly(1).Equal.To(expectedValue)` |
| Exactly 2 deep equal | `Expect(objects).To.Contain.Exactly(2).Deep.Equal.To(expected)` |
| At least 1 matching | `Expect(coll).To.Contain.At.Least(1).Matched.By(predicate)` |

### Exceptions

| Purpose | Syntax |
|---|---|
| Assert throws any | `Expect(() => doStuff()).To.Throw()` |
| Assert throws specific type | `Expect(() => doStuff()).To.Throw<InvalidOperationException>()` |
| Assert does not throw | `Expect(() => doStuff()).Not.To.Throw()` |
| Assert + exception message | `Expect(() => doStuff()).To.Throw().With.Message.Containing("error")` |
| Assert + specific type + message | `Expect(() => doStuff()).To.Throw<InvalidOperationException>().With.Message.Containing("error")` |
| Assert + exception property | `Expect(() => doStuff()).To.Throw<CustomException>().With.Property(e => e.MoreInformation).Equal.To("value")` |
| Assert + exception type (non-generic) | `Expect(() => doStuff()).To.Throw().With.Type(typeof(ArgumentException))` |

**Async exceptions** — same patterns work with async lambdas:

```csharp
// sync method
Expect(() => sut.Execute(config)).To.Throw<InvalidOperationException>();

// async Task method
Expect(async () => await sut.ExecuteAsync(config)).To.Throw<InvalidOperationException>();

// async Task<T> method (e.g. ExecuteAsync returning Task<SessionResult>)
Expect(async () => await sut.ExecuteAsync(config, signal))
    .To.Throw<InvalidOperationException>()
    .With.Message.Containing("error text");
```

When using `Expect(async () => await ... )`, NExpect awaits the returned task, catches any exception, and runs the assertion against it. The exception is caught internally — code after the assertion continues normally.

### Negation

Expectations can always be negated with `.Not.To` or `.To.Not`:

```csharp
Expect(value).Not.To.Equal(unexpected);
Expect(value).Not.To.Be.Null();
Expect(() => doStuff()).Not.To.Throw();
```

## Common pitfalls

1. **Missing `using NExpect;`** — The single biggest issue. Extension methods live in the `NExpect` namespace, and `using static NExpect.Expectations;` alone does not import it. Always add both usings.

2. **`Contain` method vs property** — `Expect("hello").To.Contain("el")` calls the extension method (correct). `Expect("hello").To.Contain` without arguments accesses the `IContain` property for chaining like `.Exactly(n)`. Never pass the argument to `.Contain` as a continuation — pass it directly as a method argument.

3. **`.Be.Null()` is a method call, `.Null` is a property** — Always use `()`: `Expect(x).To.Be.Null()`. Omitting the parentheses gives `CS1955` (non-invocable member). The same applies to `.True()`, `.False()`, `.Empty()`.

4. **Collection count with `.Exactly(n)`** — The chain is `Expect(coll).To.Contain.Exactly(n)`, not `.To.Be.Exactly(n)` or similar. Optionally append `.Items()` for readability.

5. **Exception assertions need a delegate, not a value** — Pass `() => expr` or `async () => await expr`, not the result of the expression. NExpect invokes the delegate and catches exceptions internally.

## Package reference

```xml
<PackageReference Include="NExpect" Version="2.0.126" />
```
