# Changelog

## 0.3.0

- A port of `orblit_rig` 0.3.0.
- `TrackAxis`, `BoneRole` and `Side` are enums with their data in static
  extension classes (`TrackAxes`, `BoneRoles`, `Sides`), where Dart put the
  data on the enum.
- The functions that were top level in Dart are static members. The solvers
  are in `Ik`, `BoneEdit` holds `PlaceBone`, `TurnBone`, `AimBone`,
  `BonePath` and `RequireBone`. `TwoBoneIk.Reach`, `ChainIk.PointAlong`,
  `SplineIk.SampleCurve` and `SplineIk.WalkCurve` sit with the modifier
  that owns them.
- A bone that is not in the armature throws `ArmatureException`, where Dart
  threw `ArmatureError` for some lookups and a `TypeError` for others.
- A constructor that refuses a value throws `ArgumentException` or its
  `ArgumentOutOfRangeException` child, as Dart threw `ArgumentError`.
- `RigTypes.cs` and `FaceRigTypes.cs` share one `RigDeform` helper, where
  Dart kept a private copy in each file.
