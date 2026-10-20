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
| `x.round()` rounds half away from zero, and throws on NaN or infinity | `Doubles.Round(x)` | `Math.Round` defaults to banker's rounding, and the cast of a non-finite value gives no error |
| `x.floor()` on NaN or infinity throws | Check `double.IsFinite`, then `(long)Math.Floor(x)` | The C# cast gives an unspecified value and no error |
| `jsonEncode(1.0)` gives `1.0` | A shared writer with invariant culture | `System.Text.Json` gives `1`, so byte goldens differ |
| `List.sort` | `OrderBy` when the order of equals matters | `Array.Sort` and `List.Sort` are not stable |
| `Float32List` | `float[]` or `Span<float>` | Keep the element type. `double` is not `float` |
| `int` is 64 bit and wraps | `long`, inside `unchecked` | Do not narrow to `int`. Hashes rely on the wrap |
| A sealed class and an exhaustive `switch` | An abstract record with sealed cases and a `switch` expression | C# does not check a class hierarchy for exhaustiveness. A test must |
| `Stream` and `async*` | `IAsyncEnumerable<T>` or `Channel<T>` | Pass a `CancellationToken` on every async method |
| `Isolate` | `Task.Run` or a thread | Messages were copies. Shared state now needs a lock |
| `vector_math_64` `Vector3` | `Orblit.VectorMath.Vector3`, a mutable class of doubles | `System.Numerics.Vector3` is float32. The Dart vector is written through (`color.x = 0`), so it stays a class and a copy is `Clone()` |
| A namespace and a type of one name | Rename the type | CA1724. `Orblit.Noise` holds `NoiseField`, not `Noise`, and `Orblit.Sequence` holds `CutSequence`, because a sibling namespace sees the namespace first |
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
| `num.clamp` on a double | `Doubles.Clamp` | Dart orders by `compareTo`, so a NaN clamps to the upper limit. `Math.Clamp` returns NaN |
| A `switch` over an enum with every case named | Add `_ => throw new UnreachableException()` | Dart knows the enum is closed. C# accepts a cast integer, and CS8509 is an error here |
| A Dart enum with a symbol and a method | A sealed class with static instances and a `Values` list | An enum cannot hold a method body per case. `Values` stands in for `Enum.values` |
| A JSON tree typed `Object?` | `IReadOnlyDictionary<string, object?>` for a map, `IReadOnlyList<object?>` for a list, `int` or `long` for a whole number, `double` for the rest | Keeps the Dart cast sites as `is` patterns |
| A record typedef such as `Place` | A `readonly record struct` | Value equality comes free and it does not allocate |
| `%` on a double, `floor()` on a double | `Doubles.Mod`, `Doubles.Floor` | Dart `%` is never negative and `floor()` throws on NaN or infinity. C# `%` keeps the sign and `Math.Floor` passes NaN through |
| A library-private base class that public classes extend | A public abstract class with an `internal` constructor | CS0060 refuses a public class over an internal base. The internal constructor stops outside subclasses |
| Top-level functions | A static class | Name it so it does not clash with the namespace, `Effects` in `Orblit.Effect` (CA1724) |
| A function value as a default (`Ease ease = Eases.outQuad`) | `Ease? ease = null`, then `ease ?? Eases.OutQuad` | A method group is not a compile-time constant, so it cannot be a default |
| A Dart type named like its package (`Effect`, `Audio`) | Rename the type, `TimedEffect`, `AudioPlayer` | CA1724 again, for the type that would shadow the namespace |
| `vector_math` `Vector4`, `Quaternion`, `Matrix3`, `Matrix4` | The same names in `Orblit.VectorMath`, grown a member at a time as a port needs one | A zero-length axis in `Quaternion.AxisAngle` gives the zero quaternion, as Dart does, not the identity |
| An `abstract interface class Foo` | `interface IFoo` | The `I` prefix is the .NET rule. `AudioBackend` is `IAudioBackend` |
| A `base mixin` | An abstract class with a `private protected` constructor | `Motion` in `Orblit.Audio`. Only a class in the same assembly can derive from it |
| A value class whose `==` compares doubles | A class with its own `Equals` that uses `==` | A `record` compares with `Equals`, which treats two NaNs as equal. Dart `==` does not. `Level` is a class for this reason |
| A top-level `const` | A `const` in a static class | `Acoustics.SpeedOfSound` |
| A `const` constructor with default values | Optional parameters, called by name | The class stays sealed and immutable |
| More than four named parameters | The same parameters, called by name, with a one-line `// F2:` comment at the constructor | The Dart named parameters count as the options object. A C# call by name does the same job |
| A `LinkedHashMap` entry removed then added | `OrderedDictionary.Remove(key, out value)`, then the setter | Removing first moves the entry to the end, in both languages. Assigning to a key that exists keeps its place |
| A Dart record `({Steering behaviour, double weight})` | A tuple `(Steering Behaviour, double Weight)` | Names change case, and a caller builds it positionally |
| `T? read<T>(key)` on a map of `Object?` | `T? Read<T>(key)`, which gives `default` for a missing key | For a value type that is zero or false, where Dart gives null. The type stored must match the type read, so a `long` is read as `long`, never `int` |
| A map keyed by node object, with `const` nodes | `Dictionary<Node, object?>`, which keys by reference | Dart makes two equal `const` nodes one object, so they share a key there. Two `new` nodes never do |
| A type named `Do` | `<NoWarn>CA1716</NoWarn>` in the csproj, with a comment | CA1716 flags a Visual Basic keyword. The Dart name stays |
| `double.toString()` in a label | `Labels.Number`, which adds `.0` to a whole number | .NET switches to `E` notation at 1e15 and below 1e-4, so the two agree only from 0.0001 up to below 1e15 |
| A method over ten branches | Split it into private methods, one per step | CA1502 counts every `if`, `foreach` and `&&`. `CueBoard.Update` became `Loudest`, `FadeOut`, `Follow` and `Start` |
| A `LinkedHashSet` | `OrderedDictionary<T, bool>`, used for its keys, with a comment | .NET has no ordered set. A `HashSet` forgets the order things were added in |
| `x.sign` on a double | `Doubles.Sign` | Dart gives back `-0.0`, `0.0` and NaN unchanged. `Math.Sign` gives an int and throws on NaN |
| `a ~/ b` and `a % b` on ints that may be negative | `/` for the quotient, and the remainder fixed by hand when it is below zero | `/` truncates in both languages. C# `%` keeps the sign of `a`, where Dart's never goes negative |
| A parameter or property named `is_` | `@is` for the parameter, `Is` for the property | `is` is a C# keyword. The Dart trailing underscore only dodges the same clash |
| A member named like its class, `Box.box` | Drop it and keep the member that says the same, `Bounds` | CS0542. Say so in the `CHANGELOG.md` |
| An override whose parameter has a new name | Rename the base parameter to match | CA1725 wants the same name all the way down. Dart renames freely |
| A bug the Dart has, found while porting | Port it as it is, say so in the `CHANGELOG.md`, and fix it in its own commit after the port passes | `World2` pairs ids with `a * 1000003 + b`, which mis-pairs ids outside 0 to 1000002. The fixture holds odd ids, so a fix on one side shows as a failure |
| A generic `T?` where `T` may be a value type | `TryX(out T)` for a result, and a `Slope<T>?` wrapper struct for a parameter | `T?` on an unconstrained generic means two things in C#, and neither is Dart's. `PropertyTrack<T>.TryValueAt` and `Key<T>.SlopeIn` are the two cases |
| `const` instances, `doubleMixer` and `boolMixer` | Static readonly fields on a static class, `Mixers.OfDouble` | A member named `Double` risks CA1720. The `Of` prefix keeps the four findable together |
| `sync*` and `yield` | An iterator method returning `IEnumerable<T>` | It runs when enumerated, not when called, in both languages. A refusal before the first `yield` also waits |
| `assert` in a constructor | An `ArgumentException` that always runs | Dart skips asserts in a release build. The exporter must never build an invalid object, because Dart may not refuse it. The C# tests cover the refusals apart |
| A name with a trailing underscore to dodge a keyword, `Easing.in_` | Drop the underscore, `Easing.In` | A fixture spells the Dart name, so its reader strips the underscore and ignores case |

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
- A double that may be NaN or infinite is written with `number()` in `tool/parity/lib/fixture.dart`, which turns it into a string JSON can hold, and read with `Fixture.Number`.
- Integers and bytes compare with plain equality. Doubles compare with
  `Tolerance.Close` in `Orblit.Parity`.

## Tests

- Keep each Dart test as one C# test, with the same name in PascalCase.
- A Dart loop that declares tests becomes a `[Theory]` over the same names.
- Tests the port adds, such as a fixture check, are counted apart in
  `PARITY.md`.
