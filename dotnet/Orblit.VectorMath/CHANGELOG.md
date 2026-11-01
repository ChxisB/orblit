# Changelog

## 0.20.0

- `Matrix4.TranslateByDouble`, which the motion blur example reads.

## 0.19.0

- `Matrix4.RotateX`, `RotateY` and `ScaleByDouble`, which the examples read.

## 0.18.0

- `Vector2.Normalize` and `Normalized`, which the input actions read.

## 0.17.0

- `Matrix4.FromList`, which the model file description reads.

## 0.16.0

- `Quaternion.Inverted` and `Inverse`, which divide by the squared length, and
  `Quaternion.FromTwoVectors`, which the clip retarget reads.

## 0.15.0

- `Matrix4.RotationY`, which the scene export reads.

## 0.14.0

- `Matrix3` gains `RotationX`, `RotationY`, `RotationZ` and `Multiply`, which
  the scene transform reads.
- `Doubles.Radians` and `Doubles.Degrees`, which round as `vector_math` does.

## 0.13.0

- `Matrix3` gains `Determinant`, `CopyInverse`, `Transpose` and
  `CopyNormalMatrix`, which the model bake reads.
- `Matrix4.RotationX` and `RotateZ`.

## 0.12.0

- `Vector2.DistanceTo` and `DistanceToSquared`, which the path port reads.

## 0.11.0

- `Vector2.Dot`, which the mesh port reads.

## 0.10.0

- `Matrix4` gains `Zero`, `Clone`, `SetFrom`, `Multiply`, `Multiplied`,
  `Determinant`, `CopyInverse`, `Inverted`, `Decompose`, `Rotate3`, `Rotated3`
  and `Transformed3`, which the rig port reads.
- `Matrix4.TranslationValues`, `Diagonal3Values` and `RotationZ`.
- `Matrix3.Transform` and `Transformed`.
- `Quaternion` gains `SetFrom`, `SetValues`, `Conjugated`, `Normalized` and
  `AsRotationMatrix`.
- `Vector3` gains `SetFrom`, `SetZero`, `Add`, `AddScaled`, `Scale` and
  `Scaled`.

## 0.9.0

- `Doubles.ToFileString`, the text of a double in a file.

## 0.8.0

- `Doubles.Ceil`, the ceiling of a double.
- `Vector2.Zero`, which the sprite port reads.

## 0.7.0

- `Quaternion.Euler` and `Quaternion.Conjugate`, and `Vector3.Normalize` in
  place, which the camera port reads.
- `Doubles.ToStringAsFixed`, fixed-point text.

## 0.6.0

- `Quaternion` gains `Length`, `Length2` and `Normalize`, which the sequence
  port reads.
- `Doubles.Round`, rounds a double, halves away from zero.

## 0.5.0

- `Vector2`, with what the collide port reads: `All`, `+`, `-`, `*` and `/` by a
  scalar, unary `-`, `Length`, `Length2` and `Clone`.
- `Vector3` gains an indexer. A position other than 0 to 2 throws.
- `Doubles.Sign`, the sign of a double.

## 0.4.0

- `Vector3` gains `DistanceTo` and `DistanceToSquared`, which the agent port
  reads.

## 0.3.0

- `Vector3` gains `/` by a scalar, unary `-`, `Dot`, `Cross` and `SetValues`,
  which the audio port reads.

## 0.2.0

- `Vector3` gains `Zero`, `All`, `+`, `-`, `*` by a scalar, `Length`, `Length2`
  and `Normalized`.
- `Vector4`, `Quaternion`, `Matrix3` and `Matrix4`, with what the effect port
  reads: `Quaternion.AxisAngle`, `FromRotation` and `*`, and `Matrix4.Compose`,
  `GetTranslation`, `SetTranslation`, `GetRotation`, `GetColumn` and `Transform3`.
- `Doubles.Mod` and `Doubles.Floor`, the remainder and the floor of doubles.

## 0.1.0

- `Vector3`, a mutable class of three doubles, as `vector_math_64` has it.
  Only what the ports so far read is here. Each later port adds what it needs.
- `Doubles.Compare` and `Doubles.Clamp`, which order NaN and the zeros as a total order.
