using Orblit;
using Orblit.NativeInterop;

partial class AllWriteOnly
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.OrblitObject.PropertyName {
        /// <summary>
        /// Cached name for the 'WriteOnlyProperty' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @WriteOnlyProperty = "WriteOnlyProperty";
        /// <summary>
        /// Cached name for the '_writeOnlyBackingField' field.
        /// </summary>
        public new static readonly global::Orblit.StringName @_writeOnlyBackingField = "_writeOnlyBackingField";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static void trampoline_set_WriteOnlyProperty(object orblitObject, in orblit_variant value)
            {
                ((global::AllWriteOnly)orblitObject).@WriteOnlyProperty = global::Orblit.NativeInterop.VariantUtils.ConvertTo<bool>(value);
            }
            static orblit_variant trampoline_get__writeOnlyBackingField(object orblitObject)
            {
                var ret = ((global::AllWriteOnly)orblitObject).@_writeOnlyBackingField;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<bool>(ret);
            }
            static void trampoline_set__writeOnlyBackingField(object orblitObject, in orblit_variant value)
            {
                ((global::AllWriteOnly)orblitObject).@_writeOnlyBackingField = global::Orblit.NativeInterop.VariantUtils.ConvertTo<bool>(value);
            }
            var aux_delegate_set_WriteOnlyProperty = trampoline_set_WriteOnlyProperty;
            collector.TryAdd(PropertyName.@WriteOnlyProperty, new(global::System.IntPtr.Zero), new(aux_delegate_set_WriteOnlyProperty.Method.MethodHandle.GetFunctionPointer()));
            var aux_delegate_get__writeOnlyBackingField = trampoline_get__writeOnlyBackingField;
            var aux_delegate_set__writeOnlyBackingField = trampoline_set__writeOnlyBackingField;
            collector.TryAdd(PropertyName.@_writeOnlyBackingField, new(aux_delegate_get__writeOnlyBackingField.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set__writeOnlyBackingField.Method.MethodHandle.GetFunctionPointer()));
        }
    }
    /// <summary>
    /// Get the property information for all the properties declared in this class.
    /// This method is used by Orblit to register the available properties in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Orblit.Bridge.PropertyInfo> GetOrblitPropertyList()
    {
        var properties = new global::System.Collections.Generic.List<global::Orblit.Bridge.PropertyInfo>();
        properties.Add(new(type: (global::Orblit.Variant.Type)1, name: PropertyName.@_writeOnlyBackingField, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Orblit.Variant.Type)1, name: PropertyName.@WriteOnlyProperty, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        return properties;
    }
#pragma warning restore CS0109
}
