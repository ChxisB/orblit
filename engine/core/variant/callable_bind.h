/**************************************************************************/
/*  callable_bind.h                                                       */

#pragma once

#include "core/variant/callable.h"
#include "core/variant/variant.h"

class CallableCustomBind : public CallableCustom {
	Callable callable;
	Vector<Variant> binds;

	static bool _equal_func(const CallableCustom *p_a, const CallableCustom *p_b);
	static bool _less_func(const CallableCustom *p_a, const CallableCustom *p_b);

public:
	//for every type that inherits, these must always be the same for this type
	virtual uint32_t hash() const override;
	virtual String get_as_text() const override;
	virtual CompareEqualFunc get_compare_equal_func() const override;
	virtual CompareLessFunc get_compare_less_func() const override;
	virtual bool is_valid() const override;
	virtual StringName get_method() const override;
	virtual ObjectID get_object() const override;
	virtual void call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const override;
	virtual Error rpc(int p_peer_id, const Variant **p_arguments, int p_argcount, Callable::CallError &r_call_error) const override;
	virtual const Callable *get_base_comparator() const override;
	virtual int get_argument_count(bool &r_is_valid) const override;
	virtual int get_bound_arguments_count() const override;
	virtual void get_bound_arguments(Vector<Variant> &r_arguments) const override;
	virtual int get_unbound_arguments_count() const override;
	Callable get_callable() { return callable; }
	Vector<Variant> get_binds() { return binds; }

	CallableCustomBind(const Callable &p_callable, const Vector<Variant> &p_binds);
};

class CallableCustomUnbind : public CallableCustom {
	Callable callable;
	int argcount;

	static bool _equal_func(const CallableCustom *p_a, const CallableCustom *p_b);
	static bool _less_func(const CallableCustom *p_a, const CallableCustom *p_b);

public:
	//for every type that inherits, these must always be the same for this type
	virtual uint32_t hash() const override;
	virtual String get_as_text() const override;
	virtual CompareEqualFunc get_compare_equal_func() const override;
	virtual CompareLessFunc get_compare_less_func() const override;
	virtual bool is_valid() const override;
	virtual StringName get_method() const override;
	virtual ObjectID get_object() const override;
	virtual void call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const override;
	virtual Error rpc(int p_peer_id, const Variant **p_arguments, int p_argcount, Callable::CallError &r_call_error) const override;
	virtual const Callable *get_base_comparator() const override;
	virtual int get_argument_count(bool &r_is_valid) const override;
	virtual int get_bound_arguments_count() const override;
	virtual void get_bound_arguments(Vector<Variant> &r_arguments) const override;
	virtual int get_unbound_arguments_count() const override;

	Callable get_callable() { return callable; }
	int get_unbinds() { return argcount; }

	CallableCustomUnbind(const Callable &p_callable, int p_argcount);
};
