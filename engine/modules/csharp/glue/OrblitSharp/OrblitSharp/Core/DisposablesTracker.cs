using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

#nullable enable

namespace Orblit
{
    internal static class DisposablesTracker
    {
        [UnmanagedCallersOnly]
        internal static void OnOrblitShuttingDown()
        {
            try
            {
                OnOrblitShuttingDownImpl();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }

        private static void OnOrblitShuttingDownImpl()
        {
            bool isStdoutVerbose;

            try
            {
                isStdoutVerbose = OS.IsStdOutVerbose();
            }
            catch (ObjectDisposedException)
            {
                // OS singleton already disposed. Maybe OnUnloading was called twice.
                isStdoutVerbose = false;
            }

            if (isStdoutVerbose)
                Orb.Print("Unloading: Disposing tracked instances...");

            // Dispose Orblit Objects first, and only then dispose other disposables
            // like StringName, EntityPath, Orblit.Collections.Array/Dictionary, etc.
            // The Orblit Object Dispose() method may need any of the later instances.

            foreach (WeakReference<OrblitObject> item in OrblitObjectInstances.Keys)
            {
                if (item.TryGetTarget(out OrblitObject? self))
                    self.Dispose();
            }

            foreach (WeakReference<IDisposable> item in OtherInstances.Keys)
            {
                if (item.TryGetTarget(out IDisposable? self))
                    self.Dispose();
            }

            if (isStdoutVerbose)
                Orb.Print("Unloading: Finished disposing tracked instances.");
        }

        private static ConcurrentDictionary<WeakReference<OrblitObject>, byte> OrblitObjectInstances { get; } =
            new();

        private static ConcurrentDictionary<WeakReference<IDisposable>, byte> OtherInstances { get; } =
            new();

        public static WeakReference<OrblitObject> RegisterOrblitObject(OrblitObject orblitObject)
        {
            var weakReferenceToSelf = new WeakReference<OrblitObject>(orblitObject);
            OrblitObjectInstances.TryAdd(weakReferenceToSelf, 0);
            return weakReferenceToSelf;
        }

        public static WeakReference<IDisposable> RegisterDisposable(IDisposable disposable)
        {
            var weakReferenceToSelf = new WeakReference<IDisposable>(disposable);
            OtherInstances.TryAdd(weakReferenceToSelf, 0);
            return weakReferenceToSelf;
        }

        public static void UnregisterOrblitObject(OrblitObject orblitObject, WeakReference<OrblitObject> weakReferenceToSelf)
        {
            if (!OrblitObjectInstances.TryRemove(weakReferenceToSelf, out _))
                throw new ArgumentException("Orblit Object not registered.", nameof(weakReferenceToSelf));
        }

        public static void UnregisterDisposable(WeakReference<IDisposable> weakReference)
        {
            if (!OtherInstances.TryRemove(weakReference, out _))
                throw new ArgumentException("Disposable not registered.", nameof(weakReference));
        }
    }
}
