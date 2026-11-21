namespace Orblit.Editor.Bridge;

/// <summary>An entity taken out of the scene and kept: where it sat, and the entities under it that the scene root owned.</summary>
public sealed record DetachedEntity(nint Entity, nint Parent, int Index, IReadOnlyList<nint> Owned);
