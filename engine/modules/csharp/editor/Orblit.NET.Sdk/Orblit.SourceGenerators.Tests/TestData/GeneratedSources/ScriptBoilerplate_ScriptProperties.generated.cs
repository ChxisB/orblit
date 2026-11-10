using Orblit;
using Orblit.NativeInterop;

partial class ScriptBoilerplate
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.Entity.PropertyName {
        /// <summary>
        /// Cached name for the '_nodePath' field.
        /// </summary>
        public new static readonly global::Orblit.StringName @_nodePath = "_nodePath";
        /// <summary>
        /// Cached name for the '_velocity' field.
        /// </summary>
        public new static readonly global::Orblit.StringName @_velocity = "_velocity";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get__nodePath(object orblitObject)
            {
                var ret = ((global::ScriptBoilerplate)orblitObject).@_nodePath;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.EntityPath>(ret);
            }
            static void trampoline_set__nodePath(object orblitObject, in orblit_variant value)
            {
                ((global::ScriptBoilerplate)orblitObject).@_nodePath = global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.EntityPath>(value);
            }
            static orblit_variant trampoline_get__velocity(object orblitObject)
            {
                var ret = ((global::ScriptBoilerplate)orblitObject).@_velocity;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<int>(ret);
            }
            static void trampoline_set__velocity(object orblitObject, in orblit_variant value)
            {
                ((global::ScriptBoilerplate)orblitObject).@_velocity = global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(value);
            }
            var aux_delegate_get__nodePath = trampoline_get__nodePath;
            var aux_delegate_set__nodePath = trampoline_set__nodePath;
            collector.TryAdd(PropertyName.@_nodePath, new(aux_delegate_get__nodePath.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set__nodePath.Method.MethodHandle.GetFunctionPointer()));
            var aux_delegate_get__velocity = trampoline_get__velocity;
            var aux_delegate_set__velocity = trampoline_set__velocity;
            collector.TryAdd(PropertyName.@_velocity, new(aux_delegate_get__velocity.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set__velocity.Method.MethodHandle.GetFunctionPointer()));
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
        properties.Add(new(type: (global::Orblit.Variant.Type)22, name: PropertyName.@_nodePath, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Orblit.Variant.Type)2, name: PropertyName.@_velocity, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        return properties;
    }
#pragma warning restore CS0109
}
