# Performance baselines

The engine core is fast on this machine. A game starts in 0.15 to 0.26 s and
loads a scene at about 7 µs per entity. The slow parts are the C++ editor
start, which takes 1.3 to 1.5 s and about 2.1 s from Finder, and the test
suite. The suite took 9.5 s until `f4246869` and now takes 5.0 s.

Every figure is a median of several runs with the low and high beside it.

## Machine and build

| Item | Value |
| --- | --- |
| Machine | Apple M4 Pro, 12 CPUs, 24 GB, macOS 27.0.1 |
| Build | `scons platform=macos arch=arm64 target=editor tests=yes module_csharp_enabled=no vulkan=no` |
| Optimization | the default for an editor target, `optimize=speed_trace`, which is `-O2`, with no LTO |
| Run mode | `--headless`, no GPU, so no frame time or render cost is measured |
| Tree | `3aff913d` for the baseline, `f4246869` for the after column |

## Method

The binary is the release editor above. A full run takes about 8 minutes on an
idle machine. The probe projects set
`application/run/low_processor_mode_sleep_usec=0`, because a headless frame
otherwise sleeps 6.9 ms and 600 frames would time the sleep.

## Baseline

| Row | Median s | Low s | High s | Runs | ERROR lines per run | Bad exits |
| --- | --- | --- | --- | --- | --- | --- |
| Test suite | 9.528 | 9.224 | 9.842 | 5 | 4 | 0 |
| Game start and quit | 0.246 | 0.150 | 0.265 | 20 | 0 | 0 |
| Editor start and quit | 1.281 | 1.261 | 1.469 | 10 | 0 | 0 |
| Editor start and quit, no TERM | 1.969 | 1.842 | 2.197 | 10 | 0 | 0 |
| zsh profile alone | 0.606 | 0.562 | 0.613 | 5 | 0 | 0 |
| Load, empty | 0.157 | 0.150 | 0.252 | 7 | 0 | 0 |
| 600 frames, empty | 0.260 | 0.248 | 0.263 | 5 | 0 | 0 |
| Load, 10000 entities | 0.257 | 0.245 | 0.266 | 7 | 0 | 0 |
| 600 frames, 10000 entities | 0.261 | 0.255 | 0.263 | 5 | 0 | 0 |
| Load, 50000 entities | 0.554 | 0.541 | 0.567 | 7 | 0 | 0 |
| 600 frames, 50000 entities | 0.555 | 0.553 | 0.565 | 5 | 0 | 0 |
| Load, 200000 entities | 1.657 | 1.645 | 1.665 | 7 | 0 | 0 |
| 600 frames, 200000 entities | 1.660 | 1.646 | 1.683 | 5 | 0 | 0 |

The four ERROR lines in the suite are expected. They come from tests that
check an error path.

Two rows are bimodal. A game start lands near 0.15 s or near 0.26 s, and the
editor start lands near 1.26 s or near 1.46 s. Three more full runs gave
editor medians of 1.462, 1.460 and 1.456 s, and empty load medians of 0.246
and 0.257 s. The cause is not checked. Read the median with its range.

One suite run in a second full run took 2.2 s. Eight direct runs took 9.1 to
10.0 s, and all passed. The harness did not record exit codes then, so the
cause is unknown. It now counts non-zero exits.

## What the rows say

- **Scene load costs about 7 µs per entity.** From 10,000 to 50,000 entities
  it adds 0.297 s for 40,000 entities. From 50,000 to 200,000 it adds
  1.103 s for 150,000. Both give 7.4 µs. The cost is linear.
- **Idle entities cost nothing per frame.** At 200,000 entities, 600 frames
  add 0.003 s over a one-frame run. No entity processes, so this measures
  the loop and the tree, not scripts.
- **The editor start is the largest fixed cost.** User CPU is about 1.0 s and
  RSS is about 886 MB for an empty project. A dev-build sample puts the most
  time in `Stage::initialize`, which enters and readies the editor UI tree,
  and then in `Stage::finalize`. Task 17 removes that C++ editor UI, so it is
  not optimized here.
- **A Finder launch adds about 0.6 s.** With `TERM` unset, the editor reads
  the user's shell environment on start. The zsh profile alone takes 0.6 s
  here, and the editor takes 0.6 to 0.7 s longer without `TERM`. See decision 194.

## Change kept

| Row | Before | After |
| --- | --- | --- |
| Test suite | 9.528 s (9.224 to 9.842) | 4.975 s (4.264 to 5.093) |

`[Logger][RotatedFileLogger] Rotates logs files` took 4.85 s of the 9.5 s. It
slept 1.2 s four times. A log backup is named with a timestamp of one second
resolution, so two rotations inside one second wrote to the same file and
the first backup was lost. `RotatedFileLogger::rotate_file` now adds a
padded counter when the name exists, and the test no longer sleeps. The
test failed before the change with 2 files where 3 were expected. The
suite still has 1407 cases and 359,288 assertions, all passing.

## Not changed

| Candidate | Why it stays |
| --- | --- |
| Run the shell-environment load on a thread or lazily | `setenv` is not thread-safe, and the gain shows only on a Finder launch, which this machine could not test. Decision 194. |
| Speed up the C++ editor UI start | Task 17 removes that UI. |
| A clock seam in `RotatedFileLogger` | The counter fixes the cause. A seam adds API only for a test. |
| Web `optimize=speed` | It costs about 5 MiB of wasm per the note in `detect.py`, and no real-browser frame time exists to show a gain. |

## Web size

| File | Raw | Brotli level 11 | Gzip level 9 |
| --- | --- | --- | --- |
| Release template wasm | 29,786,195 | 6,254,150 | 8,844,456 |
| Debug template wasm | 30,684,849 | 6,420,514 | 8,925,817 |
| .NET `_framework`, 9 files | 4,487,608 | 1,416,405 | 1,743,031 |

The `_framework` sizes are the build's own `.br` and `.gz` files.

The release wasm is only 2.6% smaller than the debug one. Both build with
`-Os`, because `engine/platform/web/detect.py` sets `optimize=size` for every
target and overrides the usual debug and release split. The two builds differ
only by debug-only code. Of the 29.8 MB, 25.1 MB is code and 4.4 MB is data.
Neither file has a name section.

Not tried: `optimize=size_extra`, and a build profile that drops classes a
game does not use. Both need a Web build of about 3 minutes and a size check.

## Limits

- One machine, one build, headless. No frame time and no render cost.
- The scene rows hold entities that do not process, so they say nothing
  about script cost.
- The Finder cost is inferred from a run without `TERM`. No run came from
  Finder.
