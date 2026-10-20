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
| `List.sort` | `OrderBy` when the order of equals matters | `Array.Sort` and `List.Sort` are not stable. Dart's is stable up to 33 items and a dual pivot quicksort above that, so equals come out in an order `OrderBy` does not give. A comparer that is a total order does not care |
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
| `map.putIfAbsent(k, () => draw())` | `if (!map.ContainsKey(k))`, then `Add` | The Dart value is built only for a new key. `TryAdd(k, draw())` builds it every time, so a random draw happens on every call and the sequence drifts |
| `jsonEncode` of text | A hand-written quoter | Dart escapes only the quote, the backslash, control characters and a lone surrogate. `System.Text.Json` escapes `<`, `>`, `&`, `+` and every non-ASCII character, so the bytes differ |
| `jsonDecode` of a string with a lone surrogate | Unescape `GetRawText()` by hand | Dart keeps the surrogate. `JsonElement.GetString` throws `InvalidOperationException` |
| A `Future` that is already done | `await Task.Yield()` first, where the caller holds a lock | Dart delivers the completion later, in a microtask. A C# `await` of a finished task goes on at once, so an answer can arrive inside the call that asked, and inside its lock |
| `Future.delayed(Duration.zero)` to let a task run | `Support.Until(condition, what)` | Poll a condition. A fixed delay is a flake. A negative check, that nothing happened, still needs a short `Task.Delay` |
| A regex `$` | `\z` | .NET `$` also matches before a final newline, so `ana\n` passes a key check |
| `x.toStringAsFixed(n)` | `Doubles.ToStringAsFixed(x, n)` | Dart rounds the exact value with ties away from zero, so 115.25 is `115.3`. .NET `F1` goes to even and gives `115.2`. From 1e21 Dart gives the `toString` form, such as `1e+22`. A negative that rounds to zero keeps the sign, as in `-0.000`. Digits outside 0 to 20 throw |
| `copyWith(...)` on an immutable value | `with { ... }` on a record | A positional record with defaults stands in for a class with named parameters, so a call names what it sets |
| `Quaternion.euler`, `conjugate` and `normalize` from `vector_math` | `Quaternion.Euler`, `Conjugate` and `Normalize` | `conjugate` negates x, y and z in place. `normalize` returns the old length and multiplies by the reciprocal. `Vector3.Normalize` returns 0 and leaves a zero vector alone |
| `BlendTable.between(null, null)` | The same lookup, kept | Dart looks a null name up as the text `null` while `set` stores `*`, so a `*->*` rule is never found. The port keeps it and the fixture covers it |
| A Dart `enum` in an exhaustive `switch` | A `switch` with a default arm that throws `InvalidOperationException` | A C# enum takes any number. The default arm is the only way to refuse one, and it needs a C#-only test |
| `num.clamp(a, b)` with `a > b` | `Doubles.Clamp` throws `ArgumentException` | Dart throws `ArgumentError`. Tests use `Record.Exception` and an `is ArgumentException` check, since `Assert.Throws` matches the exact type |
| A Dart abstract class that only describes behaviour | An interface with an `I` prefix | `ICameraBody` and `ICameraTarget`. The classes that implement them keep the Dart names |
| `jsonDecode` and `jsonEncode` | `DartJson.Decode` and `DartJson.Encode` in `Orblit.Json` | `System.Text.Json` reads `-0` as a double, takes a repeated key at its last place and has no refusal for `1e999`. `Orblit.Json` keeps Dart's reading and writes only what a package has needed |
| A fixture int past 2^53 | `big()` in the exporter writes it as a string, and the test reads it with `long.Parse` | `JsonCompare` reads numbers as doubles, so an int past 2^53 loses its low bits and a wrong answer can compare equal. A string compares exactly |
| A refusal in a fixture | Call the Dart code and record `{'throws': true}`, never write it by hand | A hand-written refusal passes in C# whatever Dart does. A Dart `assert` does not run under `dart run`, so a case that only an assert refuses must be a C#-only test |
| `utf8.decode` of a file header | `Encoding.UTF8.GetString`, after dropping one leading U+FEFF | Dart strips a byte order mark. .NET keeps it as a character, and the header then fails to parse |
| A `Float32List` holding NaN, written to a file | Compare the decoded numbers, not the bytes | The payload bits of a NaN depend on the CPU. Dart on arm64 writes `0x7FC00000`, and C# `(float)double.NaN` writes `0xFFC00000`, so a region with a NaN height is not byte-stable between machines |
| A seeded `Random(seed)` | `System.Random(seed)`, and a C# test that a seed repeats | The two generators differ and the Dart one is not ported (decision 77). A fixture cannot hold a jittered stroke |
| A value class with named optional parameters and `copyWith` | A sealed class with a primary constructor, `IEquatable`, and `CopyWith(double? size = null, ...)` | A call still names what it sets. A record would work, but `Brush` has defaults that need checking in the constructor |
| An `enum` that carries data | A C# enum plus an extension class, `BrushTools.Layer(tool)` | A C# enum has no members of its own |
| A top-level function or a global | A static method on the type it serves (`TwoBoneIk.Reach`, `SplineIk.SampleCurve`), or a static class (`Ik`, `BoneEdit`) | C# has no free functions. `_alongBone` was a shared global vector, and it is now a property that returns a fresh one, so no call can see another's edit |
| A sealed class hierarchy | An abstract class with a `private protected` constructor and a switch that throws on the default arm | Dart checks the switch is exhaustive and C# does not. The throwing arm is the only guard |
| Two private copies of one routine (`_deform`) | One `internal` class, `RigDeform` | The two copies changed together, so they were one routine (F1) |
| `armature[name]!` | `Known(name)` or `RequireBone`, which throw `ArmatureException` | Dart throws a `TypeError` from the null check. Both refuse, and the C# message names the bone |
| A `LinkedHashSet` | `IReadOnlyCollection<string>` over the keys of an `OrderedDictionary` | Order is part of the answer, and a `HashSet` does not keep it |
| A `typedef` function type | A delegate for a public name (`ModelPoint`, `GroundProbe`), `Func<>` for a private one | A delegate shows its name in a signature |
| A `required` named parameter | A required positional parameter first, then optional named ones | C# has no required named parameters. A call with many required values names them, with a one line `// F2:` comment |
| Two Dart tests with one name in different groups | The C# names get a group prefix, with a comment | One C# class cannot hold two methods with one name |
| A matrix product that a bound depends on | Compute the bound in the Dart exporter, as `snapStep` does | A hand-written bound is off by a rounding step, so a `>=` for `>` mutant survives. Probe the pose in Dart and pass the exact double as the spec value |
| A generated random case set | Seed `math.Random` in the exporter and keep the numbers in the fixture | The fixture holds the values, so C# needs no Dart generator (decision 77) |
| `Quaternion.rotated` | `Quaternion.AxisAngle(...).AsRotationMatrix().Transformed(v)` | `rotated` turns by the inverse, so the rig goes through the matrix |
| A pose `Location` edit | Written in the bone's local axes | A bone along +X maps local X to world -Y, so a move by (1, 0, 0) is not a world x move. A fixture case that needs a world move must say so |

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
- A loop inside one Dart test stays inside one `[Fact]`. A `[Theory]` there adds run counts Dart does not have.
- Tests the port adds, such as a fixture check, are counted apart in
  `PARITY.md`.
