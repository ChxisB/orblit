using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>Where a one-shot has got to after a step, and the marks it passed on the way.</summary>
public sealed class LayerStep(LayerPlace place, IReadOnlyList<Mark> marks, bool finished)
{
    public LayerPlace Place { get; } = place;

    public IReadOnlyList<Mark> Marks { get; } = marks.ToList().AsReadOnly();

    public bool Finished { get; } = finished;
}
