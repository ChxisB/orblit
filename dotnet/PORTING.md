# Porting Dart to C#

A port keeps the Dart shape and names. Change a design in its own commit, after
the port passes. A port is done when `PARITY.md` shows the two test counts equal
and the fixture test is green.

## Idiom table

Each row is a place where a test will not always catch the difference.

| Dart | C# | Trap |
| --- | --- | --- |
| `Map` and `Set` iterate in insertion order | `OrderedDictionary<,>` or a sorted key list | `Dictionary<,>` order breaks after a removal |
| `a % b` on ints is never negative | A `Mod` helper | C# `%` keeps the sign of `a` |
| `a % b` on doubles is never negative | `%`, then add `b` if the result is below zero | Same sign trap. Dart adds `b` inside `%`, and `TilingNoise.Wrap` does it by hand |
| `a / b` on ints gives a double | Cast one side to `double` | C# `/` on two longs truncates |
| `x.round()` rounds half away from zero | `Math.Round(x, MidpointRounding.AwayFromZero)` | `Math.Round` defaults to banker's rounding |
| `x.floor()` on NaN or infinity throws | Check `double.IsFinite`, then `(long)Math.Floor(x)` | The C# cast gives an unspecified value and no error |
| `jsonEncode(1.0)` gives `1.0` | A shared writer with invariant culture | `System.Text.Json` gives `1`, so byte goldens differ |
| `List.sort` | `OrderBy` when the order of equals matters | `Array.Sort` and `List.Sort` are not stable |
| `Float32List` | `float[]` or `Span<float>` | Keep the element type. `double` is not `float` |
| `int` is 64 bit and wraps | `long`, inside `unchecked` | Do not narrow to `int`. Hashes rely on the wrap |
| A sealed class and an exhaustive `switch` | An abstract record with sealed cases and a `switch` expression | C# does not check a class hierarchy for exhaustiveness. A test must |
| `Stream` and `async*` | `IAsyncEnumerable<T>` or `Channel<T>` | Pass a `CancellationToken` on every async method |
| `Isolate` | `Task.Run` or a thread | Messages were copies. Shared state now needs a lock |
| `vector_math_64` `Vector3` | `Orblit.VectorMath.Vector3`, a mutable class of doubles | `System.Numerics.Vector3` is float32. The Dart vector is written through (`color.x = 0`), so it stays a class and a copy is `Clone()` |
| A namespace and a type of one name | Rename the type | CA1724. `Orblit.Noise` holds `NoiseField`, not `Noise`, because a sibling namespace sees the namespace first |
| Optional positional parameters on an override | Repeat the default on every override | A call through the derived type gets no default otherwise |
| A `switch` over a table of cases | The `switch` expression, with `[SuppressMessage("Maintainability", "CA1502")]` and the reason `F2: a list of cases is data.` | CA1502 counts every arm |
| `StateError` | `InvalidOperationException` | Dart `isA<StateError>` matches subclasses. `Assert.Throws<T>` matches the exact type, so use `ThrowsAny` for a subclass |
| `ArgumentError` | `ArgumentException` | Same rule. A named subclass such as `ArgumentOutOfRangeException` fails `Throws<ArgumentException>` |
| `RangeError` | `ArgumentOutOfRangeException` | |
| `UnsupportedError` | `NotSupportedException` | |
| An id the C header declares unsigned | `ulong` for an entity, `uint` for a component | Dart `int` is signed. Keep the C type so a handle with the top bit set does not turn negative |
| An enum or method named `float32` or `int64` | Keep it, with `<NoWarn>CA1720</NoWarn>` and the reason | CA1720 objects to a name that is a type name |
| A Dart `class FooError implements Exception` | `FooException`, derived from `Exception` | CA1710 wants the suffix. The Dart `message` field is `Exception.Message` |
| `double.compareTo` and `sort` on doubles | A comparison that puts NaN last | Dart orders NaN above every number and `-0.0` below `0.0`. C# `CompareTo` puts NaN below and treats the zeros as equal |
| An instance method that reads no field | Keep it an instance method, with `[SuppressMessage("Performance", "CA1822", Justification = "...")]` | CA1822 wants `static`. Making it static changes every call site, which is a design change for its own commit |
| A method named `end`, `next`, `stop` or another Visual Basic keyword | Keep it, with `<NoWarn>CA1716</NoWarn>` and the reason | CA1716 is about Visual Basic callers, and one can write `[End]` |
| A class that has factory constructors and `implements` | An abstract class with static `Create` and `Off` members | An interface cannot hold a constructor |

## Fixtures

A package with numbers in it gets an exporter in `tool/parity/bin/<package>.dart`.
It calls the Dart code and writes `dotnet/fixtures/<package>/<name>.json`, one
case per line, stamped with the commit that last changed the package.

- Run `tool/regen_parity.sh` to rewrite them and see the diff.
- A fixture changes only when the Dart changes. Review the diff as a change to
  the contract.
- A fixture that is a whole JSON snapshot is compared with `JsonCompare.FirstDifference`, which applies the tolerance to every number.
- A test project copies its fixtures beside the assembly. Add a `Content` item
  to its csproj, as `Orblit.Noise.Tests` does.
- Integers and bytes compare with plain equality. Doubles compare with
  `Tolerance.Close` in `Orblit.Parity`.

## Tests

- Keep each Dart test as one C# test, with the same name in PascalCase.
- A Dart loop that declares tests becomes a `[Theory]` over the same names.
- Tests the port adds, such as a fixture check, are counted apart in
  `PARITY.md`.
