using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>A straight segment to draw over a scene, in world space. <paramref name="Colour"/> is linear red, green and blue.</summary>
public sealed record DebugLine(Vector3 From, Vector3 To, Vector3 Colour);
