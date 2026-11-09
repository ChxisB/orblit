using Orblit;
using Orblit.NativeInterop;

partial struct OuterClass
{
partial class NestedClass
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Orblit.RefCounted.MethodName {
        /// <summary>
        /// Cached name for the '_Get' method.
        /// </summary>
        public new static readonly global::Orblit.StringName @_Get = "_Get";
    }
    /// <summary>
    /// Get the method information for all the methods declared in this class.
    /// This method is used by Orblit to register the available methods in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo> GetOrblitMethodList()
    {
        var methods = new global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo>(1);
        methods.Add(new(name: MethodName.@_Get, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)131078, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)21, name: "property", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        return methods;
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitMethodTrampolines(global::Orblit.Bridge.MethodTrampolineCollector collector)
        {
            static orblit_variant trampoline_1__Get(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 1) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 1, provided: args.Count);
                    return default;
                }
                var callRet = ((global::Orblit.OrblitObject)orblitObject).@_Get(global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.StringName>(args[0]));
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<global::Orblit.Variant>(callRet);
            }
            var aux_delegate_1__Get = trampoline_1__Get;
            collector.TryAdd(new(MethodName.@_Get, 1), new(aux_delegate_1__Get.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
            collector.TryAdd(new(global::Orblit.OrblitObject.MethodName.@_Get, 1), new(aux_delegate_1__Get.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
        }
    }
#pragma warning restore CS0109
}
}
