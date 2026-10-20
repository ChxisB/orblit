# Changelog

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

- `Doubles.ToDartString`, which is Dart's `double.toString`.

## 0.8.0

- `Doubles.Ceil`, which is Dart's `double.ceil()`.
- `Vector2.Zero`, which the sprite port reads.

## 0.7.0

- `Quaternion.Euler` and `Quaternion.Conjugate`, and `Vector3.Normalize` in
  place, which the camera port reads.
- `Doubles.ToStringAsFixed`, which is Dart's `toStringAsFixed`.

## 0.6.0

- `Quaternion` gains `Length`, `Length2` and `Normalize`, which the sequence
  port reads.
- `Doubles.Round`, which is Dart's `double.round()`.

## 0.5.0

- `Vector2`, with what the collide port reads: `All`, `+`, `-`, `*` and `/` by a
  scalar, unary `-`, `Length`, `Length2` and `Clone`.
- `Vector3` gains an indexer. A position other than 0 to 2 throws.
- `Doubles.Sign`, which is Dart's `double.sign`.

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
- `Doubles.Mod` and `Doubles.Floor`, which are Dart's `%` and `floor()` on doubles.

## 0.1.0

- `Vector3`, a mutable class of three doubles, as `vector_math_64` has it.
  Only what the ports so far read is here. Each later port adds what it needs.
- `Doubles.Compare` and `Doubles.Clamp`, which order NaN and the zeros as Dart does.
