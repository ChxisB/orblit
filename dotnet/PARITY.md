# Parity

One row per package that has been ported. A row is added when a port starts.

Declared counts the test declarations in the source. Run counts the tests that
execute, because a loop or a theory runs one declaration many times. A package
is done when the two Run counts match.

| Package | Dart declared | Dart run | C# declared | C# run | Fixtures | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `orblit_noise` | 16 | 41 | 16 | 41 | `noise.json`, 390 cases | fixture check (6 run), non-finite refusal (3 run) | done |
| `orblit_core` | 32 | 32 | 32 | 32 | `core.json`, 1 world and 45 handles | snapshot of a built world (1 run) | wrapper layer only, `orblit_native` not started |

## The rule for doubles

`Tolerance.Close` accepts two doubles within 1e-9 relative. The scale is never
below one, so a value near zero is held to 1e-9 absolute. Two NaNs agree. Two
infinities of the same sign agree. Everything else that is not finite differs.

The `orblit_noise` fixture matches bit for bit today, because the C# does the
same IEEE operations in the same order. The tolerance is there for packages that
call `sin`, `cos` or `pow`, where the two runtimes can differ in the last digit.
