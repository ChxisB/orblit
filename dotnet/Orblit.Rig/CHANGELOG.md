# Changelog

## 0.3.0

- A port of `orblit_rig` 0.3.0.
- `TrackAxis`, `BoneRole` and `Side` are enums with their data in static
  extension classes (`TrackAxes`, `BoneRoles`, `Sides`).
- The free functions are static members. The solvers
  are in `Ik`, `BoneEdit` holds `PlaceBone`, `TurnBone`, `AimBone`,
  `BonePath` and `RequireBone`. `TwoBoneIk.Reach`, `ChainIk.PointAlong`,
  `SplineIk.SampleCurve` and `SplineIk.WalkCurve` sit with the modifier
  that owns them.
- A bone that is not in the armature throws `ArmatureException`.
- A constructor that refuses a value throws `ArgumentException` or its
  `ArgumentOutOfRangeException` child.
- `RigTypes.cs` and `FaceRigTypes.cs` share one `RigDeform` helper.
