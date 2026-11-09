using Orblit;
using Orblit.NativeInterop;

partial class Methods
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Orblit.OrblitObject.MethodName {
        /// <summary>
        /// Cached name for the 'MethodWithOverload' method.
        /// </summary>
        public new static readonly global::Orblit.StringName @MethodWithOverload = "MethodWithOverload";
    }
    /// <summary>
    /// Get the method information for all the methods declared in this class.
    /// This method is used by Orblit to register the available methods in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo> GetOrblitMethodList()
    {
        var methods = new global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo>(3);
        methods.Add(new(name: MethodName.@MethodWithOverload, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@MethodWithOverload, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)2, name: "a", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        methods.Add(new(name: MethodName.@MethodWithOverload, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)2, name: "a", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), new(type: (global::Orblit.Variant.Type)2, name: "b", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        return methods;
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitMethodTrampolines(global::Orblit.Bridge.MethodTrampolineCollector collector)
        {
            static orblit_variant trampoline_0_MethodWithOverload(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 0) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 0, provided: args.Count);
                    return default;
                }
                ((global::Methods)orblitObject).@MethodWithOverload();
                return default;
            }
            var aux_delegate_0_MethodWithOverload = trampoline_0_MethodWithOverload;
            collector.TryAdd(new(MethodName.@MethodWithOverload, 0), new(aux_delegate_0_MethodWithOverload.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
            static orblit_variant trampoline_1_MethodWithOverload(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 1) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 1, provided: args.Count);
                    return default;
                }
                ((global::Methods)orblitObject).@MethodWithOverload(global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(args[0]));
                return default;
            }
            var aux_delegate_1_MethodWithOverload = trampoline_1_MethodWithOverload;
            collector.TryAdd(new(MethodName.@MethodWithOverload, 1), new(aux_delegate_1_MethodWithOverload.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
            static orblit_variant trampoline_2_MethodWithOverload(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 2) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 2, provided: args.Count);
                    return default;
                }
                ((global::Methods)orblitObject).@MethodWithOverload(global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(args[0]), global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(args[1]));
                return default;
            }
            var aux_delegate_2_MethodWithOverload = trampoline_2_MethodWithOverload;
            collector.TryAdd(new(MethodName.@MethodWithOverload, 2), new(aux_delegate_2_MethodWithOverload.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
        }
    }
#pragma warning restore CS0109
}
