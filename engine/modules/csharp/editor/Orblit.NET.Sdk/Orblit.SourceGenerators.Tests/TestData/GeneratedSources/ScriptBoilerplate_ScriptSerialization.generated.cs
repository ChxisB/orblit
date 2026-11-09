using Orblit;
using Orblit.NativeInterop;

partial class ScriptBoilerplate
{
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void SaveOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.SaveOrblitObjectData(info);
        info.AddProperty(PropertyName.@_nodePath, global::Orblit.Variant.From<global::Orblit.EntityPath>(this.@_nodePath));
        info.AddProperty(PropertyName.@_velocity, global::Orblit.Variant.From<int>(this.@_velocity));
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void RestoreOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.RestoreOrblitObjectData(info);
        if (info.TryGetProperty(PropertyName.@_nodePath, out var _value__nodePath))
            this.@_nodePath = _value__nodePath.As<global::Orblit.EntityPath>();
        if (info.TryGetProperty(PropertyName.@_velocity, out var _value__velocity))
            this.@_velocity = _value__velocity.As<int>();
    }
}
