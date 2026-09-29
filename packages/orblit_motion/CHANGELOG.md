# Changelog

## 0.4.0

- **Retargeting.** `retargetClip` makes a clip built for one skeleton move
  another. Each bone is turned so it stands in the world the way its driver
  did, measured from where each rests. A shoulder that tips forward in the
  source tips forward in the target, whichever way the two point their
  bones. Positions grow by how much bigger one skeleton is than the other,
  and root motion follows its bone. It returns the new clip and a list of
  what it could not carry.
- **Keys survive.** When a bone and its parent line up with the bones that
  drive them, each rotation is worked out exactly at the source's own keys,
  and easing, holds and cubic slopes come across as they were. When they do
  not, because the target has fewer spine bones or a parent nothing drives,
  the turn is worked out at every key of the bones involved and joined with
  straight lines.
- **Which bones.** `matchBones` pairs the bones of two skeletons by name. A
  namespace such as `mixamorig:`, a `DEF-` prefix and the spelling of left
  and right are ignored. A name that two bones share once cleaned is left
  unmatched rather than guessed. `retargetClip` takes a `bones` map that adds
  to that or overrides it.
- **Rest poses.** A `RestSkeleton` is a skeleton standing still: its bones,
  their parents, and where each sits in its parent. `restSkeletonsFromGltf`
  reads one for every skin in a `.glb` or `.gltf`, with bones named as
  `clipsFromGltf` names them.

## 0.3.0

- **Cutscenes.** A `CutsceneDocument` is a `.ocutscene` file. Its keys and
  marks are a clip's, named by the scene's own ids, so a key on a part of a
  placed prefab names it by its path. On top of them are `CutsceneShot`s,
  which name the camera looked through and when, and `CutsceneSound`s. Two
  shots that overlap blend from one camera to the other over the overlap,
  with weights that add up to one. It is written one key, mark, shot or
  sound to a line, and read leniently the way a clip is.
- **Playing a cutscene.** `sequence` is its shots, sounds and marks for a
  `Director`, which stops on the last frame by default. `shotsAt` answers
  which cameras are looked through at a moment. The keys are sampled from
  `motion`, because a clip holds its last keys and a sequence's tracks do
  not. `Director`, `ShotAt` and `SoundAt` are exported from here.
- `ClipScope.wholeScene` is where a cutscene's targets are: the scene's own
  ids, as they are.
- `ClipDocument.fromJson` reads a clip out of decoded JSON. A cutscene
  shares it.

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
