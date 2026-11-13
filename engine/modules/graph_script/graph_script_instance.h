/**************************************************************************/
/*  graph_script_instance.h                                               */

#pragma once

#include "graph_script.h"

#include "core/containers/hash_map.h"
#include "core/math/random_number_generator.h"
#include "core/object/script_instance.h"

// The variables of one entity and the way to call its graph functions.
class Entity;
class GraphCall;

class GraphScriptInstance final : public ScriptInstance {
	Ref<GraphScript> script;
	Object *owner = nullptr;
	Ref<RandomNumberGenerator> random;
	// The value of each variable by name. The script calls refresh after every change to its variables.
	HashMap<StringName, Variant> values;
	// How many calls are open on this instance. A call step may call the function it sits in.
	int depth = 0;
	// Set when a call went past the limit. Every open call then gives up, so a graph that
	// branches into two recursive calls ends at once.
	bool too_deep = false;
	// The calls set aside by a latent step. This list is what keeps each of them alive.
	Vector<Ref<GraphCall>> waiting;

	void _drop_waiting();
	void _connect_receiver(Entity *p_owner, const GraphCompiledFunction &p_function);

	// Each open call holds a C++ frame, so the limit keeps a runaway graph off the stack.
	static constexpr int MAX_CALL_DEPTH = 64;

public:
	GraphScriptInstance(const Ref<GraphScript> &p_script, Object *p_owner);
	~GraphScriptInstance();

	// Keeps the value of each variable that still has its name and type. The script calls this
	// after a change.
	void refresh();

	void add_waiting(const Ref<GraphCall> &p_call);
	void remove_waiting(const GraphCall *p_call);
	// A change to the script voids the frames of its waiting calls, so they end here and the
	// log says how many.
	void drop_waiting_calls();

	// What the debugger shows of an instance: each variable and its value.
	void list_members(List<String> *r_names, List<Variant> *r_values) const;

	virtual bool set(const StringName &p_name, const Variant &p_value) override;
	virtual bool get(const StringName &p_name, Variant &r_ret) const override;
	virtual void get_property_list(List<PropertyInfo> *r_properties) const override;
	virtual Variant::Type get_property_type(const StringName &p_name, bool *r_is_valid = nullptr) const override;
	virtual void validate_property(PropertyInfo &p_property) const override {}

	virtual bool property_can_revert(const StringName &p_name) const override;
	virtual bool property_get_revert(const StringName &p_name, Variant &r_ret) const override;

	virtual Object *get_owner() override { return owner; }

	// The steps that draw a random number use this. A test passes one with a seed.
	void set_random(const Ref<RandomNumberGenerator> &p_random) { random = p_random; }
	const Ref<RandomNumberGenerator> &get_random() const { return random; }

	virtual void get_method_list(List<MethodInfo> *r_list) const override;
	virtual bool has_method(const StringName &p_method) const override;
	virtual Variant callp(const StringName &p_method, const Variant **p_args, int p_argcount, Callable::CallError &r_error) override;
	virtual void notification(int p_notification, bool p_reversed = false) override;

	virtual Script *get_script() const override { return script.ptr(); }
	virtual ScriptLanguage *get_language() override;
};
