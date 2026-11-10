using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit.Bridge
{
    internal static class CSharpInstanceBridge
    {
        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool Call(IntPtr orblitObjectGCHandle, orblit_string_name* method,
            orblit_variant** args, int argCount, orblit_variant_call_error* refCallError, orblit_variant* outRet)
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                {
                    *outRet = default;
                    (*refCallError).Error = orblit_variant_call_error_error.ORBLIT_CALL_ERROR_CALL_ERROR_INSTANCE_IS_NULL;
                    return orblit_bool.False;
                }

                bool methodInvoked = orblitObject.InvokeOrblitClassMethod(CustomUnsafe.AsRef(method),
                    new NativeVariantPtrArgs(args, argCount), out orblit_variant retValue);

                if (!methodInvoked)
                {
                    *outRet = default;
                    // This is important, as it tells Object::call that no method was called.
                    // Otherwise, it would prevent Object::call from calling native methods.
                    (*refCallError).Error = orblit_variant_call_error_error.ORBLIT_CALL_ERROR_CALL_ERROR_INVALID_METHOD;
                    return orblit_bool.False;
                }

                *outRet = retValue;
                return orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outRet = default;
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool Set(IntPtr orblitObjectGCHandle, orblit_string_name* name, orblit_variant* value)
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    throw new InvalidOperationException("Orblit Object instance is null.");

                if (orblitObject.SetOrblitClassPropertyValue(CustomUnsafe.AsRef(name), CustomUnsafe.AsRef(value)))
                {
                    return orblit_bool.True;
                }

                if (!orblitObject.HasOrblitClassMethod(OrblitObject.MethodName._Set.NativeValue.DangerousSelfRef))
                {
                    return orblit_bool.False;
                }

                var nameManaged = StringName.CreateTakingOwnershipOfDisposableValue(
                    NativeFuncs.orblitsharp_string_name_new_copy(CustomUnsafe.AsRef(name)));

                Variant valueManaged = Variant.CreateCopyingBorrowed(*value);

                return orblitObject._Set(nameManaged, valueManaged).ToOrblitBool();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool Get(IntPtr orblitObjectGCHandle, orblit_string_name* name,
            orblit_variant* outRet)
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    throw new InvalidOperationException("Orblit Object instance is null.");

                // Properties
                if (orblitObject.GetOrblitClassPropertyValue(CustomUnsafe.AsRef(name), out orblit_variant outRetValue))
                {
                    *outRet = outRetValue;
                    return orblit_bool.True;
                }

                // Signals
                if (orblitObject.HasOrblitClassSignal(CustomUnsafe.AsRef(name)))
                {
                    orblit_signal signal = new orblit_signal(NativeFuncs.orblitsharp_string_name_new_copy(*name),
                        orblitObject.GetInstanceId());
                    *outRet = VariantUtils.CreateFromSignalTakingOwnershipOfDisposableValue(signal);
                    return orblit_bool.True;
                }

                // Methods
                if (orblitObject.HasOrblitClassMethod(CustomUnsafe.AsRef(name)))
                {
                    orblit_callable method = new orblit_callable(NativeFuncs.orblitsharp_string_name_new_copy(*name),
                        orblitObject.GetInstanceId());
                    *outRet = VariantUtils.CreateFromCallableTakingOwnershipOfDisposableValue(method);
                    return orblit_bool.True;
                }

                if (!orblitObject.HasOrblitClassMethod(OrblitObject.MethodName._Get.NativeValue.DangerousSelfRef))
                {
                    return orblit_bool.False;
                }

                var nameManaged = StringName.CreateTakingOwnershipOfDisposableValue(
                    NativeFuncs.orblitsharp_string_name_new_copy(CustomUnsafe.AsRef(name)));

                Variant ret = orblitObject._Get(nameManaged);

                if (ret.VariantType == Variant.Type.Nil)
                {
                    *outRet = default;
                    return orblit_bool.False;
                }

                *outRet = ret.CopyNativeVariant();
                return orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outRet = default;
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool CallViaTrampoline(
            MethodTrampolineDelegate methodTrampoline,
            IntPtr orblitObjectGCHandle, orblit_variant** args, int argCount,
            orblit_variant_call_error* refCallError, orblit_variant* outRet)
        {
            try
            {
                object orblitObject = GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                {
                    *outRet = default;
                    (*refCallError).Error = orblit_variant_call_error_error.ORBLIT_CALL_ERROR_CALL_ERROR_INSTANCE_IS_NULL;
                    return orblit_bool.False;
                }

                *outRet = methodTrampoline(orblitObject, new NativeVariantPtrArgs(args, argCount), ref *refCallError);
                return orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outRet = default;
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool SetViaTrampoline(
            PropertySetterTrampolineDelegate setterTrampoline,
            IntPtr orblitObjectGCHandle, orblit_variant* value)
        {
            try
            {
                object orblitObject = GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    throw new InvalidOperationException("Orblit Object instance is null.");

                setterTrampoline(orblitObject, *value);
                return orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool GetViaTrampoline(
            PropertyGetterTrampolineDelegate getterTrampoline,
            IntPtr orblitObjectGCHandle, orblit_variant* outRet)
        {
            try
            {
                object orblitObject = GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    throw new InvalidOperationException("Orblit Object instance is null.");

                *outRet = getterTrampoline(orblitObject);
                return orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outRet = default;
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static void CallDispose(IntPtr orblitObjectGCHandle, orblit_bool okIfNull)
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (okIfNull.ToBool())
                    orblitObject?.Dispose();
                else
                    orblitObject!.Dispose();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe void CallToString(IntPtr orblitObjectGCHandle, orblit_string* outRes, orblit_bool* outValid)
        {
            try
            {
                var self = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (self == null)
                {
                    *outRes = default;
                    *outValid = orblit_bool.False;
                    return;
                }

                var resultStr = self.ToString();

                if (resultStr == null)
                {
                    *outRes = default;
                    *outValid = orblit_bool.False;
                    return;
                }

                *outRes = Marshaling.ConvertStringToNative(resultStr);
                *outValid = orblit_bool.True;
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                *outRes = default;
                *outValid = orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe orblit_bool HasMethodUnknownParams(IntPtr orblitObjectGCHandle, orblit_string_name* method)
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    return orblit_bool.False;

                return orblitObject.HasOrblitClassMethod(CustomUnsafe.AsRef(method)).ToOrblitBool();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
                return orblit_bool.False;
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe void SerializeState(
            IntPtr orblitObjectGCHandle,
            orblit_dictionary* propertiesState,
            orblit_dictionary* signalEventsState
        )
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    return;

                // Call OnBeforeSerialize

                // ReSharper disable once SuspiciousTypeConversion.Global
                if (orblitObject is ISerializationListener serializationListener)
                    serializationListener.OnBeforeSerialize();

                // Save instance state

                using var info = OrblitSerializationInfo.CreateCopyingBorrowed(
                    *propertiesState, *signalEventsState);

                orblitObject.SaveOrblitObjectData(info);
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe void DeserializeState(
            IntPtr orblitObjectGCHandle,
            orblit_dictionary* propertiesState,
            orblit_dictionary* signalEventsState
        )
        {
            try
            {
                var orblitObject = (OrblitObject)GCHandle.FromIntPtr(orblitObjectGCHandle).Target;

                if (orblitObject == null)
                    return;

                // Restore instance state

                using var info = OrblitSerializationInfo.CreateCopyingBorrowed(
                    *propertiesState, *signalEventsState);

                orblitObject.RestoreOrblitObjectData(info);

                // Call OnAfterDeserialize

                // ReSharper disable once SuspiciousTypeConversion.Global
                if (orblitObject is ISerializationListener serializationListener)
                    serializationListener.OnAfterDeserialize();
            }
            catch (Exception e)
            {
                ExceptionUtils.LogException(e);
            }
        }
    }
}
