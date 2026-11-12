using Orblit;
using Orblit.NativeInterop;

partial class GenericSignals<T>
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the signals contained in this class, for fast lookup.
    /// </summary>
    public new class SignalName : global::Orblit.OrblitObject.SignalName {
        /// <summary>
        /// Cached name for the 'MySignal' signal.
        /// </summary>
        public new static readonly global::Orblit.StringName @MySignal = "MySignal";
    }
    /// <summary>
    /// Get the signal information for all the signals declared in this class.
    /// This method is used by Orblit to register the available signals in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo> GetOrblitSignalList()
    {
        var signals = new global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo>(1);
        signals.Add(new(name: SignalName.@MySignal, returnVal: new(type: (global::Orblit.Variant.Type)0, name: "", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false), flags: (global::Orblit.MethodFlags)1, arguments: new() { new(type: (global::Orblit.Variant.Type)2, name: "num", hint: (global::Orblit.PropertyHint)0, hintString: "", usage: (global::Orblit.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        return signals;
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitRaiseSignalTrampolines(global::Orblit.Bridge.RaiseSignalTrampolineCollector collector)
        {
            static void trampoline_1_MySignal(object orblitObject, NativeVariantPtrArgs args, ref orblit_variant_call_error callError)
            {
                if (args.Count != 1) {
                    callError = orblit_variant_call_error.CreateInvalidArgumentCountError(expected: 1, provided: args.Count);
                    return;
                }
                ((global::GenericSignals<T>)orblitObject).backing_MySignal?.Invoke(global::Orblit.NativeInterop.VariantUtils.ConvertTo<int>(args[0]));
            }
            var aux_delegate_1_MySignal = trampoline_1_MySignal;
            collector.TryAdd(new(SignalName.@MySignal, 1), new(aux_delegate_1_MySignal.Method.MethodHandle.GetFunctionPointer()));
        }
    }
#pragma warning restore CS0109
    private global::GenericSignals<T>.MySignalEventHandler backing_MySignal;
    /// <inheritdoc cref="global::GenericSignals{T}.MySignalEventHandler"/>
    public event global::GenericSignals<T>.MySignalEventHandler @MySignal {
        add => backing_MySignal += value;
        remove => backing_MySignal -= value;
    }
    protected void EmitSignalMySignal(int @num)
    {
        EmitSignal(SignalName.MySignal, [@num]);
    }
}
