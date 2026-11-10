using Orblit;
using Orblit.NativeInterop;

partial struct OuterClass
{
partial class NestedClass
{
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void SaveOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.SaveOrblitObjectData(info);
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void RestoreOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.RestoreOrblitObjectData(info);
    }
}
}
