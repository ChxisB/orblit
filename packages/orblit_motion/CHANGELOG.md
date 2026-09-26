# Changelog

## 0.2.0

- **Blends.** A `BlendDocument` is a `.oblend` file: a graph of states and
  the changes between them. A state plays one clip, or clips mixed by the
  blend's inputs — along one input with a `BlendLine`, or over two with a
  `BlendPlane` — and the mixes nest, so a point on a line can be a plane of
  directions. Clips are named rather than held, so one blend serves every
  character with the same moves.
- **Changes.** A `BlendChange` goes from one state, or from anywhere, to
  another when its `BlendCondition` holds: an input above or below a number,
  an input on, the state having played through, or any of those put together.
  Each fades over its own time with its own easing, and can keep the new
  state in step with the old. Conditions are data, so a blend is written to a
  file, shown in an editor and checked without playing it.
- **Places.** Where a blend has got to is a `BlendPlace`: which state, how
  many times through it, and what it is fading in over, which may itself be
  fading. It is a value, written as JSON for a save file that restores a
  character mid-stride and as 21 numbers for a component column that
  replicates one, and a test can build one by hand and ask what happens next
  without playing a frame to get there.
- **Playing.** `BlendDocument.advance` plays on from a place and hands back
  the new place, the mixed pose, the marks passed and the root motion taken;
  `changeFor`, `weightsAt` and `sampleAt` answer the same questions without
  moving. `BlendPlayer` holds a place, the inputs and the clips for one
  character. Marks come from the clip with the most say in the state being
  played, and root motion from every clip, mixed as the pose is.
- **Mixing.** `ClipFrame.mix` blends frames by weight: numbers and vectors by
  average, rotations the short way round, and flags by whichever frame has
  the most say. A value only some frames have is shared among those.

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
