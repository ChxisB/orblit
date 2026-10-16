# Changelog

## 0.3.0

- **A modifier stack.** `ModifierStack` runs steps between playback and
  skinning. Evaluate the pose, `apply` the stack, then take the joints. Each
  step is a `Modifier` in a slot with an `influence` from 0 to 1. A slot can
  be switched off, or read its influence from a pose property, so a clip can
  hand a limb between playback and reaching. The stack does the blending, so
  no modifier blends itself. `ModifierFrame` carries the frame's `dt` and the
  model's place in the world.
- Reaching and aiming: `TwoBoneIk`, `ChainIk` (FABRIK), `SplineIk` and
  `LookAt`, which eases and keeps its turn inside a limit.
- Limits: `ConeLimit` and `HingeLimit` keep a bone's swing and twist inside a
  range. `SpeedLimit` caps how fast bones turn and move. `Constrain` runs a
  `BoneConstraint` as a step.
- `TwistSpread` shares a forearm's roll along the bones between it and the
  wrist. `BoneAttachment` follows a bone and reports where it is, in the model
  and in the world.
- `SpringBones` swings a chain in the world, so a character that stops leaves
  its hair moving. It has sphere, capsule and plane colliders that a tail
  slides along at the bone's length.
- `FootPlacement` stands feet on any ground a `GroundProbe` can find, keeps
  the height the animation lifts a foot to, lowers the pelvis, and tilts the
  foot to the slope.
- `Pose.refresh` works bones out again after their transforms were edited.
  Unlike `evaluate` it does not solve inverse-kinematics chains, so it gives
  the same answer however often it runs.
- `placeBone`, `turnBone` and `aimBone` edit a bone in the model's space.
  `swingTwist`, `twistAngle`, `solveFabrik` and `sampleCurve` are the maths
  under the modifiers.
- **Fix.** The end of an `IkChain` that had to bend landed off its target. The
  lower bone was aimed from the head it had before the upper bone turned. It
  now lands on the target, so a rig that used `addIkChain` ends up in a
  slightly different place.

## 0.2.1

- **Two-bone IK bends the joint towards its pole.** It bent it away: the
  solver turned the limb with `Quaternion.rotated`, which turns by the inverse
  of the rotation it is given. A generated limb puts its pole on the side the
  elbow or knee already bends, so an arm under IK was bending backwards.
- A `BoneWidget`'s `roll` turns its shape the same way a bone's `roll` turns
  the bone. It turned it the other way.

## 0.2.0

- `BoneNaming.unique` makes a file's joint names fit to be bone names: an
  unnamed joint is called `joint` and its position, and a repeated name gets
  `.001`. It was private to `orblit_stage`; it is here so a clip imported from
  a model and the model it plays on name every bone the same way.

## 0.1.0

- First cut of `orblit_rig`. Pre-alpha: everything is subject to change.
