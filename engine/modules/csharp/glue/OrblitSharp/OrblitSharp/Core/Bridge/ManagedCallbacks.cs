using System;
using System.Runtime.InteropServices;
using Orblit.NativeInterop;

namespace Orblit.Bridge
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ManagedCallbacks
    {
        // @formatter:off
        public delegate* unmanaged<IntPtr, orblit_variant**, int, orblit_bool*, void> SignalAwaiter_SignalCallback;
        public delegate* unmanaged<IntPtr, void*, orblit_variant**, int, orblit_variant*, void> DelegateUtils_InvokeWithVariantArgs;
        public delegate* unmanaged<IntPtr, IntPtr, orblit_bool> DelegateUtils_DelegateEquals;
        public delegate* unmanaged<IntPtr, int> DelegateUtils_DelegateHash;
        public delegate* unmanaged<IntPtr, orblit_bool*, int> DelegateUtils_GetArgumentCount;
        public delegate* unmanaged<IntPtr, orblit_array*, orblit_bool> DelegateUtils_TrySerializeDelegateWithGCHandle;
        public delegate* unmanaged<orblit_array*, IntPtr*, orblit_bool> DelegateUtils_TryDeserializeDelegateWithGCHandle;
        public delegate* unmanaged<void> ScriptManagerBridge_FrameCallback;
        public delegate* unmanaged<orblit_string_name*, IntPtr, IntPtr> ScriptManagerBridge_CreateManagedForOrblitObjectBinding;
        public delegate* unmanaged<IntPtr, IntPtr, orblit_variant**, int, orblit_bool> ScriptManagerBridge_CreateManagedForOrblitObjectScriptInstance;
        public delegate* unmanaged<IntPtr, orblit_string_name*, void> ScriptManagerBridge_GetScriptNativeName;
        public delegate* unmanaged<orblit_string*, orblit_string*, orblit_string*, orblit_bool*, orblit_bool*, orblit_string*, void> ScriptManagerBridge_GetGlobalClassName;
        public delegate* unmanaged<IntPtr, IntPtr, void> ScriptManagerBridge_SetOrblitObjectPtr;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_variant**, int, orblit_bool*, void> ScriptManagerBridge_RaiseEventSignal;
        public delegate* unmanaged<RaiseSignalTrampolineDelegate, IntPtr, orblit_variant**, int, orblit_variant_call_error*, orblit_bool*, void> ScriptManagerBridge_RaiseEventSignalViaTrampoline;
        public delegate* unmanaged<IntPtr, IntPtr, orblit_bool> ScriptManagerBridge_ScriptIsOrInherits;
        public delegate* unmanaged<IntPtr, orblit_string*, orblit_bool> ScriptManagerBridge_AddScriptBridge;
        public delegate* unmanaged<orblit_string*, orblit_ref*, void> ScriptManagerBridge_GetOrCreateScriptBridgeForPath;
        public delegate* unmanaged<IntPtr, void> ScriptManagerBridge_RemoveScriptBridge;
        public delegate* unmanaged<IntPtr, orblit_bool> ScriptManagerBridge_TryReloadRegisteredScriptWithClass;
        public delegate* unmanaged<IntPtr, orblit_bool*,
            delegate* unmanaged<IntPtr, orblit_string_name*, int, void*, orblit_bool, void>,
            delegate* unmanaged<IntPtr, orblit_string_name*, void*, void*, void>,
            delegate* unmanaged<IntPtr, orblit_string_name*, int, void*, void>,
            void> ScriptManagerBridge_UpdateScriptTrampolines;
        public delegate* unmanaged<IntPtr, orblit_csharp_type_info*, orblit_array*, orblit_dictionary*, orblit_dictionary*, orblit_ref*, void> ScriptManagerBridge_UpdateScriptClassInfo;
        public delegate* unmanaged<IntPtr, IntPtr*, orblit_bool, orblit_bool> ScriptManagerBridge_SwapGCHandleForType;
        public delegate* unmanaged<IntPtr, delegate* unmanaged<IntPtr, orblit_string*, void*, int, void>, void> ScriptManagerBridge_GetPropertyInfoList;
        public delegate* unmanaged<IntPtr, delegate* unmanaged<IntPtr, void*, int, void>, void> ScriptManagerBridge_GetPropertyDefaultValues;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_variant**, int, orblit_variant_call_error*, orblit_variant*, orblit_bool> ScriptManagerBridge_CallStatic;
        public delegate* unmanaged<MethodTrampolineDelegate, orblit_variant**, int, orblit_variant_call_error*, orblit_variant*, orblit_bool> ScriptManagerBridge_CallStaticWithTrampoline;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_variant**, int, orblit_variant_call_error*, orblit_variant*, orblit_bool> CSharpInstanceBridge_Call;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_variant*, orblit_bool> CSharpInstanceBridge_Set;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_variant*, orblit_bool> CSharpInstanceBridge_Get;
        public delegate* unmanaged<MethodTrampolineDelegate, IntPtr, orblit_variant**, int, orblit_variant_call_error*, orblit_variant*, orblit_bool> CSharpInstanceBridge_CallViaTrampoline;
        public delegate* unmanaged<PropertySetterTrampolineDelegate, IntPtr, orblit_variant*, orblit_bool> CSharpInstanceBridge_SetViaTrampoline;
        public delegate* unmanaged<PropertyGetterTrampolineDelegate, IntPtr, orblit_variant*, orblit_bool> CSharpInstanceBridge_GetViaTrampoline;
        public delegate* unmanaged<IntPtr, orblit_bool, void> CSharpInstanceBridge_CallDispose;
        public delegate* unmanaged<IntPtr, orblit_string*, orblit_bool*, void> CSharpInstanceBridge_CallToString;
        public delegate* unmanaged<IntPtr, orblit_string_name*, orblit_bool> CSharpInstanceBridge_HasMethodUnknownParams;
        public delegate* unmanaged<IntPtr, orblit_dictionary*, orblit_dictionary*, void> CSharpInstanceBridge_SerializeState;
        public delegate* unmanaged<IntPtr, orblit_dictionary*, orblit_dictionary*, void> CSharpInstanceBridge_DeserializeState;
        public delegate* unmanaged<IntPtr, void> GCHandleBridge_FreeGCHandle;
        public delegate* unmanaged<IntPtr, orblit_bool> GCHandleBridge_GCHandleIsTargetCollectible;
        public delegate* unmanaged<void*, void> DebuggingUtils_GetCurrentStackInfo;
        public delegate* unmanaged<void> DisposablesTracker_OnOrblitShuttingDown;
        public delegate* unmanaged<orblit_bool, void> Orb_OnCoreApiAssemblyLoaded;
        // @formatter:on

        public static ManagedCallbacks Create()
        {
            return new()
            {
                // @formatter:off
                SignalAwaiter_SignalCallback = &SignalAwaiter.SignalCallback,
                DelegateUtils_InvokeWithVariantArgs = &DelegateUtils.InvokeWithVariantArgs,
                DelegateUtils_DelegateEquals = &DelegateUtils.DelegateEquals,
                DelegateUtils_DelegateHash = &DelegateUtils.DelegateHash,
                DelegateUtils_GetArgumentCount = &DelegateUtils.GetArgumentCount,
                DelegateUtils_TrySerializeDelegateWithGCHandle = &DelegateUtils.TrySerializeDelegateWithGCHandle,
                DelegateUtils_TryDeserializeDelegateWithGCHandle = &DelegateUtils.TryDeserializeDelegateWithGCHandle,
                ScriptManagerBridge_FrameCallback = &ScriptManagerBridge.FrameCallback,
                ScriptManagerBridge_CreateManagedForOrblitObjectBinding = &ScriptManagerBridge.CreateManagedForOrblitObjectBinding,
                ScriptManagerBridge_CreateManagedForOrblitObjectScriptInstance = &ScriptManagerBridge.CreateManagedForOrblitObjectScriptInstance,
                ScriptManagerBridge_GetScriptNativeName = &ScriptManagerBridge.GetScriptNativeName,
                ScriptManagerBridge_GetGlobalClassName = &ScriptManagerBridge.GetGlobalClassName,
                ScriptManagerBridge_SetOrblitObjectPtr = &ScriptManagerBridge.SetOrblitObjectPtr,
                ScriptManagerBridge_RaiseEventSignal = &ScriptManagerBridge.RaiseEventSignal,
                ScriptManagerBridge_RaiseEventSignalViaTrampoline = &ScriptManagerBridge.RaiseEventSignalViaTrampoline,
                ScriptManagerBridge_ScriptIsOrInherits = &ScriptManagerBridge.ScriptIsOrInherits,
                ScriptManagerBridge_AddScriptBridge = &ScriptManagerBridge.AddScriptBridge,
                ScriptManagerBridge_GetOrCreateScriptBridgeForPath = &ScriptManagerBridge.GetOrCreateScriptBridgeForPath,
                ScriptManagerBridge_RemoveScriptBridge = &ScriptManagerBridge.RemoveScriptBridge,
                ScriptManagerBridge_TryReloadRegisteredScriptWithClass = &ScriptManagerBridge.TryReloadRegisteredScriptWithClass,
                ScriptManagerBridge_UpdateScriptTrampolines = &ScriptManagerBridge.UpdateScriptTrampolines,
                ScriptManagerBridge_UpdateScriptClassInfo = &ScriptManagerBridge.UpdateScriptClassInfo,
                ScriptManagerBridge_SwapGCHandleForType = &ScriptManagerBridge.SwapGCHandleForType,
                ScriptManagerBridge_GetPropertyInfoList = &ScriptManagerBridge.GetPropertyInfoList,
                ScriptManagerBridge_GetPropertyDefaultValues = &ScriptManagerBridge.GetPropertyDefaultValues,
                ScriptManagerBridge_CallStatic = &ScriptManagerBridge.CallStatic,
                ScriptManagerBridge_CallStaticWithTrampoline = &ScriptManagerBridge.CallStaticWithTrampoline,
                CSharpInstanceBridge_Call = &CSharpInstanceBridge.Call,
                CSharpInstanceBridge_Set = &CSharpInstanceBridge.Set,
                CSharpInstanceBridge_Get = &CSharpInstanceBridge.Get,
                CSharpInstanceBridge_CallViaTrampoline = &CSharpInstanceBridge.CallViaTrampoline,
                CSharpInstanceBridge_SetViaTrampoline = &CSharpInstanceBridge.SetViaTrampoline,
                CSharpInstanceBridge_GetViaTrampoline = &CSharpInstanceBridge.GetViaTrampoline,
                CSharpInstanceBridge_CallDispose = &CSharpInstanceBridge.CallDispose,
                CSharpInstanceBridge_CallToString = &CSharpInstanceBridge.CallToString,
                CSharpInstanceBridge_HasMethodUnknownParams = &CSharpInstanceBridge.HasMethodUnknownParams,
                CSharpInstanceBridge_SerializeState = &CSharpInstanceBridge.SerializeState,
                CSharpInstanceBridge_DeserializeState = &CSharpInstanceBridge.DeserializeState,
                GCHandleBridge_FreeGCHandle = &GCHandleBridge.FreeGCHandle,
                GCHandleBridge_GCHandleIsTargetCollectible = &GCHandleBridge.GCHandleIsTargetCollectible,
                DebuggingUtils_GetCurrentStackInfo = &DebuggingUtils.GetCurrentStackInfo,
                DisposablesTracker_OnOrblitShuttingDown = &DisposablesTracker.OnOrblitShuttingDown,
                Orb_OnCoreApiAssemblyLoaded = &Orb.OnCoreApiAssemblyLoaded,
                // @formatter:on
            };
        }

        public static void Create(IntPtr outManagedCallbacks)
            => *(ManagedCallbacks*)outManagedCallbacks = Create();
    }
}
