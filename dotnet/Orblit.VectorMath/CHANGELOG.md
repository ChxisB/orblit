# Changelog

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
