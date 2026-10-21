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
| A Dart type named like its package (`Effect`, `Audio`, `Quest`) | Rename the type, `TimedEffect`, `AudioPlayer`, `QuestDefinition` | CA1724 again, for the type that would shadow the namespace |
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
| A regex `\s`, `\w`, `\d`, `.`, `^` or `$` in multi-line mode | The pieces in `DartPattern` | .NET `\s` also matches the next-line character, and `\w` and `\d` match Unicode letters and digits. Dart reads `\w` and `\d` as ASCII, and its `.` stops at `\r`, U+2028 and U+2029 as well as `\n`. A multi-line `^` and `$` break at the same four |
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
| A NaN written into a binary STL, PLY or glb | `Float32.Quiet` turns every NaN into `0x7FC00000` before the bytes are written | A file should not change with the CPU that made the number. Dart writes the positive quiet NaN and .NET makes a negative one from arithmetic. The row above compares decoded numbers instead, for a package that has no such need |
| A `List.sort` of faces by material, past 32 faces | A stable `OrderBy` (decision 75), and the exporter holds mixed materials to 32 faces | Dart is stable up to 32 and not above. Equals can come out in another order, so a fixture past the limit would hold Dart's accident. `DifferenceTests` pins the stable order |
| A `switch` expression with a `double` arm and an integer arm | A `(double)` cast on the first arm | The switch takes its type from its arms. `float` first and then `uint` settles on `float` and rounds a 32-bit integer on the way. A glTF accessor parity case found it |
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
| A generator that parses source text | A Roslyn incremental generator over symbols (`ForAttributeWithMetadataName`) | Roslyn has resolved every type, so nothing is guessed. A field whose type does not resolve is refused as `ORBLIT001`, a case the Dart scanner could not see |
| A Dart `double` field maps to `float32` | A C# `double` is `float64` and a `float` is `float32` | Dart has one floating type and the generator chose the narrow one. C# says how wide a field is, so the kind follows the field |
| Annotation constructor arguments | Attribute properties (`Name`, `Kind`, `Replicated`, `OwnerWritable`), with `Kind` as a plain `ComponentKind` | A named attribute argument cannot be a nullable enum (CS0655). The scanner reads which arguments were given, so an unset kind still means infer it |
| `orblit_components.json` written beside the code | A `const string Manifest` on the generated registry | A source generator cannot write a file. `ComponentEmitter.Manifest` writes the same bytes as Dart for the same components, and a fixture of 127 cases compares them |
| The source path as the scanner found it | A path relative to `ProjectDir`, with forward slashes on every system | An absolute or backslash path would change the manifest from one machine to the next |
| A typed view over a list (`Float32List`) | A `ref struct` column over `Span<T>`, with its own `_data` field | The same memory with names on the offsets. A primary constructor cannot capture a ref-like parameter (CS9110) |
| A name written between single quotes unescaped | `SymbolDisplay.FormatLiteral` | A quote or a backslash in a component name breaks the generated Dart. The C# file compiles and registers the name as written |
| `List.sort` of components with one name, past 32 | A stable `OrderBy` with ordinal comparison (decision 75) | Dart is stable up to 32 and not above. `DifferenceTests` pins 40 equal names in input order |
| A command line tool that writes files and exits non-zero | `ORBLIT001` per refused type, and no registry written while any type is refused | A generator reports through diagnostics. A half registry would hide the refusal behind a later compile error |
| No third party package | `Microsoft.CodeAnalysis.CSharp` 4.14.0, `PrivateAssets="all"` | A generator needs Roslyn. 4.14.0 loads in SDK 9 and 10, and a newer one fails to load in an older SDK (decision 78) |
| `ArgumentError.value(value, name, rule)` read back as `name rule (value).` | An internal `RuleException` with that text, an `ArgumentException` | The C# message of `ArgumentException` adds a parameter name, so the port builds the Dart text itself and `Parse` passes it on in a `QuestFormatException` |
| A regex `$` | `\z` | Dart's `$` stops at the end of the input. .NET's also matches before a final newline, so `"a\n"` would pass as a key |
| A fixture message that holds a runtime's own words | `cutAfter` on the case: both sides compare up to and including that text | A JSON syntax error and a printed `Map` differ by language, and a value range error from another package has its own parity test |
| A constant such as `questExtension` | A `const` on a static class, `QuestNames.Extension` | A top level constant has no home in C#. `Quests` would be shadowed by the `QuestBook.Quests` property |
| A sealed `Node` base with one subclass per type | An abstract class with a `private protected` constructor, and a `switch` over the subclasses with an `UnreachableException` default | The constructor keeps the hierarchy closed to the assembly. A new node type then fails at the `default` arm and in no test that forgot it |
| `typedef Before = ProgressTable Function(ProgressTable)` | A `public delegate ProgressTable Before(ProgressTable table)` | A bare `Func` would work, but the name keeps the signature in one place and reads as the Dart typedef does |
| A Dart `Set<String>` that keeps insertion order (`BeforeNames`) | An `IReadOnlyList<string>` built with `Distinct()` | .NET's `HashSet` has no promised order, and a fixture compares the order |
| `'$value'` of a Dart `double`, `List` or `Map` in an error text | `Printed.Dart`: null, `true`, `1.0`, `[a, b]` and `{k: v}` | `ToString` gives `True`, `1` and `System.Collections.Generic...`. Exponent forms such as `1e+21` are not Dart's and the code says so |
| A format version read as `is int` | A format version read as `is long`; a double such as `1.0` is no version | `DartJson.Decode` gives a `long` for every whole number, so an `int` arm would never run |
| A private `_hex` in each exporter | `hex` in `tool/parity/lib/fixture.dart` | The third copy (save, progress, quest) was merged. Exporters import it from the parity tool |
| A class with `copyWith` and no `==` | A positional `sealed record` with defaults, changed with `with` | A record compares by value where the Dart class compares by identity. Nothing in the package relies on identity, so a test pins `with` and not `Equals` |
| `num.clamp`, `%` on doubles and `floor()` | `Doubles.Clamp`, `Doubles.Mod` and `Doubles.Floor` | `Math.Clamp` throws when the bounds are swapped, and Dart's `clamp` sends NaN to the upper bound. `Doubles.Floor` returns a `long` and throws on NaN, as `floor()` does |
| `log(x) / ln2` | The same, with the Dart `ln2` constant written out | `Math.Log2` rounds differently in the last digit, and an exposure fixture compares to 1e-9 |
| A const map of presets keyed by an enum | A `FrozenDictionary` behind `IReadOnlyDictionary`, with `GetValueOrDefault` and a fallback | The Dart enum is closed, so the fallback is dead there. A cast integer reaches it in C#, so a test covers it |
| A `static final` that holds a mutable object (`Strike.none`) | A `static readonly` that holds the same shared `Vector3` | Both languages hand every caller the one object. A caller that changes its direction changes it for all, and a test pins `Assert.Same` |
| A hash that wraps at 32 bits (`unitAt`) | `UnitRandom.At` in `long` arithmetic with `Low32` | A Dart int is 64-bit on the VM and a JS number on the web, so the hash masks by hand. `System.Random` and `HashCode` differ by machine and by run. The mask on the first operand is redundant, since the later masks drop the high bits. A test of known values pins the output |
| `Random` in a particle path | None. A particle is a function of seed, id and channel | `Sample` and `Birth` remember nothing, so a rewound or skipped frame finds the same particle. Decision 77 covers the packages that do need a seeded `Random` |
| A class named `Range` | A `sealed record Range`, with `Range.Of(v)` for a point | Inside `Orblit.Particles` the record wins over `System.Range`. A consumer with `using System;` and `using Orblit.Particles;` gets an ambiguity and writes `Orblit.Particles.Range` or an alias |
| `Float32List` filled by assignment | A `float[]` with a `(float)` cast on every write | Dart rounds to the nearest float32 on store, and so does the cast. A float minus a float is a float in C#, so a sum that Dart does in double is cast to `double` first |
| `copyWith` on a class that checks its fields | A method that calls the constructor | A record `with` would skip the checks. A copy cannot clear `Duration` in either language, because null means keep |
| `List.unmodifiable` | `ReadOnlyCollection` over a copy | An `IReadOnlyList` field that holds the caller's list still changes when the caller changes it. The copy is the freeze |
| An extension `sample` on `Emitter` | A static `EmitterSampling.Sample(emitter, time, output, ...)` | A static call matches the other ports and reads the same |
| `int` for a count or a capacity | `int`, 32-bit | A C# array is indexed by `int`, so a larger count could never be held. Dart's 64-bit int accepts it. Seeds, ids and burst ids stay `long`, because the hash and `BurstId` need all 64 bits |
| Three private `_need` helpers that throw `ArgumentError.value` | One internal `Require.That(ok, name, value, why)` | The third copy was merged (F1). Each throws `ArgumentException` with the argument name and the Dart text |
| A package named `orblit_path` | The namespace `Orblit.Pathing` | `Orblit.Path` would hide `System.IO.Path` for any file in a namespace under `Orblit` (CA1724) |
| An `int` cell index, 64-bit | `long` parameters on `IsOpen`, `SetOpen`, `HeightOf`, `WeightOf`, `CentreOf`, and a `long` field on `NavCell` | A cell index past 32 bits is off the grid in Dart. A C# `int` would wrap it into the grid. The array offset is an `int` only after `Inside` has passed |
| `int.abs()` and `-` on 64-bit ints, which wrap | An own `Abs` and an `unchecked` block in `IsClear`. `NearestOpen` relies on the default unchecked `long` loop | `Math.Abs(long.MinValue)` throws. Dart wraps, so a far end gives a negative step budget and `IsClear` answers true. The wrap is kept and tested |
| `width * depth` allocated before it is checked | `NavGrid` refuses a product above `Array.MaxLength` before it allocates | Dart accepts a larger grid and fails when the list is made, with a `RangeError` for a negative product. C# cannot hold the array at all |
| `Uint16List` counter of standing obstacles | `ushort[]` | Both wrap at 65536, and 65536 obstacles on one cell open it. Kept, with a test on each side of the wrap |
| `floor()` and `ceil()` of a huge double, which saturate to `int64` | `Doubles.Floor` and `Doubles.Ceil` with a `long` result, and `CellsAcross` clamps a count to `int.MaxValue` | `(long)Math.Floor(1e300)` is platform-defined in C#. Dart gives `int64` max |
| Dart's euclidean `%` on a double | `Doubles.Mod` | C# `%` keeps the sign of the dividend |
| A set passed to `NavLink` and kept by reference | `NavLink` copies the agents into its own set | A caller that changes its list later cannot change a link. Dart shares the set. Recorded in `AddedTests` |
| `typedef Planner` and `typedef Clear` | `public delegate` types, with a method group converting to them | A `NavWalker` takes a `Planner` and an optional `Clear`. `grid.FindPath` and `grid.IsClear` convert without a lambda |
| Sealed `RouteStep` with `WalkStep` and `LinkStep` | An abstract class with a `private protected` constructor and two sealed subclasses | Matches the sealed-hierarchy row. A `switch` over the two needs a default arm in C# |
| Long search and walk methods with many branches | `CellSearch`, `PortalSearch`, `LinkSearch` and `FunnelWalk` classes, `Crossing` and `Spread` helpers | CA1502 is an error at 10. Each split keeps the Dart order of work. The names have no Dart twin |
| A `PathEnd` enum switched with no default | A `default` arm that throws `InvalidOperationException` | A C# enum holds any number. Tested with `(PathEnd)99` |
| A mesh index read from a list | `Number` throws `ArgumentOutOfRangeException` for any index off the end, including negative and past 32 bits | Dart's list throws `RangeError`. Both are a throw, so the fixture checks only that it throws |
| `String.toLowerCase`, `Uri.decodeComponent` and a date printed with `toIso8601String` | `DartText.ToLower`, `DecodeUriComponent` and `Iso` in `Orblit.Asset` | .NET lowers `İ` to a dotted `i̇` and leaves the Kelvin sign alone, where Dart folds both to ASCII, so two ids that Dart treats as one would be two files. `Iso` writes three fraction digits, or six when the instant has microseconds. `Orblit.Mesh` holds `ToLower` and `Decode` and `Orblit.Dialogue` holds `Printed.Dart`, so each is a second copy here. A third copy of any is the time to move them (F1) |
| A `FormatException` carrying `message`, `source` and `offset` | `AssetFormatException(message, text, offset)`, a sealed `Exception` | `FormatException` has no place for the text or the offset, and a caller reads them to point at the fault |
| A file or index that cannot be read, in a loop that treats every failure as a miss | The same loop, with every field read by pattern (`is string`, `is long`) and `IOException`, `UnauthorizedAccessException` and `FormatException` caught | Dart's comment says every failure is a miss, but a non-UTF-8 index throws `FormatException` and a non-string `usedAt`, `at` or `hash` throws `TypeError`. C# makes all of them a miss, and `AddedTests` pins it |
| `File.resolveSymbolicLinksSync` | `RealPath.Resolve`, which walks the path one name at a time | `Directory.ResolveLinkTarget` follows the last name only. A temporary directory on macOS sits under `/var`, a link to `/private/var`, so a root compared by prefix would look like another folder |
| `RandomAccessFile.lockSync(FileLock.blockingExclusive)` | A `FileShare.None` open of `index.lock` in a retry loop, behind an in-process queue | .NET `FileStream.Lock` is not supported on macOS, and the Dart lock is a POSIX byte lock that a .NET lock file does not meet. A Dart and a C# build on one directory at once may not exclude each other on Linux or Windows. The worst case is a lost index update and a recook |
| `File.writeAsBytes` into a temporary name, then `rename` | `AtomicFile.WriteAsync(temporary, target, bytes)` | The content store and the cook cache write the same way. The fetch records are the third user and go through it too (F1) |
| `Importer.handles` | `Importer.CanHandle` | `Handles` is a Visual Basic keyword, which CA1716 refuses in a public name |
| Two Dart tests whose names differ only in punctuation or case | A distinguishing word in the C# name, such as `...WithAPoint` and `...WithAComma` | PascalCase with the punctuation dropped makes one name, and one class cannot hold two methods with it. The first of each pair keeps the plain name |
| An abstract `behavesLike(make)` function that registers tests for each implementation | An abstract class with the facts and a derived class for each implementation (`CookCacheContract`) | A derived class in xunit runs every inherited fact, as the Dart `group` does for each `make` |
| A generic `Dictionary<string, object?>` held as a non-generic `IDictionary` | Enumerate `.Keys.Cast<object>()` and read the indexer | Enumerating the `IDictionary` yields `KeyValuePair` boxes, not entries. `CookKey` took the pairs for the keys until a nested `Dictionary` case was added |
| A second process that runs the same test code (`Process.run` of `dart`) | An `[Fact]` worker, started with `Orblit.Asset.Tests -method <Class.Method>`, and `Assert.SkipUnless` on its environment variables | The apphost has no `--filter-method`. The worker skips itself in a normal run, so a run shows one skipped test for it |
| A file format that both languages write | Write the same directory in both, byte for byte. Dart's files, in a temporary folder, are opened by the C# code, and C# output equal to Dart's bytes shows the reverse | Dart reads what it writes, so equal bytes are a read in both directions. Where the bytes cannot be equal (a time, a host), the cross-read test says so |
| A parity case whose result comes from a `Future` | `PackCases.CheckAsync(fixture, op, run)`, where `run` is a `Func<JsonElement, Task<JsonNode?>>` | `Dump.Attempt` runs a sync function only, so an async case gets its own loop that catches the throw after the `await` |
| A case that needs files on disk | The case carries its own world: a list of specs with `files`, `base` and `target`, built in a temporary folder for each case (`PackCases.BuildWorldAsync`) | A pack cannot be a JSON value. The fixture holds the input, the C# builds the packs and the fixture holds what came out. One world for every case would let one case change another's result |
| A top-level function such as `packBundle` | A static method on a class named for the file, `PackBundle.PackAsync` | C# has no free functions. A bundle folder is made before the manifest is written, because a world with no files gives Dart an empty folder and C# an error |
| `RandomAccessFile.read(n)` that is not held to the range it was given | `FilePackBytes` over a `SafeFileHandle`, held to the file and not to its `bytes` range | A read may run past the range but not past the end of the file, as Dart does. The Dart class has no test, so the `PackAddedTests` are the only check. The handle is held open, so a test disposes it with `await using` before it deletes the folder |
| A `Future` for an id that is not in any pack | A `Task` that faults with `AssetNotFoundException`, and an `ExistsAsync` that answers without throwing | A caller that awaited the Dart future in a `try` still gets the throw at the `await` in C#. Do not throw before the task is made |
| `Uri` for a URL that came from a manifest or a page | `WebUrl`, an own parser | .NET `Uri` treats a leading `/` as a file path on Unix, and its rules for `\`, ports and relative references differ from Dart. Check a new case against the fixture. |
| A `Timer` that fires after the synchronous turn | A real delay, with 2 ms as the least the parity scripts use | .NET cannot run a timer after the caller's loop, so wants made in one loop do not always join. Tests use 50 ms. |
| `x << n` with a large `n` | Mask or test `n` first | C# masks the shift count to 5 or 6 bits, and Dart gives 0 for a count of 64 or more. |
| `await` always yields to the event loop | A synchronous continuation when the task is done | Dart gives a fixed order to work that was asked for in one turn, and C# does not. A test that needs the order holds the transport open (`Gated`) until every ask is made. |
| `close()` on a fetcher | `Dispose()` | Dispose cancels each download and lets jobs finish with `FetchCancelledException`. Take the jobs under the lock and complete them outside it. |
| Cook workers on one isolate that share a map of hashes and call back in turn | `Interlocked.Increment` for the next index, a `ConcurrentDictionary` of `Lazy<Task<ContentHash>>`, and a lock round `onResult` | Dart never ran two pieces of cook code at once. Threads do, so a `Lazy` keeps a shared dependency to one read, and a callback that prints progress is still called one at a time. A test importer that records what it was asked uses a concurrent queue. |
| `nativeImporters`, a getter that builds the list of native importers | `NativeImporters.Create()`, a method | Each call returns new importers, because `MaterialImporter` holds the matc version it read. A caller that wants one list keeps it |
| `_matcVersion ??= _askMatc()`, the version asked for once and kept | `Lazy<string>` in `LazyThreadSafetyMode.PublicationOnly` | Cook workers share one importer. Two asking at once is harmless, and a failed read is not kept, as the Dart field is not set until the call returns |
| `Process.run` and `Process.runSync`, which throw `ProcessException` when the program does not exist | `NativeTool.Capture`, which reads stderr on a task while it reads stdout, and a caught `Win32Exception` for `which` | Reading one pipe to the end while the other fills stops a program that writes a lot to both. A machine with no `which` finds nothing on its PATH |
| A fake tool that is a Dart script run by `dart` | A POSIX `sh` script written by `NativeToolWork.Fake(spec)`, and `[ShellFact]`, which skips without `/bin/sh` | The Windows runner has no `sh`, so the importer tests skip there. A function in `sh` has its own `$1`, so the script's arguments are passed in with `"$@"` |
| `CookingNotSupported` and the `_stub`, `_web` importers | Not ported | Decision 71 leaves the stub and web files out, and only they throw `CookingNotSupported` |
| An `async` `dependenciesOf` that returns a failed `Future` for an unreadable model or scene | `Task.Run` round the synchronous read | A C# method that throws before its first `await` throws at the call, and the Dart caller sees the error only when it awaits. The pool gives the same fault on the task. |
| `is int` on a decoded JSON number, which is false for a double | `GltfJson.WholeAt`, which answers without throwing | `AsInt` throws `InvalidCastException` on a double or a string, and a model with `"byteOffset": 1.5` has to be skipped, as Dart skips it. |
| `ModelAtlasCook.run`, one long method over local variables | `RunAsync` in steps that take a private sealed `Job` | The method is far over the complexity limit of 10. The `Job` holds what one run shares, so a step takes the run and not seven values from it. |
| `Uri.decodeComponent` caught on `ArgumentError`, which hands back the uri as given | `DartText.PercentDecoded` | `DecodeUriComponent` throws `ArgumentException` for a bad escape, as Dart throws `ArgumentError`, so the catch sits in one place. Bytes that are not UTF-8 still throw `FormatException`, as in Dart. |
| `settled['padding'] as int? ?? 2`, a cast that throws for a value of another type | `Typed<T>`, which throws `InvalidCastException` with Dart's text | A document can hold `"padding": "2"`. Dart fails with `type 'String' is not a subtype of type 'int?' in type cast`. A `switch` that fell back to the default would hide the mistake. |
| `'${named.runtimeType}'` in a failure message | `DartType`, which names the Dart type of a decoded JSON value | The message says `_GrowableList<dynamic>` or `_Map<String, dynamic>`, as Dart does. Only the types the JSON reader produces can reach it. |
| `ArgumentError.value(value, key, 'has to be a size in texels')` | `ArgumentException` with the text `Invalid argument (key): has to be a size in texels: value`, and `key` as the parameter name | That is Dart's own text. The C# message also ends with `(Parameter 'key')`, so tests read the start of it. |
| `result.heuristic.name`, the name of a Dart enum value | `HeuristicLabel`, the C# member name with a lower-case first letter | C# members are in Pascal case, and the note reads `bestShortSideFit`, as in Dart. |
| `Terrain` the class, in a file that sits in `Orblit.Asset` | `using TerrainData = Orblit.Terrain.Terrain;` | Inside `namespace Orblit.Asset` the simple name `Terrain` finds the namespace `Orblit.Terrain` first, so the type cannot be named without the alias. The type itself was ported in task 06 and is not renamed. |
| `ArgumentError.value(value, name, rule)` in the cook files | `DartArgument.Value(value, name, rule)` | One helper, extracted when the copies passed three. A string value is quoted by `DartJson.Encode`, as Dart quotes it. A number, a bool and null print plain. A list or a map prints as its contents, where Dart prints `Instance(length:n)`. The earlier atlas and glTF messages printed a string unquoted and now match Dart. |
| A Dart parameter named `out` | `@out` | `out` is a C# keyword. The name at the call stays `@out:`. |
| `cookDuringBuild` and `packDuringBuild`, free functions | `BuildHook.CookDuringBuildAsync` and `BuildHook.PackDuringBuildAsync` | A free function becomes a member of a static class. The two hooks sit apart from `CookProject` because the build calls them and `CookProject` does not. |
| `bundleHash(manifest)` | `BundleHash.Of(manifest)` | The hash of the manifest's UTF-8 bytes. |
| `CookFailed`, an `Exception` | `CookFailedException` | Its `Message` names each failure and how many of how many, with the same text. |
| `currentCookTarget`, which throws `UnsupportedError` | `CookProject.CurrentCookTarget`, which throws `NotSupportedException` | The platform comes from `OperatingSystem.Is…`. A platform with no target throws and does not fall back. |
| `Platform.environment['ORBLIT_CACHE']`, then `HOME`, then `USERPROFILE`, then the system temp folder | `CookProject.DefaultCachePath(Func<string, string?>)`, called with `Environment.GetEnvironmentVariable` | The function is a parameter so a test does not change the environment of the whole process. |
| `Directory.list(recursive: true, followLinks: false)` and `entry is! File` | `Directory.EnumerateFiles` with `EnumerationOptions { RecurseSubdirectories, AttributesToSkip = ReparsePoint }` | .NET skips hidden and system files unless told not to, and Dart lists them. The cook decides what a dot hides. A link is not followed in either. |
| `Uri.file(path)` and `uri.toFilePath()` | `new Uri(Path.GetFullPath(path))` and `LocalPath` | A relative path makes `Uri` throw, so the full path is taken first. A folder's Uri ends in a separator. |
| `file.writeAsBytes(bytes, flush: true)` | `AtomicFile.WriteBesideAsync` | The bytes are written beside the file and moved in, so a cook that stops half way leaves no half-written output. |
| `_Fixed`, a private subclass of `ImportSettingsReader` | `ImportSettingsReader` is a `public class` with a `virtual ForAssetAsync`, and `RuntimeImport` holds a private sealed `FixedSettingsReader` | A Dart class is open to extension. A C# class is sealed here unless opening it is a choice, and this is one. The reader stays small, so the subclass is the shortest way to put the call's settings over the file's own. |
| `settings == const ImportSettings()`, an identity check against a shared empty value | `ImportSettings? settings = null` | The Dart default is one const value that the code compares by identity. A null says the same without the trick. |
| The list of default importers written in `CookProject` and again in `RuntimeImport` | `RuntimeImport.DefaultImporters()` returns `CookProject.DefaultImporters()` | Dart writes the list twice. One list cannot drift. |
| `bin/cook.dart`, a process that reads `args` and writes to `stdout` and `stderr` | `CookCli.RunAsync(args, output, error)` over injected writers, and the `orblit-cook` tool in `Orblit.Cook` that calls it | Decision 79. The tests run the command in this process and read the same exit code and text. A Dart process test is a real process only because Dart has no other way to reach `main`. |
| `print` and `stdout.writeln` in the command lines | `CliOutput.Line(writer, text)`, which writes `text + "\n"` | The line ending is a line feed on every platform, as in Dart. It stood in `PackCli` and `KitCli` already, and `CookCli` made the third copy. |
| `cook: <message> (<invalidValue>)` for an `ArgumentError` in the command | `cook: {e.Message}` with the .NET text | `ArgumentException.Message` names the parameter and is longer than Dart's. The exit code is 2 in both. Upgrade the text when a build has to match the Dart line. |

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
