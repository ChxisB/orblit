using Orblit;
using Orblit.NativeInterop;

partial class InheritanceBase
{
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void SaveOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.SaveOrblitObjectData(info);
        info.AddProperty(PropertyName.@MyString, global::Orblit.Variant.From<string>(this.@MyString));
        info.AddProperty(PropertyName.@MyInteger, global::Orblit.Variant.From<int>(this.@MyInteger));
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void RestoreOrblitObjectData(global::Orblit.Bridge.OrblitSerializationInfo info)
    {
        base.RestoreOrblitObjectData(info);
        if (info.TryGetProperty(PropertyName.@MyString, out var _value_MyString))
            this.@MyString = _value_MyString.As<string>();
        if (info.TryGetProperty(PropertyName.@MyInteger, out var _value_MyInteger))
            this.@MyInteger = _value_MyInteger.As<int>();
    }
}
