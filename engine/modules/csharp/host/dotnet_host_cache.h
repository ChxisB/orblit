/**************************************************************************/
/*  dotnet_host_cache.h                                                   */

#pragma once

#include "../csharp_script.h"
#include "../interop_types.h"
#include "../dotnet_gc_handle.h"

#include "core/object/object.h"
#include "core/string/string_name.h"
#include "core/string/ustring.h"
#include "core/variant/callable.h"
#include "core/variant/dictionary.h"
#include "core/variant/variant.h"

class CSharpScript;

namespace DotNetHostCache {

#ifndef ORBLIT_CLR_STDCALL
#ifdef WIN32
#define ORBLIT_CLR_STDCALL __stdcall
#else
#define ORBLIT_CLR_STDCALL
#endif
#endif

struct orblitsharp_property_info {
	orblit_string_name name; // Not owned
	orblit_string hint_string;
	Variant::Type type;
	PropertyHint hint;
	PropertyUsageFlags usage;
	orblit_string_name *class_name; // Not owned
	bool exported;
};

struct orblitsharp_property_def_val_pair {
	orblit_string_name name; // Not owned
	orblit_variant value;
};

struct ManagedCallbacks {
	using Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddMethod = void(ORBLIT_CLR_STDCALL *)(CSharpScript *p_script, const StringName *p_name, int32_t p_argc, orblitsharp::MethodTrampoline p_trampoline, bool p_is_static);
	using Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddProperty = void(ORBLIT_CLR_STDCALL *)(CSharpScript *p_script, const StringName *p_name,
			orblitsharp::PropertyGetterTrampoline p_getter_trampoline, orblitsharp::PropertySetterTrampoline p_setter_trampoline);
	using Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddRaiseSignal = void(ORBLIT_CLR_STDCALL *)(CSharpScript *p_script, const StringName *p_name, int32_t p_argc, orblitsharp::RaiseSignalTrampoline p_trampoline);
	using Callback_ScriptManagerBridge_GetPropertyInfoList_Add = void(ORBLIT_CLR_STDCALL *)(CSharpScript *p_script, const String *, void *p_props, int32_t p_count);
	using Callback_ScriptManagerBridge_GetPropertyDefaultValues_Add = void(ORBLIT_CLR_STDCALL *)(CSharpScript *p_script, void *p_def_vals, int32_t p_count);

	using FuncSignalAwaiter_SignalCallback = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const Variant **, int32_t, bool *);
	using FuncDelegateUtils_InvokeWithVariantArgs = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, void *, const Variant **, int32_t, const Variant *);
	using FuncDelegateUtils_DelegateEquals = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, GCHandleIntPtr);
	using FuncDelegateUtils_DelegateHash = int32_t(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr);
	using FuncDelegateUtils_GetArgumentCount = int32_t(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, bool *);
	using FuncDelegateUtils_TrySerializeDelegateWithGCHandle = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const Array *);
	using FuncDelegateUtils_TryDeserializeDelegateWithGCHandle = bool(ORBLIT_CLR_STDCALL *)(const Array *, GCHandleIntPtr *);
	using FuncScriptManagerBridge_FrameCallback = void(ORBLIT_CLR_STDCALL *)();
	using FuncScriptManagerBridge_CreateManagedForOrblitObjectBinding = GCHandleIntPtr(ORBLIT_CLR_STDCALL *)(const StringName *, Object *);
	using FuncScriptManagerBridge_CreateManagedForOrblitObjectScriptInstance = bool(ORBLIT_CLR_STDCALL *)(const CSharpScript *, Object *, const Variant **, int32_t);
	using FuncScriptManagerBridge_GetScriptNativeName = void(ORBLIT_CLR_STDCALL *)(const CSharpScript *, StringName *);
	using FuncScriptManagerBridge_GetGlobalClassName = void(ORBLIT_CLR_STDCALL *)(const String *, String *, String *, bool *, bool *, String *);
	using FuncScriptManagerBridge_SetOrblitObjectPtr = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, Object *);
	using FuncScriptManagerBridge_RaiseEventSignal = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const StringName *, const Variant **, int32_t, bool *);
	using FuncScriptManagerBridge_RaiseEventSignalViaTrampoline = void(ORBLIT_CLR_STDCALL *)(orblitsharp::RaiseSignalTrampoline p_trampoline, GCHandleIntPtr p_obj_gchandle, const Variant **p_args, int32_t p_argc, Callable::CallError *r_ref_call_error, bool *r_owner_is_null);
	using FuncScriptManagerBridge_ScriptIsOrInherits = bool(ORBLIT_CLR_STDCALL *)(const CSharpScript *, const CSharpScript *);
	using FuncScriptManagerBridge_AddScriptBridge = bool(ORBLIT_CLR_STDCALL *)(const CSharpScript *, const String *);
	using FuncScriptManagerBridge_GetOrCreateScriptBridgeForPath = void(ORBLIT_CLR_STDCALL *)(const String *, Ref<CSharpScript> *);
	using FuncScriptManagerBridge_RemoveScriptBridge = void(ORBLIT_CLR_STDCALL *)(const CSharpScript *);
	using FuncScriptManagerBridge_TryReloadRegisteredScriptWithClass = bool(ORBLIT_CLR_STDCALL *)(const CSharpScript *);
	using FuncScriptManagerBridge_UpdateScriptTrampolines = void(ORBLIT_CLR_STDCALL *)(const CSharpScript *, bool *,
			Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddMethod,
			Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddProperty,
			Callback_ScriptManagerBridge_UpdateScriptTrampolines_TryAddRaiseSignal);
	using FuncScriptManagerBridge_UpdateScriptClassInfo = void(ORBLIT_CLR_STDCALL *)(const CSharpScript *, CSharpScript::TypeInfo *, Array *, Dictionary *, Dictionary *, Ref<CSharpScript> *);
	using FuncScriptManagerBridge_SwapGCHandleForType = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, GCHandleIntPtr *, bool);
	using FuncScriptManagerBridge_GetPropertyInfoList = void(ORBLIT_CLR_STDCALL *)(CSharpScript *, Callback_ScriptManagerBridge_GetPropertyInfoList_Add);
	using FuncScriptManagerBridge_GetPropertyDefaultValues = void(ORBLIT_CLR_STDCALL *)(CSharpScript *, Callback_ScriptManagerBridge_GetPropertyDefaultValues_Add);
	using FuncScriptManagerBridge_CallStatic = bool(ORBLIT_CLR_STDCALL *)(const CSharpScript *, const StringName *, const Variant **, int32_t, Callable::CallError *, Variant *);
	using FuncScriptManagerBridge_CallStaticWithTrampoline = bool(ORBLIT_CLR_STDCALL *)(orblitsharp::MethodTrampoline p_trampoline, const Variant **, int32_t, Callable::CallError *, Variant *);
	using FuncCSharpInstanceBridge_Call = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const StringName *, const Variant **, int32_t, Callable::CallError *, Variant *);
	using FuncCSharpInstanceBridge_Set = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const StringName *, const Variant *);
	using FuncCSharpInstanceBridge_Get = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const StringName *, Variant *);
	using FuncCSharpInstanceBridge_CallViaTrampoline = bool(ORBLIT_CLR_STDCALL *)(orblitsharp::MethodTrampoline p_trampoline, GCHandleIntPtr p_obj_gchandle, const Variant **p_args, int32_t p_argc, Callable::CallError *r_ref_call_error, Variant *r_ret);
	using FuncCSharpInstanceBridge_SetViaTrampoline = bool(ORBLIT_CLR_STDCALL *)(orblitsharp::PropertySetterTrampoline p_trampoline, GCHandleIntPtr p_obj_gchandle, const Variant *p_args);
	using FuncCSharpInstanceBridge_GetViaTrampoline = bool(ORBLIT_CLR_STDCALL *)(orblitsharp::PropertyGetterTrampoline p_trampoline, GCHandleIntPtr p_obj_gchandle, Variant *r_args);
	using FuncCSharpInstanceBridge_CallDispose = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, bool);
	using FuncCSharpInstanceBridge_CallToString = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, String *, bool *);
	using FuncCSharpInstanceBridge_HasMethodUnknownParams = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const StringName *);
	using FuncCSharpInstanceBridge_SerializeState = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const Dictionary *, const Dictionary *);
	using FuncCSharpInstanceBridge_DeserializeState = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr, const Dictionary *, const Dictionary *);
	using FuncGCHandleBridge_FreeGCHandle = void(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr);
	using FuncGCHandleBridge_GCHandleIsTargetCollectible = bool(ORBLIT_CLR_STDCALL *)(GCHandleIntPtr);
	using FuncDebuggingUtils_GetCurrentStackInfo = void(ORBLIT_CLR_STDCALL *)(Vector<ScriptLanguage::StackInfo> *);
	using FuncDisposablesTracker_OnOrblitShuttingDown = void(ORBLIT_CLR_STDCALL *)();
	using FuncOrb_OnCoreApiAssemblyLoaded = void(ORBLIT_CLR_STDCALL *)(bool);

	FuncSignalAwaiter_SignalCallback SignalAwaiter_SignalCallback;
	FuncDelegateUtils_InvokeWithVariantArgs DelegateUtils_InvokeWithVariantArgs;
	FuncDelegateUtils_DelegateEquals DelegateUtils_DelegateEquals;
	FuncDelegateUtils_DelegateHash DelegateUtils_DelegateHash;
	FuncDelegateUtils_GetArgumentCount DelegateUtils_GetArgumentCount;
	FuncDelegateUtils_TrySerializeDelegateWithGCHandle DelegateUtils_TrySerializeDelegateWithGCHandle;
	FuncDelegateUtils_TryDeserializeDelegateWithGCHandle DelegateUtils_TryDeserializeDelegateWithGCHandle;
	FuncScriptManagerBridge_FrameCallback ScriptManagerBridge_FrameCallback;
	FuncScriptManagerBridge_CreateManagedForOrblitObjectBinding ScriptManagerBridge_CreateManagedForOrblitObjectBinding;
	FuncScriptManagerBridge_CreateManagedForOrblitObjectScriptInstance ScriptManagerBridge_CreateManagedForOrblitObjectScriptInstance;
	FuncScriptManagerBridge_GetScriptNativeName ScriptManagerBridge_GetScriptNativeName;
	FuncScriptManagerBridge_GetGlobalClassName ScriptManagerBridge_GetGlobalClassName;
	FuncScriptManagerBridge_SetOrblitObjectPtr ScriptManagerBridge_SetOrblitObjectPtr;
	FuncScriptManagerBridge_RaiseEventSignal ScriptManagerBridge_RaiseEventSignal;
	FuncScriptManagerBridge_RaiseEventSignalViaTrampoline ScriptManagerBridge_RaiseEventSignalViaTrampoline;
	FuncScriptManagerBridge_ScriptIsOrInherits ScriptManagerBridge_ScriptIsOrInherits;
	FuncScriptManagerBridge_AddScriptBridge ScriptManagerBridge_AddScriptBridge;
	FuncScriptManagerBridge_GetOrCreateScriptBridgeForPath ScriptManagerBridge_GetOrCreateScriptBridgeForPath;
	FuncScriptManagerBridge_RemoveScriptBridge ScriptManagerBridge_RemoveScriptBridge;
	FuncScriptManagerBridge_TryReloadRegisteredScriptWithClass ScriptManagerBridge_TryReloadRegisteredScriptWithClass;
	FuncScriptManagerBridge_UpdateScriptTrampolines ScriptManagerBridge_UpdateScriptTrampolines;
	FuncScriptManagerBridge_UpdateScriptClassInfo ScriptManagerBridge_UpdateScriptClassInfo;
	FuncScriptManagerBridge_SwapGCHandleForType ScriptManagerBridge_SwapGCHandleForType;
	FuncScriptManagerBridge_GetPropertyInfoList ScriptManagerBridge_GetPropertyInfoList;
	FuncScriptManagerBridge_GetPropertyDefaultValues ScriptManagerBridge_GetPropertyDefaultValues;
	FuncScriptManagerBridge_CallStatic ScriptManagerBridge_CallStatic;
	FuncScriptManagerBridge_CallStaticWithTrampoline ScriptManagerBridge_CallStaticWithTrampoline;
	FuncCSharpInstanceBridge_Call CSharpInstanceBridge_Call;
	FuncCSharpInstanceBridge_Set CSharpInstanceBridge_Set;
	FuncCSharpInstanceBridge_Get CSharpInstanceBridge_Get;
	FuncCSharpInstanceBridge_CallViaTrampoline CSharpInstanceBridge_CallViaTrampoline;
	FuncCSharpInstanceBridge_SetViaTrampoline CSharpInstanceBridge_SetViaTrampoline;
	FuncCSharpInstanceBridge_GetViaTrampoline CSharpInstanceBridge_GetViaTrampoline;
	FuncCSharpInstanceBridge_CallDispose CSharpInstanceBridge_CallDispose;
	FuncCSharpInstanceBridge_CallToString CSharpInstanceBridge_CallToString;
	FuncCSharpInstanceBridge_HasMethodUnknownParams CSharpInstanceBridge_HasMethodUnknownParams;
	FuncCSharpInstanceBridge_SerializeState CSharpInstanceBridge_SerializeState;
	FuncCSharpInstanceBridge_DeserializeState CSharpInstanceBridge_DeserializeState;
	FuncGCHandleBridge_FreeGCHandle GCHandleBridge_FreeGCHandle;
	FuncGCHandleBridge_GCHandleIsTargetCollectible GCHandleBridge_GCHandleIsTargetCollectible;
	FuncDebuggingUtils_GetCurrentStackInfo DebuggingUtils_GetCurrentStackInfo;
	FuncDisposablesTracker_OnOrblitShuttingDown DisposablesTracker_OnOrblitShuttingDown;
	FuncOrb_OnCoreApiAssemblyLoaded Orb_OnCoreApiAssemblyLoaded;
};

extern ManagedCallbacks managed_callbacks;
extern bool orblit_api_cache_updated;

void update_api_cache(const ManagedCallbacks &p_managed_callbacks);

} // namespace DotNetHostCache
