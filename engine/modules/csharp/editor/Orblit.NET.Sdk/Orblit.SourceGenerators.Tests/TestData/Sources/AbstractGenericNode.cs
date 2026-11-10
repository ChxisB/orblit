using Orblit;

public abstract partial class AbstractGenericNode<[MustBeVariant] T> : Entity
{
    [Export] // This should be included, but without type hints.
    public Orblit.Collections.Array<T> MyArray { get; set; } = new();
}
