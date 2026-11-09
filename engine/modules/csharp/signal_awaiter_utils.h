/**************************************************************************/
/*  signal_awaiter_utils.h                                                */

#pragma once

#include "dotnet_gc_handle.h"

#include "core/object/object_id.h"
#include "core/string/string_name.h"
#include "core/string/ustring.h"
#include "core/variant/callable.h"

Error dotnet_host_connect_signal_awaiter(Object *p_source, const StringName &p_signal, Object *p_target, GCHandleIntPtr p_awaiter_handle_ptr);

class BaseSignalCallable : public CallableCustom {
public:
	virtual StringName get_signal() const = 0;
};

class SignalAwaiterCallable : public BaseSignalCallable {
	ObjectID target_id;
	DotNetGCHandleData awaiter_handle;
	StringName signal;

public:
	static bool compare_equal(const CallableCustom *p_a, const CallableCustom *p_b);
	static bool compare_less(const CallableCustom *p_a, const CallableCustom *p_b);

	static constexpr CompareEqualFunc compare_equal_func_ptr = &SignalAwaiterCallable::compare_equal;
	static constexpr CompareEqualFunc compare_less_func_ptr = &SignalAwaiterCallable::compare_less;

	uint32_t hash() const override;

	String get_as_text() const override;

	CompareEqualFunc get_compare_equal_func() const override;
	CompareLessFunc get_compare_less_func() const override;

	ObjectID get_object() const override;

	StringName get_signal() const override;

	void call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const override;

	SignalAwaiterCallable(Object *p_target, DotNetGCHandleData p_awaiter_handle, const StringName &p_signal);
	~SignalAwaiterCallable();
};

class EventSignalCallable : public BaseSignalCallable {
	Object *owner = nullptr;
	StringName event_signal_name;

public:
	static bool compare_equal(const CallableCustom *p_a, const CallableCustom *p_b);
	static bool compare_less(const CallableCustom *p_a, const CallableCustom *p_b);

	static constexpr CompareEqualFunc compare_equal_func_ptr = &EventSignalCallable::compare_equal;
	static constexpr CompareEqualFunc compare_less_func_ptr = &EventSignalCallable::compare_less;

	uint32_t hash() const override;

	String get_as_text() const override;

	CompareEqualFunc get_compare_equal_func() const override;
	CompareLessFunc get_compare_less_func() const override;

	ObjectID get_object() const override;

	StringName get_signal() const override;

	void call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const override;

	EventSignalCallable(Object *p_owner, const StringName &p_event_signal_name);
};
