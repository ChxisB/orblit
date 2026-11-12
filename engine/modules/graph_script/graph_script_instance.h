/**************************************************************************/
/*  graph_script_instance.h                                               */

#pragma once

#include "graph_script.h"

#include "core/containers/hash_map.h"
#include "core/object/script_instance.h"

// The variables of one entity and the way to call its graph functions.
class GraphScriptInstance final : public ScriptInstance {
	Ref<GraphScript> script;
	Object *owner = nullptr;
	// A variable the script gained after this instance was made reads as its default.
	HashMap<StringName, Variant> values;

public:
	GraphScriptInstance(const Ref<GraphScript> &p_script, Object *p_owner);

	virtual bool set(const StringName &p_name, const Variant &p_value) override;
	virtual bool get(const StringName &p_name, Variant &r_ret) const override;
	virtual void get_property_list(List<PropertyInfo> *r_properties) const override;
	virtual Variant::Type get_property_type(const StringName &p_name, bool *r_is_valid = nullptr) const override;
	virtual void validate_property(PropertyInfo &p_property) const override {}

	virtual bool property_can_revert(const StringName &p_name) const override;
	virtual bool property_get_revert(const StringName &p_name, Variant &r_ret) const override;

	virtual Object *get_owner() override { return owner; }

	virtual void get_method_list(List<MethodInfo> *r_list) const override;
	virtual bool has_method(const StringName &p_method) const override;
	virtual Variant callp(const StringName &p_method, const Variant **p_args, int p_argcount, Callable::CallError &r_error) override;
	virtual void notification(int p_notification, bool p_reversed = false) override {}

	virtual Script *get_script() const override { return script.ptr(); }
	virtual ScriptLanguage *get_language() override;
};
