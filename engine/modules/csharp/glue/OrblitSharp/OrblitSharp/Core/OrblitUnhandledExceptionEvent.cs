using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit
{
    public static partial class Orb
    {
        [UnmanagedCallersOnly]
        internal static void OnCoreApiAssemblyLoaded(orblit_bool isDebug)
        {
            try
            {
                Dispatcher.InitializeDefaultOrblitTaskScheduler();

                if (isDebug.ToBool())
                {
                    DebuggingUtils.InstallTraceListener();

                    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                    {
                        // Exception.ToString() includes the inner exception
                        ExceptionUtils.LogUnhandledException((Exception)e.ExceptionObject);
                    };
                }
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }
    }
}
