using Orblit;
using Orblit.NativeInterop;

partial class ExportDiagnostics_ORB0111
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Orblit.Entity.PropertyName {
        /// <summary>
        /// Cached name for the 'MyButtonGet' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonGet = "MyButtonGet";
        /// <summary>
        /// Cached name for the 'MyButtonGetSet' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonGetSet = "MyButtonGetSet";
        /// <summary>
        /// Cached name for the 'MyButtonGetWithBackingField' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonGetWithBackingField = "MyButtonGetWithBackingField";
        /// <summary>
        /// Cached name for the 'MyButtonGetSetWithBackingField' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonGetSetWithBackingField = "MyButtonGetSetWithBackingField";
        /// <summary>
        /// Cached name for the 'MyButtonOkWithCallableCreationExpression' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonOkWithCallableCreationExpression = "MyButtonOkWithCallableCreationExpression";
        /// <summary>
        /// Cached name for the 'MyButtonOkWithImplicitCallableCreationExpression' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonOkWithImplicitCallableCreationExpression = "MyButtonOkWithImplicitCallableCreationExpression";
        /// <summary>
        /// Cached name for the 'MyButtonOkWithCallableFromExpression' property.
        /// </summary>
        public new static readonly global::Orblit.StringName @MyButtonOkWithCallableFromExpression = "MyButtonOkWithCallableFromExpression";
        /// <summary>
        /// Cached name for the '_backingField' field.
        /// </summary>
        public new static readonly global::Orblit.StringName @_backingField = "_backingField";
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitPropertyTrampolines(global::Orblit.Bridge.PropertyTrampolineCollector collector)
        {
            static orblit_variant trampoline_get_MyButtonGet(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGet;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get_MyButtonGetSet(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGetSet;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static void trampoline_set_MyButtonGetSet(object orblitObject, in orblit_variant value)
            {
                ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGetSet = global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.Callable>(value);
            }
            static orblit_variant trampoline_get_MyButtonGetWithBackingField(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGetWithBackingField;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get_MyButtonGetSetWithBackingField(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGetSetWithBackingField;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static void trampoline_set_MyButtonGetSetWithBackingField(object orblitObject, in orblit_variant value)
            {
                ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonGetSetWithBackingField = global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.Callable>(value);
            }
            static orblit_variant trampoline_get_MyButtonOkWithCallableCreationExpression(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonOkWithCallableCreationExpression;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get_MyButtonOkWithImplicitCallableCreationExpression(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonOkWithImplicitCallableCreationExpression;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get_MyButtonOkWithCallableFromExpression(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@MyButtonOkWithCallableFromExpression;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static orblit_variant trampoline_get__backingField(object orblitObject)
            {
                var ret = ((global::ExportDiagnostics_ORB0111)orblitObject).@_backingField;
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Callable>(ret);
            }
            static void trampoline_set__backingField(object orblitObject, in orblit_variant value)
            {
                ((global::ExportDiagnostics_ORB0111)orblitObject).@_backingField = global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.Callable>(value);
            }
            var aux_delegate_get_MyButtonGet = trampoline_get_MyButtonGet;
            collector.TryAdd(PropertyName.@MyButtonGet, new(aux_delegate_get_MyButtonGet.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get_MyButtonGetSet = trampoline_get_MyButtonGetSet;
            var aux_delegate_set_MyButtonGetSet = trampoline_set_MyButtonGetSet;
            collector.TryAdd(PropertyName.@MyButtonGetSet, new(aux_delegate_get_MyButtonGetSet.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set_MyButtonGetSet.Method.MethodHandle.GetFunctionPointer()));
            var aux_delegate_get_MyButtonGetWithBackingField = trampoline_get_MyButtonGetWithBackingField;
            collector.TryAdd(PropertyName.@MyButtonGetWithBackingField, new(aux_delegate_get_MyButtonGetWithBackingField.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get_MyButtonGetSetWithBackingField = trampoline_get_MyButtonGetSetWithBackingField;
            var aux_delegate_set_MyButtonGetSetWithBackingField = trampoline_set_MyButtonGetSetWithBackingField;
            collector.TryAdd(PropertyName.@MyButtonGetSetWithBackingField, new(aux_delegate_get_MyButtonGetSetWithBackingField.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set_MyButtonGetSetWithBackingField.Method.MethodHandle.GetFunctionPointer()));
            var aux_delegate_get_MyButtonOkWithCallableCreationExpression = trampoline_get_MyButtonOkWithCallableCreationExpression;
            collector.TryAdd(PropertyName.@MyButtonOkWithCallableCreationExpression, new(aux_delegate_get_MyButtonOkWithCallableCreationExpression.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get_MyButtonOkWithImplicitCallableCreationExpression = trampoline_get_MyButtonOkWithImplicitCallableCreationExpression;
            collector.TryAdd(PropertyName.@MyButtonOkWithImplicitCallableCreationExpression, new(aux_delegate_get_MyButtonOkWithImplicitCallableCreationExpression.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get_MyButtonOkWithCallableFromExpression = trampoline_get_MyButtonOkWithCallableFromExpression;
            collector.TryAdd(PropertyName.@MyButtonOkWithCallableFromExpression, new(aux_delegate_get_MyButtonOkWithCallableFromExpression.Method.MethodHandle.GetFunctionPointer()), new(global::System.IntPtr.Zero));
            var aux_delegate_get__backingField = trampoline_get__backingField;
            var aux_delegate_set__backingField = trampoline_set__backingField;
            collector.TryAdd(PropertyName.@_backingField, new(aux_delegate_get__backingField.Method.MethodHandle.GetFunctionPointer()), new(aux_delegate_set__backingField.Method.MethodHandle.GetFunctionPointer()));
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
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@_backingField, hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@MyButtonOkWithCallableCreationExpression, hint: (global::Orblit.PropertyHint)37, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4, exported: true));
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@MyButtonOkWithImplicitCallableCreationExpression, hint: (global::Orblit.PropertyHint)37, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4, exported: true));
        properties.Add(new(type: (global::Orblit.Variant.Type)25, name: PropertyName.@MyButtonOkWithCallableFromExpression, hint: (global::Orblit.PropertyHint)37, hintString: "", usage: (global::Orblit.PropertyUsageFlags)4, exported: true));
        return properties;
    }
#pragma warning restore CS0109
}
