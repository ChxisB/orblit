using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// A placement in the layout of the engine's Transform3D: the three rows of the basis, then the origin. A point
/// lands at a row dotted with the point, plus the origin.
/// </summary>
public readonly record struct Transform3D(Vector3 Row0, Vector3 Row1, Vector3 Row2, Vector3 Origin);
