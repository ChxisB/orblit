# Changelog

## 0.11.0

- `GltfImport.Clips` reads a `KHR_animation_pointer` channel that aims at a
  node's translation, rotation or scale. It gives the same channel as the
  plain form does. A bone is still found by its name.
- A pointer at anything else, such as a material, a light, a camera or a
  node's visibility, is named in the import notes and not read. Clips have no
  track for those yet.
- A pointer at a node the file does not have is noted as not a node.
- Two channels that meet on one node and one property, one plain and one
  pointed, give one channel and a note.

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
- Part M3 adds `BlendDocument`, a graph of states, sources and conditions
  saved as `.oblend` text, with `BlendPlayer` to run it. `BlendPlace` is the
  state of a player as explicit data: it saves as JSON and as 21 float64
  values. `PoseSnapshot` holds a frozen pose to fade out of.
- Part M4 adds `ClipLayer` and `BoneMask`. A layer lays one frame over another
  by weight on the bones a mask names, or adds it on top of a reference.
- Part M5 adds bone names, `HumanoidMap` and `LimbReach`. A skeleton is
  searched for the bones of a person by name and by shape. `ClipRetarget`
  carries a clip from one skeleton to another, and `BoneMap` saves the pairs
  worked out or set by hand.
- Part M6 adds `GltfImport`, which reads a model's animations as clips and
  its skins as skeletons, and `LiveRetarget`, which carries a source frame
  onto a target skeleton one bone at a time and pins a limb to a source bone.
  `Orblit.Motion` now refers to `Orblit.Mesh`.
- Part M7 adds `CutsceneDocument`, a clip that moves what is in the scene
  with `CutsceneShot` and `CutsceneSound`, saved as `.ocutscene` text. Its
  `Sequence` blends shots over the time they overlap and cuts where they
  touch. A cutscene refuses keys on bones and root motion.
- Each part is checked against a fixture of its own to 1e-9 relative.
