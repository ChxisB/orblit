using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>A box in the layout of the engine's AABB: the corner with the smallest coordinates, then the size.</summary>
public readonly record struct Aabb(Vector3 Position, Vector3 Size);
