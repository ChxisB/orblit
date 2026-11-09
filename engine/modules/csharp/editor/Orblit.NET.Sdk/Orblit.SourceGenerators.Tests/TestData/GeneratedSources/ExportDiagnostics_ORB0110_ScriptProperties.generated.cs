using Orblit;
using Orblit.NativeInterop;

partial class ExportDiagnostics_ORB0110
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.Entity.PropertyName {
        /// <summary>
        /// Cached name for the 'MyButton' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButton = "MyButton";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get_MyButton(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0110)orblitObject).@MyButton;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<int>(ret);
            }
            var aux_delegate_get_MyButton = trampoline_get_MyButton;
            collector.TryAdd(PropertyName.@MyButton, new(aux_delegate_get_MyButton.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
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
        return properties;
    }
#pragma warning restore CS0109
}
