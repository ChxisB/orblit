import 'package:orblit_noise/orblit_noise.dart';
import 'package:vector_math/vector_math_64.dart';

/// Writes a smooth vector for the point to [out]. Three offsets of one field
/// give three components that are not the same number.
///
/// The pure and the stepped form both read it, so they push a particle the
/// same way.
void swirl(GradientNoise noise, double x, double y, double z, Vector3 out) {
  out.setValues(
    noise.at(x, y, z),
    noise.at(x + 31.7, y, z),
    noise.at(x + 67.3, y, z),
  );
}
