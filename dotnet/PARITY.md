# Parity

One row per package that has been ported. A row is added when a port starts.

Declared counts the test declarations in the source. Run counts the tests that
execute, because a loop or a theory runs one declaration many times. A package
is done when the two Run counts match.

| Package | Dart declared | Dart run | C# declared | C# run | Fixtures | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `orblit_noise` | 16 | 41 | 16 | 41 | `noise.json`, 390 cases | fixture check (6 run), non-finite refusal (3 run) | done |
| `orblit_core` | 32 | 32 | 32 | 32 | `core.json`, 1 world and 45 handles | snapshot of a built world (1 run) | done. Wrapper layer only: both sides call the same C++ |
| `orblit_native` | 24 | 24 | 24 | 24 | none | none | done. Compiles real C++ with the machine's clang, so it needs one on the PATH |
| `orblit_cells` | 21 | 21 | 21 | 21 | `cells.json`, 180 `cellOf` and 121 `plan` cases | `cellOf` fixture check (1 run), `plan` fixture check (1 run) | done |
| `orblit_profile` | 16 | 16 | 16 | 16 | `profile.json`, 2 scripted runs of 150 frames | snapshot fixture check (1 run) | done |
| `orblit_light` | 32 | 32 | 32 | 32 | `light.json`, 100 photometry, 34 tint and 266 light cases | photometry, tint and light fixture checks (3 run) | done |
| `orblit_progress` | 37 | 37 | 37 | 37 | `progress.json`, 141 schema, 42 table script, 308 table decode, 236 changes decode, 30 condition and 144 comparison cases | schema, table, table decode, changes decode, condition, condition list and comparison fixture checks (7 run) | done |
| `orblit_effect` | 35 | 35 | 35 | 35 | `effect.json`, 570 curve, 56 effect, 7 playing script, 77 applied, 196 shortest turn and 72 lerp cases | curve, effect, playing, applied and helper fixture checks (5 run) | done. `vector_math` has no package here, so its fixture is the effect one plus hand-written `Orblit.VectorMath.Tests` |
| `orblit_audio` | 99 | 99 | 99 | 99 | `audio.json`, 504 falloff, 60 cone, 850 constructor refusal, 500 heard-by and 150 script cases (40 audio, 40 music, 40 cue, 30 contact) | falloff and cone, constructor refusal, heard-by and four script fixture checks (7 run) | done. `Orblit.VectorMath` gained `Dot`, `Cross`, `/`, unary `-` and `SetValues`, with 3 hand-written tests |
| `orblit_agent` | 64 | 64 | 64 | 64 | `agent.json`, 1200 force, 100 constructor refusal, 100 integrate, 40 heading, 250 tree (14 ticks each), 15 label and 1 plaza case (100 walkers, 4 seconds) | force, avoid refusal, integrate, heading, tree, label and plaza fixture checks (7 run) | done. `Orblit.VectorMath` gained `DistanceTo` and `DistanceToSquared`, with 1 hand-written test |
| `orblit_collide` | 32 | 32 | 32 | 32 | `collide.json`, 621 contact, 250 shape, 120 box, 300 segment, 700 ray, 120 many-ray, 120 broadphase, 6 refusal, 100 layer, 300 flat touch, 100 flat box and 125 world cases (25 with ids the pair key gets wrong) | contact, shape, box, segment, ray, many-ray, broadphase, broadphase refusal, layer, flat touch, flat box and world fixture checks (12 run) | done. The Dart `World2` pair key mis-pairs ids outside 0 to 1000002 and is ported as it is |
| `orblit_sequence` | 40 | 40 | 40 | 40 | `sequence.json`, 7 easing, 40 clip, 120 channel, 100 lerp, 100 mix, 40 sequence (19 moments, 12 mark windows and 22 snaps each) and 40 director script cases (14 steps each) | easing, clip, channel, lerp, mix, sequence and director fixture checks (7 run), refusal tests (5 run) | done. `Orblit.VectorMath` gained `Quaternion.Length`, `Length2` and `Normalize` and `Doubles.Round`, with 4 hand-written tests. Dart `Sequence` is `CutSequence` and a `Mixer<T>` is `IMixer<T>` |
| `orblit_save` | 99 | 105 | 99 | 105 | `save.json`, 171 reason parse, 156 custom reason, 273 key, 60 file write, 99 file read, 60 history record, 42 entry, 19 spacing, 10 autosave script (each with a scripted random) and 7 autosave refusal cases | reason, custom reason, key, file write and read, history record, entry, spacing, autosave and autosave refusal fixture checks (10 run), tests for the inline queue off the caller's stack, a second close, a record's copy and a key with a new line (5 run) | done. The worker isolate is a worker task, so the store is shared. `AutoSave` is `AutoSaver`. The history tool is `orblit-save-history` |
| `orblit_camera` | 59 | 59 | 59 | 59 | `camera.json`, 1 default lens, 120 damping, 90 lens (60 blend, 30 printed), 40 flat lens, 60 screen rect, 60 blend, 50 table, 80 rotate, 120 slerp, 100 look, 120 project, 120 placing, 250 aim (50 each of 5 kinds), 300 body (50 each of 6 kinds), 60 noise and 80 brain script cases | 16 fixture checks, one per kind of case above (16 run), refusal tests for a blend style and a follow binding that are not one of the named ones (2 run) | done. `Orblit.VectorMath` gained `Quaternion.Euler` and `Conjugate`, `Vector3.Normalize` and `Doubles.ToStringAsFixed`, with 7 hand-written tests. The 26 refusal cases are real Dart `ArgumentError`s. Dart `Damping` is `Smoothing` here, because a static class named `Damping` is shadowed by a property of the same name |
| `orblit_sprite` | 95 | 95 | 95 | 95 | `sprite.json`, 23 atlas read, 12 sequence, 19 grid, 25 region, 8 tileset, 43 tile map, 48 animation, 48 animation from an atlas, 3 flipbook, 9 parallax, 28 view, 35 pack, 3 write atlas, 393 PNG decode, 6 PNG encode and 4 PNG encode refusal cases | one fixture check for each of the 16 kinds (16 run), option, pixel size, heuristic and PNG size refusals (9 run) | done. A sequence of 34 or more names that read as the same number comes out in a stable order, not Dart's (decision 75) |
| `orblit_terrain` | 144 | 144 | 144 | 144 | `terrain.json`, 169 cover, 162 colour, 73 policy, 62 key name, 81 key containing, 21 region encode, 19 region new, 13 valid size, 297 region decode, 260 terrain decode, 207 layer, 6 query, 108 stroke and 6 scatter cases | one fixture check for each of the 14 kinds (14 run), a density refusal, a map length refusal and three seeded jitter checks (9 run) | done. A seeded jittered stroke is repeatable but is not Dart's (decision 77), so no fixture holds one. A region file with a NaN height differs from Dart's in the NaN payload bits, so the fixture compares decoded heights and leaves out NaN region decodes |

## The rule for doubles

`Tolerance.Close` accepts two doubles within 1e-9 relative. The scale is never
below one, so a value near zero is held to 1e-9 absolute. Two NaNs agree. Two
infinities of the same sign agree. Everything else that is not finite differs.

The `orblit_noise` fixture matches bit for bit today, because the C# does the
same IEEE operations in the same order. The tolerance is there for packages that
call `sin`, `cos` or `pow`, where the two runtimes can differ in the last digit.
