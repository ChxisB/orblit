using Orblit;
using Orblit.NativeInterop;

partial class ExportedToolButtons
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.OrblitObject.PropertyName {
        /// <summary>
        /// Cached name for the 'MyButton1' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButton1 = "MyButton1";
        /// <summary>
        /// Cached name for the 'MyButton2' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButton2 = "MyButton2";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get_MyButton1(object orblitObject)
            {
                var ret = ((global::ExportedToolButtons)orblitObject).@MyButton1;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get_MyButton2(object orblitObject)
            {
                var ret = ((global::ExportedToolButtons)orblitObject).@MyButton2;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            var aux_delegate_get_MyButton1 = trampoline_get_MyButton1;
            collector.TryAdd(PropertyName.@MyButton1, new(aux_delegate_get_MyButton1.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get_MyButton2 = trampoline_get_MyButton2;
            collector.TryAdd(PropertyName.@MyButton2, new(aux_delegate_get_MyButton2.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
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
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@MyButton1, hint: (global::Orblit.PropertyHint)37, hintString: "Click me!", usage: (global::Orblit.PropertyUsageFlags)4, exported: true));
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@MyButton2, hint: (global::Orblit.PropertyHint)37, hintString: "Click me!,ColorRect", usage: (global::Orblit.PropertyUsageFlags)4, exported: true));
        return properties;
    }
#pragma warning restore CS0109
}
