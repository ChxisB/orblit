# Changelog

## 0.10.0

- A port of `orblit_motion` 0.10.0, part M1: the clip file, its channels and
  kinds, the frame a clip is sampled into, and root motion.
- A clip is read and written as `.oclip` text, one key to a line. A key that
  cannot be read is left out with a note, and a file that is not a clip or is
  from a newer Orblit throws `ClipFormatException`.
- A channel holds its keys typed as what it carries. `ClipChannel` is the
  untyped view, with `Rekeyed`, `OfKind` and `BoxedKeys` for code that holds
  a channel without knowing its kind.
- `ClipDocument.SampleAt` gives the frame at a time, with the root held in
  place on request. `RootStepBetween` says how far the root went between two
  times, `MarksBetween` lists the marks passed, and `ClipFrame.Mix` blends
  frames by weight.
- Root motion names the entity or bone that carries the body, whether it turns
  and rises, and which way is up. A step composes with `RootStep.Then`.
- The reader and the sampling are checked against a fixture of 1,333 cases
  to 1e-9 relative.
- Part M2 adds `ClipPlayer`. It plays a clip forwards or backwards at a speed.
  Each `Advance` gives the frame, the marks passed and how far the root went,
  however big the step. A clip ends the way it says, or the way the player is
  told: hold, loop, bounce or release.
- `ClipPass` gives the clip time and the root motion and marks for a stretch
  of laps, without a player. `ClipSync` lines laps up on shared marks.
- `KeyReducer` thins dense keys to an angle and a distance. `RestSkeleton`
  poses a skeleton's rest origins. `ClipScene` turns a frame into `SetField`
  changes, and `ClipScope` says which entity each name in the clip means.
- These are checked against a second fixture of 6,052 cases to 1e-9 relative.
  Blends, retargeting and cutscenes come in the next parts.
