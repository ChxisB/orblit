/**************************************************************************/
/*  managed_callable.cpp                                                  */

#include "managed_callable.h"

#include "csharp_script.h"
#include "host/dotnet_host_cache.h"

#ifdef ORBLIT_CSHARP_HOT_RELOAD
SelfList<ManagedCallable>::List ManagedCallable::instances;
RBMap<ManagedCallable *, Array> ManagedCallable::instances_pending_reload;
Mutex ManagedCallable::instances_mutex;
#endif

bool ManagedCallable::compare_equal(const CallableCustom *p_a, const CallableCustom *p_b) {
	const ManagedCallable *a = static_cast<const ManagedCallable *>(p_a);
	const ManagedCallable *b = static_cast<const ManagedCallable *>(p_b);

	if (a->delegate_handle.value == b->delegate_handle.value) {
		return true;
	}
	if (!a->delegate_handle.value || !b->delegate_handle.value) {
		return false;
	}

	// Call Delegate's 'Equals'
	return DotNetHostCache::managed_callbacks.DelegateUtils_DelegateEquals(
			a->delegate_handle, b->delegate_handle);
}

bool ManagedCallable::compare_less(const CallableCustom *p_a, const CallableCustom *p_b) {
	if (compare_equal(p_a, p_b)) {
		return false;
	}
	return p_a < p_b;
}

uint32_t ManagedCallable::hash() const {
	return DotNetHostCache::managed_callbacks.DelegateUtils_DelegateHash(delegate_handle);
}

String ManagedCallable::get_as_text() const {
	return "Delegate::Invoke";
}

CallableCustom::CompareEqualFunc ManagedCallable::get_compare_equal_func() const {
	return compare_equal_func_ptr;
}

CallableCustom::CompareLessFunc ManagedCallable::get_compare_less_func() const {
	return compare_less_func_ptr;
}

ObjectID ManagedCallable::get_object() const {
	if (object_id != ObjectID()) {
		return object_id;
	}
	return CSharpLanguage::get_singleton()->get_managed_callable_middleman()->get_instance_id();
}

int ManagedCallable::get_argument_count(bool &r_is_valid) const {
	return DotNetHostCache::managed_callbacks.DelegateUtils_GetArgumentCount(delegate_handle, &r_is_valid);
}

void ManagedCallable::call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const {
	r_call_error.error = Callable::CallError::CALL_ERROR_INVALID_METHOD; // Can't find anything better
	r_return_value = Variant();

	ERR_FAIL_NULL(delegate_handle.value);

	DotNetHostCache::managed_callbacks.DelegateUtils_InvokeWithVariantArgs(
			delegate_handle, trampoline, p_arguments, p_argcount, &r_return_value);

	r_call_error.error = Callable::CallError::CALL_OK;
}

void ManagedCallable::release_delegate_handle() {
	if (delegate_handle.value) {
		DotNetHostCache::managed_callbacks.GCHandleBridge_FreeGCHandle(delegate_handle);
		delegate_handle = { nullptr };
	}
}

// Why you do this clang-format...
/* clang-format off */
ManagedCallable::ManagedCallable(GCHandleIntPtr p_delegate_handle, void *p_trampoline, ObjectID p_object_id) :
		delegate_handle(p_delegate_handle), trampoline(p_trampoline), object_id(p_object_id) {
#ifdef ORBLIT_CSHARP_HOT_RELOAD
	{
		MutexLock lock(instances_mutex);
		instances.add(&self_instance);
	}
#endif
}
/* clang-format on */

ManagedCallable::~ManagedCallable() {
#ifdef ORBLIT_CSHARP_HOT_RELOAD
	{
		MutexLock lock(instances_mutex);
		instances.remove(&self_instance);
		instances_pending_reload.erase(this);
	}
#endif

	release_delegate_handle();
}
