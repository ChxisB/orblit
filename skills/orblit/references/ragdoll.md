# Ragdolls

A ragdoll gives a skeleton a physics body for each limb and a joint to its
parent. It lives in the physics repository, as `Orblit.Physics.Rig`, one
package beside the others. Names below were checked against
`Orblit.Physics.Rig` 0.1.0, `Orblit.Physics` 0.17.0 and `Orblit.Rig` 0.3.0.
They were exercised in headless tests and have not been seen drawn in a
running scene.

```sh
dotnet add package Orblit.Physics.Rig
```

The package is packed by the physics repository's `tool/pack_local.sh`, into
the same feed as the engine's ([dotnet.md](dotnet.md#setting-up)). It depends
on `Orblit.Physics`, `Orblit.Rig` and `Orblit.VectorMath` and nothing else in
Orblit. The namespace is `Orblit.Physics.Rig`, and it holds `Ragdoll`,
`RagdollMode`, `RagdollTuning`, `RagdollPose`, `RagdollCharacter`,
`RagdollSpec`, `RagdollBody`, `RagdollJoint`, `RagdollHinge` and `RagdollBall`.
It runs where `Orblit.Physics` does.

## A spec

A `RagdollSpec` lists bodies, parents before the bodies that hang from them,
with one root first.

```csharp
using Orblit.Physics.Rig;

var spec = new RagdollSpec(
[
    new RagdollBody("spine", radius: 0.14, @from: "hips", mass: 20),
    new RagdollBody(
        "head",
        radius: 0.1,
        parent: "spine",
        joint: new RagdollBall(swing: 0.6),
        mass: 4),
    new RagdollBody(
        "upper_arm.L",
        radius: 0.05,
        parent: "spine",
        joint: new RagdollBall(swing: 1.5),
        mass: 2),
    new RagdollBody(
        "forearm.L",
        radius: 0.04,
        parent: "upper_arm.L",
        joint: new RagdollHinge(), // low 0, high 2.4: bends one way
        mass: 1.5),
]);
```

- A body is a capsule from the head of `from` to the tail of `bone`. `from`
  defaults to `bone`. Write it `@from`, because `from` is a C# keyword. A torso
  over three bones is one body with `from` the lowest. Bones between them, and
  bones below `bone` with no body, keep their animated pose against the body.
- `parent` is another body's `bone`, and `joint` is a `RagdollBall` or a
  `RagdollHinge`. A root has neither and every other body has both.
- `new RagdollHinge(axis, low, high)` turns about `axis`, in the bone's own
  space whose Y runs head to tail. `new RagdollBall(swing, twistLow, twistHigh)`
  tips within a cone of `swing` radians, up to 3, and turns about its own
  length within the twist limits. Angles are radians here, as in
  `Orblit.Physics`.
- A `RagdollBody` throws `ArgumentOutOfRangeException` for a radius or mass of
  nought or less, and `ArgumentException` for a parent without a joint or a
  joint without a parent. The `RagdollSpec` constructor throws
  `ArgumentException` for a missing parent, a body listed before its parent,
  two bodies ending at one bone or no single root.
  `spec.Check(armature)` throws `ArmatureException` for a bone the rig lacks.
  `Ragdoll.Build` calls it.

## Running it

```csharp
using Orblit.Physics;
using Orblit.Physics.Rig;
using Orblit.Rig;

using var physics = new Physics();
pose.Evaluate();
var ragdoll = Ragdoll.Build(
    physics,
    pose,
    spec,
    model,
    ignore: characterLayer); // layers the bodies take no notice of
var stack = new ModifierStack();
stack.Add("ragdoll", new RagdollPose(ragdoll));

// Every frame.
binding.PoseFrom(locals, pose); // locals: the clip's bones now, by name
pose.Evaluate();
stack.Apply(pose, new ModifierFrame(dt, model));
physics.Step(dt);
var joints = binding.JointsFor(pose);
```

`pose` is an `Orblit.Rig.Pose`, `model` a `Matrix4` and `binding` an
`Orblit.Stage.SkinBinding`.

- Put `RagdollPose` after the modifiers that shape the animation and before
  the ones that should follow the fall, such as spring bones.
- Step the world once a frame with the `dt` the frame carries. The bones
  written are where the last step left the bodies, and the animated bodies
  are given the velocity that reaches this pose in the step that follows.
- Bodies are numbered from `firstId` (default 1), one per body in spec order.
  `IdOf(bone)` finds the id of the body that covers a bone. A joint has its
  body's number, because bodies and joints are numbered apart.
- The bodies are in one `layer` (default `0x100`, and it has to be a single
  bit) that does not care about itself, so a forearm passes through the thigh
  beside it. `ragdoll.Layers` is that group, and the layers to cast with when
  a ray should not see it.
- Without a modifier stack, call `ragdoll.FollowPose(pose, frame)` before the
  step and `ragdoll.WritePose(pose, frame)` after it. `Snap(pose, frame)`
  puts every body where the pose has it, with no velocity.
- `Dispose` takes every body and joint out of the world.

## Modes and hits

Each body is one of three, and `ModeOf(bone)` says which.

| Mode | The body |
| --- | --- |
| `RagdollMode.Animated` | is driven by velocity to the pose, pushes what it meets and is not pushed back |
| `RagdollMode.Powered` | is free, and a spring on its joint pulls it to the pose's angle, so it gives under a blow and comes back |
| `RagdollMode.Limp` | is free and has only its joint's limits |

```csharp
using Orblit.VectorMath;

ragdoll.Hit("forearm.L", new Vector3(0, 0, 6), spread: 0.04);
ragdoll.SetMode("head", RagdollMode.Limp);
ragdoll.SetFrom("upper_arm.L", RagdollMode.Powered); // and everything below
ragdoll.SetAll(RagdollMode.Animated);
```

- `Hit` makes the struck body limp and pushes it. The impulse is in
  newton-seconds in the world, at the world point `at`, or at the body's
  centre without one. A body one joint away goes limp `spread` seconds later,
  and the next after the same again, so a blow runs out from where it landed.
  A spread of nought is everything now.
- A powered root is animated, because it has no joint to spring.
- `new RagdollTuning(PowerHertz: 8, PowerRatio: 1, LinearDamping: 0.1,
  AngularDamping: 0.5, SnapDistance: 2)`, passed to `Build` as `tuning`. A
  driven body further than `SnapDistance` model units from its pose is put
  there, not carried.
- `IsAtRest(speed: 0.2)`, `HasFree`, `RootPosition` and `Bones`.

## A character that falls and gets up

`RagdollCharacter` hands a character between its capsule and its bodies.

```csharp
using Orblit.Physics;
using Orblit.VectorMath;

using var character = new RagdollCharacter(
    ragdoll,
    id: 800,
    at: feet,
    pose: ragdollPose, // for the blend; may be null
    layers: new Layers(characterLayer));

character.Knock("forearm.L", new Vector3(0, 0, 6)); // the capsule goes
// ... later, once ragdoll.IsAtRest() ...
if (character.StandUp(reach: 2, blend: 0.4) is { } standing)
{
    model = Matrix4.TranslationValues(standing.X, standing.Y, standing.Z);
}
```

`ragdollPose` is the `RagdollPose` added to the stack.

- `Knock` removes the capsule and calls `Hit`. Knocking a character that is
  already down only hits the bodies again. `IsDown` says whether it is down.
- `StandUp` casts down `reach` metres from the root. With ground there it puts
  the capsule back at that point, makes every body animated, and starts the
  blend. It answers the point, or null and stays down when no ground is in
  reach, and null for a character that is standing.
- **Moving the model to the point is the game's job.** The ragdoll writes the
  root bone where the body lies, so the model has to be where the character
  stands before the get-up clip plays.
- `ragdollPose.Recover(seconds)` mixes the output from the last fallen frame to
  the animated pose, easing in and out. `StandUp` calls it when given a pose.
  `Recovering` says whether it is still going.
- The capsule's layer has to be one the ragdoll ignores, or its bodies would
  push it. The constructor throws `ArgumentException` when it is not, and takes
  the ragdoll's own layer out of what the capsule cares about. The shape
  defaults to `Shape.Capsule(0.3, 0.6)`.
- `Dispose` takes the capsule out of the world if it is there.

## Aiming a joint

`physics.Aim(id, rest, goal)` moves where a joint's springs pull and its
goal, without making it again. `rest` is six places in the order along x, y,
z then about x, y, z, in metres and radians, as a list of `double?`. A null
entry leaves that way alone, and a null list leaves all six. A way with no
spring ignores its place, and a joint with no goal ignores `goal`. It answers
false for an id that is not a joint and for an infinite value. Both bodies wake
only when a place moved. A powered ragdoll aims its joints each frame to the
angles the pose has between two bones.

## Traps

- **An animated body below a free one pins that one's far end** to where the
  pose has it, as a hand holds a rail. A shoulder that should fall needs the
  forearm limp too: use `SetFrom`, not `SetMode`. A `Hit` gets there by
  itself as the spread reaches each body.
- **A joint's zero is how its bodies stood when it was made.** The ragdoll
  makes each one at the rig's rest and remakes it when its springs change, by
  standing the body at rest against its parent for the moment, so building from
  a bent pose is fine. Do not call `Join` on a ragdoll's ids yourself.
- **Bone lengths are the rig's, never the bodies'.** Only the root bone is
  moved. Below it a bone is turned and stays on its parent, so a stretched
  joint cannot stretch the mesh.
- **Driven characters cannot be made free**, and a driven body shoves one.
  Keep the ragdoll's bodies and the capsule in layers that ignore each other.
- `Hit` times come from a clock that only runs while `dt` is above nought, so a
  paused game does not spread a blow.
- Poses must be evaluated before `Ragdoll.Build` and before every solve.
