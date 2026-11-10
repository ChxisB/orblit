using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit.Bridge
{
    internal static class GCHandleBridge
    {
        [UnmanagedCallersOnly]
        internal static void FreeGCHandle(IntPtr gcHandlePtr)
        {
            try
            {
                CustomGCHandle.Free(GCHandle.FromIntPtr(gcHandlePtr));
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }

        // Returns true, if releasing the provided handle is necessary for assembly unloading to succeed.
        // This check is not perfect and only intended to prevent things in OrblitTools from being reloaded.
        [UnmanagedCallersOnly]
        internal static orblit_bool GCHandleIsTargetCollectible(IntPtr gcHandlePtr)
        {
            try
            {
                var target = GCHandle.FromIntPtr(gcHandlePtr).Target;

                if (target is Delegate @delegate)
                    return DelegateUtils.IsDelegateCollectible(@delegate).ToOrblitBool();

                return target.GetType().IsCollectible.ToOrblitBool();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                return orblit_bool.True;
            }
        }
    }
}
