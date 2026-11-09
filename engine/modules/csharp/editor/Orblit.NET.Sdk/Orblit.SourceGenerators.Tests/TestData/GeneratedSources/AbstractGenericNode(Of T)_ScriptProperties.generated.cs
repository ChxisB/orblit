using Orblit;
using Orblit.NativeInterop;

partial class AbstractGenericNode<T>
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.Entity.PropertyName {
        /// <summary>
        /// Cached name for the 'MyArray' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyArray = "MyArray";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get_MyArray(object orblitObject)
            {
                var ret = ((global::AbstractGenericNode<T>)orblitObject).@MyArray;
                return global::Orblit.NativeInterop.VariantUtils.CreateFromArray(ret);
            }
            static void trampoline_set_MyArray(object orblitObject, in orblit_variant value)
            {
                ((global::AbstractGenericNode<T>)orblitObject).@MyArray = global::Orblit.NativeInterop.VariantUtils.ConvertToArray<T>(value);
            }
            var aux_delegate_get_MyArray = trampoline_get_MyArray;
            var aux_delegate_set_MyArray = trampoline_set_MyArray;
            collector.TryAdd(PropertyName.@MyArray, new(aux_delegate_get_MyArray.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set_MyArray.Method.MethodHandle.GetFunctionPointer()));
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
        properties.Add(new(type: (global::Orblit.Variant.Type)28, name: PropertyName.@MyArray, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4102, exported: true));
        return properties;
    }
#pragma warning restore CS0109
}
