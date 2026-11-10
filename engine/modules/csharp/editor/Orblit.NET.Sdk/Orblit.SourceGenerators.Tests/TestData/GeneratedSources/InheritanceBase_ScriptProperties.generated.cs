using Orblit;
using Orblit.NativeInterop;

partial class InheritanceBase
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.Entity.PropertyName {
        /// <summary>
        /// Cached name for the 'MyString' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyString = "MyString";
        /// <summary>
        /// Cached name for the 'MyInteger' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyInteger = "MyInteger";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get_MyString(object orblitObject)
            {
                var ret = ((global::InheritanceBase)orblitObject).@MyString;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<string>(ret);
            }
            static void trampoline_set_MyString(object orblitObject, in orblit_variant value)
            {
                ((global::InheritanceBase)orblitObject).@MyString = global::Orblit.NativeInterop.VariantUtils.ConvertTo<string>(value);
            }
            static orblit_variant trampoline_get_MyInteger(object orblitObject)
            {
                var ret = ((global::InheritanceBase)orblitObject).@MyInteger;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<int>(ret);
            }
            static void trampoline_set_MyInteger(object orblitObject, in orblit_variant value)
            {
                ((global::InheritanceBase)orblitObject).@MyInteger = global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(value);
            }
            var aux_delegate_get_MyString = trampoline_get_MyString;
            var aux_delegate_set_MyString = trampoline_set_MyString;
            collector.TryAdd(PropertyName.@MyString, new(aux_delegate_get_MyString.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set_MyString.Method.MethodHandle.GetFunctionPointer()));
            var aux_delegate_get_MyInteger = trampoline_get_MyInteger;
            var aux_delegate_set_MyInteger = trampoline_set_MyInteger;
            collector.TryAdd(PropertyName.@MyInteger, new(aux_delegate_get_MyInteger.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set_MyInteger.Method.MethodHandle.GetFunctionPointer()));
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
        properties.Add(new(type: (global::Orblit.Variant.Type)4, name: PropertyName.@MyString, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Orblit.Variant.Type)2, name: PropertyName.@MyInteger, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        return properties;
    }
#pragma warning restore CS0109
}
