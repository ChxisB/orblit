using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit
{
    public class SignalAwaiter : IAwaiter<Variant[]>, IAwaitable<Variant[]>
    {
        private bool _completed;
        private Variant[] _result;
        private Action _continuation;

        public SignalAwaiter(OrblitObject source, StringName signal, OrblitObject target)
        {
            var awaiterGcHandle = CustomGCHandle.AllocStrong(this);
            using orblit_string_name signalSrc = NativeFuncs.orblitsharp_string_name_new_copy(
                (orblit_string_name)(signal?.NativeValue ?? default));
            NativeFuncs.orblitsharp_internal_signal_awaiter_connect(OrblitObject.GetPtr(source), in signalSrc,
                OrblitObject.GetPtr(target), GCHandle.ToIntPtr(awaiterGcHandle));
        }

        public bool IsCompleted => _completed;

        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
        }

        public Variant[] GetResult() => _result;

        public IAwaiter<Variant[]> GetAwaiter() => this;

        [UnmanagedCallersOnly]
        internal static unsafe void SignalCallback(IntPtr awaiterGCHandlePtr, orblit_variant** args, int argCount,
            orblit_bool* outAwaiterIsNull)
        {
            try
            {
                var awaiter = (SignalAwaiter)GCHandle.FromIntPtr(awaiterGCHandlePtr).Target;

                if (awaiter == null)
                {
                    *outAwaiterIsNull = orblit_bool.True;
                    return;
                }

                *outAwaiterIsNull = orblit_bool.False;

                awaiter._completed = true;

                if (argCount > 0)
                {
                    Variant[] signalArgs = new Variant[argCount];

                    for (int i = 0; i < argCount; i++)
                        signalArgs[i] = Variant.CreateCopyingBorrowed(*args[i]);

                    awaiter._result = signalArgs;
                }
                else
                {
                    awaiter._result = [];
                }

                awaiter._continuation?.Invoke();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outAwaiterIsNull = orblit_bool.False;
            }
        }
    }
}
