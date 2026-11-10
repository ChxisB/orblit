/**************************************************************************/
/*  jni_singleton.h                                                       */

#pragma once

#include "java_class_wrapper.h"

#include "core/containers/rb_map.h"
#include "core/variant/variant.h"

class JNISingleton : public Object {
	ORBLIT_CLASS(JNISingleton, Object);

	struct MethodData {
		Variant::Type ret_type;
		Vector<Variant::Type> argtypes;
	};

	RBMap<StringName, MethodData> method_map;
	Ref<JavaObject> wrapped_object;

protected:
	static void register_members();
	bool _get(const StringName &p_name, Variant &r_property) const;

public:
	virtual Variant callp(const StringName &p_method, const Variant **p_args, int p_argcount, Callable::CallError &r_error) override;

	Ref<JavaObject> get_wrapped_object() const {
		return wrapped_object;
	}

	bool has_java_method(const StringName &p_method) const {
		return method_map.has(p_method);
	}

	void add_method(const StringName &p_name, const Vector<Variant::Type> &p_args, Variant::Type p_ret_type);

	void add_signal(const StringName &p_name, const Vector<Variant::Type> &p_args);

	JNISingleton() {}

	JNISingleton(const Ref<JavaObject> &p_wrapped_object) {
		wrapped_object = p_wrapped_object;
	}

	~JNISingleton() {
		method_map.clear();
		wrapped_object.unref();
	}
};
