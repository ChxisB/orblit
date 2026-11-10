using Orblit;
using Orblit.NativeInterop;

partial class ScriptBoilerplate
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Orblit.Entity.MethodName {
        /// <summary>
        /// Cached name for the 'OnFrame' method.
        /// </summary>
        public new static readonly global::Orblit.StringName @OnFrame = "OnFrame";
        /// <summary>
        /// Cached name for the 'Bazz' method.
        /// </summary>
        public new static readonly global::Orblit.StringName @Bazz = "Bazz";
    }
    /// <summary>
    /// Get the method information for all the methods declared in this class.
    /// This method is used by Orblit to register the available methods in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo> GetOrblitMethodList()
    {
        var methods = new global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo>(2);
        methods.Add(new(name: MethodName.@OnFrame, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)3, name: "delta", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        methods.Add(new(name: MethodName.@Bazz, returnVal: new(type: (global::Orblit.Variant.Type)2, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)21, name: "name", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        return methods;
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitMethodTrampolines(global::Orblit.Bridge.MethodTrampolineCollector collector)
        {
            static orblit_variant trampoline_1_OnFrame(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 1) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 1, provided: args.Count);
                    return default;
                }
                ((global::Orblit.Entity)orblitObject).@OnFrame(global::Orblit.NativeInterop.VariantUtils.ConvertTo<double>(args[0]));
                return default;
            }
            var aux_delegate_1_OnFrame = trampoline_1_OnFrame;
            collector.TryAdd(new(MethodName.@OnFrame, 1), new(aux_delegate_1_OnFrame.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
            collector.TryAdd(new(global::Orblit.Entity.MethodName.@OnFrame, 1), new(aux_delegate_1_OnFrame.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
            static orblit_variant trampoline_1_Bazz(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 1) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 1, provided: args.Count);
                    return default;
                }
                var callRet = ((global::ScriptBoilerplate)orblitObject).@Bazz(global::Orblit.NativeInterop.VariantUtils.ConvertTo<global::Orblit.StringName>(args[0]));
                return global::Orblit.NativeInterop.VariantUtils.CreateFrom<int>(callRet);
            }
            var aux_delegate_1_Bazz = trampoline_1_Bazz;
            collector.TryAdd(new(MethodName.@Bazz, 1), new(aux_delegate_1_Bazz.Method.MethodHandle.GetFunctionPointer(), isStatic: false));
        }
    }
#pragma warning restore CS0109
}
