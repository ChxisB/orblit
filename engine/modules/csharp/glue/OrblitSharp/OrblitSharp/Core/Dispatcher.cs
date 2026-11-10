using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit
{
    public static class Dispatcher
    {
        internal static OrblitTaskScheduler DefaultOrblitTaskScheduler;

        internal static void InitializeDefaultOrblitTaskScheduler()
        {
            DefaultOrblitTaskScheduler?.Dispose();
            DefaultOrblitTaskScheduler = new OrblitTaskScheduler();
        }

        public static OrblitSynchronizationContext SynchronizationContext => DefaultOrblitTaskScheduler.Context;
    }
}
