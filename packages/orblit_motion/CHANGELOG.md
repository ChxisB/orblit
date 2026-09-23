# Changelog

## 0.1.0

- First cut of `orblit_motion`. Pre-alpha: everything is subject to change.
- **Clips.** A `ClipDocument` is a `.oclip` file: a length, what happens at
  the end, and channels of keys. Each channel names what it moves by a scene
  path and a property, `transform.position` on an entity or `rotation` on one
  of its bones, and carries a number, a vector, a rotation or a flag. Keys are
  `orblit_sequence`'s, so a clip eases, steps and curves the way a cutscene
  does. The file writes one key to a line, so a diff of two takes is a diff of
  keys.
- **Playing.** `ClipPlayer` moves a playhead, loops, holds and bounces the
  way a `Director` does, and hands back the marks it crossed. Sampling is
  stateless, so a scrub backwards shows what playing forwards would.
- **Root motion.** A clip can name the channel that moves the character. It
  is held still in the pose, and what it would have moved comes out of the
  player as a step, so a walk moves whoever consumes it rather than sliding
  the model off its own origin. Measured across a loop's end as well as
  within a lap.
- **Scenes.** `ClipScope` resolves a clip's targets against the entity that
  plays it, including one inside a prefab instance, and `sceneOpsFor` turns a
  sampled frame into the field edits that put it on a scene.
- **Importing.** `clipsFromGltf` reads a model file's animations into clips:
  its joints as bones, named the way the stage names them, and any other node
  as an entity.
- **Editing.** `ClipChannel.rekeyed` and `ClipChannel.ofKind` change and
  make channels held as `ClipChannel<Object>`, the way an editor holds a list
  of every kind, without having to name what each one carries.
