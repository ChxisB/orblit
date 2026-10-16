# Ragdolls

A ragdoll gives a skeleton a physics body for each limb and a joint to its
parent. It lives in the physics repository, as one package beside the others.
Names below were checked against `orblit_physics_rig` 0.1.0, `orblit_physics`
0.15.0 and `orblit_rig` 0.3.0. They were exercised in headless tests and have
not been seen drawn in a running scene.

```yaml
dependencies:
  orblit_physics_rig:
    git:
      url: https://github.com/ChxisB/orblit-physics.git
      path: packages/orblit_physics_rig
```

It depends on `orblit_physics` and `orblit_rig` and nothing else in Orblit.
`lib/orblit_physics_rig.dart` lists the exports: `Ragdoll`, `RagdollMode`,
`RagdollTuning`, `RagdollPose`, `RagdollCharacter`, `RagdollSpec`,
`RagdollBody`, `RagdollHinge`, `RagdollBall`. Native platforms only, as
`orblit_physics` is.

## A spec

A `RagdollSpec` lists bodies, parents before the bodies that hang from them,
with one root first.

```dart
final spec = RagdollSpec([
  RagdollBody(bone: 'spine', from: 'hips', radius: 0.14, mass: 20),
  RagdollBody(
    bone: 'head',
    parent: 'spine',
    joint: RagdollBall(swing: 0.6),
    radius: 0.1,
    mass: 4,
  ),
  RagdollBody(
    bone: 'upper_arm.L',
    parent: 'spine',
    joint: RagdollBall(swing: 1.5),
    radius: 0.05,
    mass: 2,
  ),
  RagdollBody(
    bone: 'forearm.L',
    parent: 'upper_arm.L',
    joint: RagdollHinge(),   // low 0, high 2.4: bends one way
    radius: 0.04,
    mass: 1.5,
  ),
]);
```

- A body is a capsule from the head of `from` to the tail of `bone`. `from`
  defaults to `bone`. A torso over three bones is one body with `from` the
  lowest. Bones between them, and bones below `bone` with no body, keep their
  animated pose against the body.
- `parent` is another body's `bone`, and `joint` is `RagdollBall` or
  `RagdollHinge`. A root has neither and every other body has both.
- `RagdollHinge(axis:, low:, high:)` turns about `axis`, in the bone's own
  space whose Y runs head to tail. `RagdollBall(swing:, twistLow:, twistHigh:)`
  tips within a cone of `swing` radians and turns about its own length within
  the twist limits. Angles are radians here, as in `orblit_physics`.
- The constructor throws `ArgumentError` for a missing parent, a body listed
  before its parent, two bodies ending at one bone or no single root.
  `spec.check(armature)` throws `ArmatureError` for a bone the rig lacks.

## Running it

```dart
pose.evaluate();
final ragdoll = Ragdoll.build(
  physics: physics,
  pose: pose,
  spec: spec,
  modelToWorld: model,
  ignore: characterLayer,   // layers the bodies take no notice of
);
final stack = ModifierStack()..add('ragdoll', RagdollPose(ragdoll));

// Every frame.
binding.poseFrom(clip, pose);
pose.evaluate();
stack.apply(pose, frame: ModifierFrame(dt: dt, modelToWorld: model));
physics.step(dt);
final joints = binding.jointsFor(pose);
```

- Put `RagdollPose` after the modifiers that shape the animation and before
  the ones that should follow the fall, such as `SpringBones`.
- Step the world once a frame with the `dt` the frame carries. The bones
  written are where the last step left the bodies, and the animated bodies
  are given the velocity that reaches this pose in the step that follows.
- Bodies are numbered from `firstId` (default 1), one per body in spec order.
  `idOf(bone)` finds the id of the body that covers a bone. A joint has its
  body's number, because bodies and joints are numbered apart.
- The bodies are in one `layer` (default `0x100`) that does not care about
  itself, so a forearm passes through the thigh beside it. `ragdoll.layers`
  is that group, and the layers to cast with when a ray should not see it.

## Modes and hits

Each body is one of three, and `modeOf(bone)` says which.

| Mode | The body |
| --- | --- |
| `RagdollMode.animated` | is driven by velocity to the pose, pushes what it meets and is not pushed back |
| `RagdollMode.powered` | is free, and a spring on its joint pulls it to the pose's angle, so it gives under a blow and comes back |
| `RagdollMode.limp` | is free and has only its joint's limits |

```dart
ragdoll.hit('forearm.L', impulse: Vector3(0, 0, 6), spread: 0.04);
ragdoll.setMode('head', RagdollMode.limp);
ragdoll.setFrom('upper_arm.L', RagdollMode.powered); // and everything below
ragdoll.setAll(RagdollMode.animated);
```

- `hit` makes the struck body limp and pushes it. A body one joint away goes
  limp `spread` seconds later, and the next after the same again, so a blow
  runs out from where it landed. A spread of nought is everything now.
- A powered root is animated, because it has no joint to spring.
- `RagdollTuning(powerHertz: 8, powerRatio: 1, linearDamping: 0.1,
  angularDamping: 0.5, snapDistance: 2)`. A driven body further than
  `snapDistance` model units from its pose is put there, not carried.
- `isAtRest({speed = 0.2})`, `hasFree`, `rootPosition`, `bones`.

## A character that falls and gets up

`RagdollCharacter` hands a character between its capsule and its bodies.

```dart
final character = RagdollCharacter(
  ragdoll: ragdoll,
  id: 800,
  at: feet,
  pose: ragdollPose,                       // for the blend; may be null
  layers: const Layers(is_: characterLayer),
);

character.knock('forearm.L', impulse: Vector3(0, 0, 6));  // capsule goes
// ... later, once ragdoll.isAtRest() ...
final standing = character.standUp(reach: 2, blend: 0.4); // Vector3?
if (standing != null) model = Matrix4.translation(standing);
```

- `knock` removes the capsule and calls `hit`. Knocking a character that is
  already down only hits the bodies again.
- `standUp` casts down `reach` metres from the root. With ground there it puts
  the capsule back at that point, makes every body animated, and starts the
  blend. It answers the point, or null and stays down when no ground is in
  reach, and null for a character that is standing.
- **Moving the model to the point is the game's job.** The ragdoll writes the
  root bone where the body lies, so the model has to be where the character
  stands before the get-up clip plays.
- `ragdollPose.recover(seconds)` mixes the output from the last fallen frame to
  the animated pose, easing in and out. `standUp` calls it when given a pose.
  `recovering` says whether it is still going.
- The capsule's layer has to be one the ragdoll ignores, or its bodies would
  push it. The constructor throws `ArgumentError` when it is not, and takes the
  ragdoll's own layer out of what the capsule cares about.

## Aiming a joint

`physics.aim(id, {rest, goal})` moves where a joint's springs pull and its
goal, without making it again. `rest` is six places in the order along x, y,
z then about x, y, z, in metres and radians. A null leaves that way alone. A
way with no spring ignores its place, and a joint with no goal ignores
`goal`. It answers false for an id that is not a joint and for an infinite
value. Both bodies wake only when a place moved. A powered ragdoll aims its
joints each frame to the angles the pose has between two bones.

## Traps

- **An animated body below a free one pins that one's far end** to where the
  pose has it, as a hand holds a rail. A shoulder that should fall needs the
  forearm limp too: use `setFrom`, not `setMode`. A `hit` gets there by
  itself as the spread reaches each body.
- **A joint's zero is how its bodies stood when it was made.** The ragdoll
  makes each one at the rig's rest and remakes it when its springs change, by
  standing the body at rest against its parent for the moment, so building from
  a bent pose is fine. Do not call `join` on a ragdoll's ids yourself.
- **Bone lengths are the rig's, never the bodies'.** Only the root bone is
  moved. Below it a bone is turned and stays on its parent, so a stretched
  joint cannot stretch the mesh.
- **Driven characters cannot be made free**, and a driven body shoves one.
  Keep the ragdoll's bodies and the capsule in layers that ignore each other.
- `hit` times come from a clock that only runs while `dt` is above nought, so a
  paused game does not spread a blow.
- Poses must be evaluated before `Ragdoll.build` and before every solve.
