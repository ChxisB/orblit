/**************************************************************************/
/*  graph_step_reflect.cpp                                                */

#include "graph_step_reflect.h"

#include "graph_call.h"

#include "core/config/engine.h"
#include "core/containers/local_vector.h"
#include "core/object/class_registry.h"
#include "scene/stage/entity.h"
#include "scene/stage/stage.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp and the ports the derive
// functions add.
constexpr int THEN = 0;
constexpr int RESULT = 1;
constexpr int SET_TARGET = 1;
constexpr int SET_VALUE = 2;
constexpr int EMIT_FIRST_ARGUMENT = 1;
constexpr int EMIT_ON_TARGET = 1;
constexpr int EMIT_ON_FIRST_ARGUMENT = 2;
constexpr int CONNECT_TARGET = 1;
constexpr int NEW_OBJECT = 1;
constexpr int FREE_OBJECT = 1;

// What a resolve function keeps in the values of a call step.
constexpr int NAME = 0;
constexpr int OWNER = 1;
constexpr int IS_STATIC = 2;

struct Member {
	StringName owner;
	StringName name;
};

// A text parameter, or an empty string when it is missing or not text.
String text_of(const Dictionary &p_parameters, const char *p_key) {
	const Variant value = p_parameters.get(p_key, Variant());
	const bool is_text = value.get_type() == Variant::STRING || value.get_type() == Variant::STRING_NAME;
	return is_text ? String(value) : String();
}

String read_text(const Dictionary &p_parameters, const char *p_key, StringName &r_text) {
	r_text = text_of(p_parameters, p_key);
	return r_text.is_empty() ? vformat("the step needs a '%s' parameter.", p_key) : String();
}

String read_class(const Dictionary &p_parameters, StringName &r_class) {
	const String problem = read_text(p_parameters, "class", r_class);
	return !problem.is_empty() || ClassRegistry::class_exists(r_class) ? problem : vformat("there is no class named '%s'.", r_class);
}

String read_class_member(const Dictionary &p_parameters, const char *p_key, Member &r_member) {
	const String problem = read_class(p_parameters, r_member.owner);
	return problem.is_empty() ? read_text(p_parameters, p_key, r_member.name) : problem;
}

String find_class_method(const Dictionary &p_parameters, Member &r_member, MethodInfo &r_info) {
	const String problem = read_class_member(p_parameters, "method", r_member);
	if (!problem.is_empty()) {
		return problem;
	}
	return ClassRegistry::get_method_info(r_member.owner, r_member.name, &r_info) ? String() : vformat("the class '%s' has no method named '%s'.", r_member.owner, r_member.name);
}

String find_builtin_method(const Dictionary &p_parameters, Variant::Type &r_type, MethodInfo &r_info) {
	StringName type_name;
	StringName method;
	String problem = read_text(p_parameters, "type", type_name);
	if (problem.is_empty()) {
		problem = read_text(p_parameters, "method", method);
	}
	if (!problem.is_empty()) {
		return problem;
	}
	r_type = Variant::get_type_by_name(type_name);
	if (r_type == Variant::VARIANT_MAX || !Variant::has_builtin_method(r_type, method)) {
		return vformat("the type '%s' has no method named '%s'.", type_name, method);
	}
	r_info = Variant::get_builtin_method_info(r_type, method);
	return String();
}

String find_utility_function(const Dictionary &p_parameters, MethodInfo &r_info) {
	StringName function;
	const String problem = read_text(p_parameters, "function", function);
	if (!problem.is_empty()) {
		return problem;
	}
	if (!Variant::has_utility_function(function)) {
		return vformat("there is no utility function named '%s'.", function);
	}
	r_info = Variant::get_utility_function_info(function);
	return String();
}

String find_property(const Dictionary &p_parameters, Member &r_member, PropertyInfo &r_info) {
	const String problem = read_class_member(p_parameters, "property", r_member);
	if (!problem.is_empty()) {
		return problem;
	}
	List<PropertyInfo> properties;
	ClassRegistry::get_property_list(r_member.owner, &properties);
	const String wanted = r_member.name;
	for (const PropertyInfo &property : properties) {
		if (property.name == wanted) {
			r_info = property;
			return String();
		}
	}
	return vformat("the class '%s' has no property named '%s'.", r_member.owner, r_member.name);
}

// An argument with a default may stay unwired. Its value then is the default.
void add_arguments(const MethodInfo &p_info, Vector<GraphPort> &r_inputs) {
	const int required = p_info.arguments.size() - p_info.default_arguments.size();
	for (int i = 0; i < p_info.arguments.size(); i++) {
		GraphPort port;
		port.name = p_info.arguments[i].name;
		port.type = p_info.arguments[i].type;
		// The name of an argument may be a parameter the step has, and a release build gives
		// arguments other names, so the value of an unwired argument goes by its position.
		port.parameter = vformat("argument_%d", i);
		port.optional = i >= required;
		if (port.optional) {
			port.fallback = p_info.default_arguments[i - required];
		}
		r_inputs.push_back(port);
	}
}

void add_signature(const MethodInfo &p_info, GraphDerivedPorts &r_ports) {
	add_arguments(p_info, r_ports.inputs);
	if (GraphStepReflect::returns_value(p_info)) {
		r_ports.outputs.push_back({ "result", GraphPortSort::DATA, p_info.return_val.type });
	}
}

// A value type is copied into the step, so a method that changes it would change nothing the
// graph can see. These types share their data between copies.
bool shares_data(Variant::Type p_type) {
	return p_type == Variant::ARRAY || p_type == Variant::DICTIONARY || (p_type >= Variant::PACKED_BYTE_ARRAY && p_type <= Variant::PACKED_VECTOR4_ARRAY);
}

// A query step runs each time something reads its output, so it must not change anything.
String check_shape(const GraphCompiledStep &p_step, const MethodInfo &p_info, bool p_checks_const) {
	if (GraphStepKinds::get_pin_count(*p_step.kind, p_step.parameters) > 0 && !(p_info.flags & METHOD_FLAG_VARARG)) {
		return vformat("'%s' takes a fixed number of arguments, so the step cannot have extra ones.", p_info.name);
	}
	if (!p_step.kind->is_pure()) {
		return String();
	}
	if (!GraphStepReflect::returns_value(p_info)) {
		return vformat("a query step needs a method that gives a value, and '%s' gives none.", p_info.name);
	}
	if (p_checks_const && !(p_info.flags & (METHOD_FLAG_CONST | METHOD_FLAG_STATIC))) {
		return vformat("a query step may run more than once, so '%s' must not change anything.", p_info.name);
	}
	return String();
}

// Keeps the name of what to call, then its owner, and the port where the arguments begin.
void keep_call(GraphCompiledStep &p_step, const StringName &p_name, const Variant &p_owner, int p_first_argument) {
	p_step.values.push_back(p_name);
	p_step.values.push_back(p_owner);
	p_step.operation = p_first_argument;
}

Object *live_object(GraphCall &p_call, const GraphCompiledStep &p_step, const Variant &p_value) {
	Object *object = p_value.get_validated_object();
	if (!object) {
		p_call.fail(p_step, "needs an object that exists.");
	}
	return object;
}

// The values on the argument ports of one call, as the array of pointers a call needs.
class Invocation {
	GraphCall &call;
	const GraphCompiledStep &step;
	LocalVector<Variant> values;
	LocalVector<const Variant *> pointers;

public:
	Callable::CallError error;

	Invocation(GraphCall &p_call, const GraphCompiledStep &p_step, int p_first_argument) :
			call(p_call), step(p_step) {
		for (int port = p_first_argument; port < p_step.inputs.size(); port++) {
			values.push_back(p_call.input(p_step, port));
		}
		for (const Variant &value : values) {
			pointers.push_back(&value);
		}
	}

	Invocation(const Invocation &) = delete;

	const Variant **arguments() { return pointers.ptr(); }
	int count() const { return values.size(); }

	// Fails the call when the engine says it did not work.
	void report(Object *p_base) {
		if (error.error == Callable::CallError::CALL_OK) {
			return;
		}
		const StringName name = step.values[NAME];
		call.fail(step, p_base ? Variant::get_call_error_text(p_base, name, arguments(), count(), error) : Variant::get_call_error_text(name, arguments(), count(), error));
	}
};

// A flow step keeps the result of its call on the port after "then", when it has one.
int keep_result(GraphCall &p_call, const GraphCompiledStep &p_step, const Variant &p_result) {
	if (p_step.next.size() > 1) {
		p_call.store(p_step, RESULT, p_result);
	}
	return THEN;
}

int run_with_result(GraphEvalFn p_call_it, GraphCall &p_call, const GraphCompiledStep &p_step) {
	return keep_result(p_call, p_step, p_call_it(p_call, p_step));
}

// The callee gave its value with the "completed" signal, as the only argument.
int resume_call_result(GraphCall &p_call, GraphPending &p_frame) {
	const Array heard = p_frame.container;
	p_frame.done = true;
	return keep_result(p_call, p_call.get_step(p_frame.step), heard.is_empty() ? Variant() : heard[0]);
}

Variant call_static(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const MethodBind *method = ClassRegistry::get_method(p_step.values[OWNER], p_step.values[NAME]);
	ERR_FAIL_NULL_V(method, Variant());
	Invocation invocation(p_call, p_step, p_step.operation);
	// A static method has no object to call it on.
	const Variant result = method->call(nullptr, invocation.arguments(), invocation.count(), invocation.error);
	invocation.report(nullptr);
	return result;
}

// A function of this script or of another one. The target is the owner when nothing is wired.
Variant call_function(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = GraphStepReflect::read_target(p_call, p_step, p_step.operation - 1);
	if (!target) {
		return Variant();
	}
	Invocation invocation(p_call, p_step, p_step.operation);
	const Variant result = target->callp(p_step.values[NAME], invocation.arguments(), invocation.count(), invocation.error);
	invocation.report(target);
	return result;
}

int emit_from(GraphCall &p_call, const GraphCompiledStep &p_step, Object *p_emitter, int p_first_argument) {
	const StringName signal = p_step.parameters["signal"];
	if (!p_emitter->has_signal(signal)) {
		p_call.fail(p_step, vformat("the object has no signal named '%s'.", signal));
		return GraphFlow::END;
	}
	Invocation invocation(p_call, p_step, p_first_argument);
	// The return value says only that nothing listens, which is no fault.
	p_emitter->emit_signalp(signal, invocation.arguments(), invocation.count());
	return THEN;
}

bool is_engine_singleton(const Object *p_object) {
	List<Engine::Singleton> singletons;
	Engine::get_singleton()->get_singletons(&singletons);
	for (const Engine::Singleton &singleton : singletons) {
		if (singleton.ptr == p_object) {
			return true;
		}
	}
	return false;
}

String free_problem(const Object *p_object, const Object *p_owner) {
	if (p_object->is_ref_counted()) {
		return "cannot free an object that counts its references. It frees itself.";
	}
	if (p_object == Stage::get_singleton() || is_engine_singleton(p_object)) {
		return "cannot free the stage or an engine singleton.";
	}
	if (p_object == p_owner && !Object::cast_to<Entity>(p_owner)) {
		return "the owner of the script is not an entity, so it cannot free itself.";
	}
	return String();
}

} // namespace

namespace GraphStepReflect {

Object *read_target(GraphCall &p_call, const GraphCompiledStep &p_step, int p_port) {
	const Variant target = p_call.input(p_step, p_port);
	// Only a pin with no wire means the owner. A wire that carries nothing is a fault, or an unset
	// variable would make the step act on the owner.
	const bool wired = p_step.inputs[p_port].step >= 0;
	return target.get_type() == Variant::NIL && !wired ? p_call.get_owner() : live_object(p_call, p_step, target);
}

bool returns_value(const MethodInfo &p_info) {
	return p_info.return_val.type != Variant::NIL || (p_info.return_val.usage & PROPERTY_USAGE_NIL_IS_VARIANT);
}

String derive_method(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Member member;
	MethodInfo info;
	const String problem = find_class_method(p_parameters, member, info);
	if (problem.is_empty()) {
		add_signature(info, r_ports);
	}
	return problem;
}

String derive_static(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Member member;
	MethodInfo info;
	String problem = find_class_method(p_parameters, member, info);
	if (problem.is_empty() && !(info.flags & METHOD_FLAG_STATIC)) {
		problem = vformat("the method '%s' is not static.", info.name);
	}
	if (problem.is_empty()) {
		add_signature(info, r_ports);
	}
	return problem;
}

String derive_builtin(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Variant::Type type = Variant::NIL;
	MethodInfo info;
	const String problem = find_builtin_method(p_parameters, type, info);
	if (!problem.is_empty()) {
		return problem;
	}
	if (!(info.flags & METHOD_FLAG_STATIC)) {
		r_ports.inputs.push_back({ "target", GraphPortSort::DATA, type, "target" });
	}
	add_signature(info, r_ports);
	return String();
}

String derive_utility(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	MethodInfo info;
	const String problem = find_utility_function(p_parameters, info);
	if (problem.is_empty()) {
		add_signature(info, r_ports);
	}
	return problem;
}

String derive_get_property(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Member member;
	PropertyInfo info;
	const String problem = find_property(p_parameters, member, info);
	if (problem.is_empty()) {
		r_ports.outputs.push_back({ "value", GraphPortSort::DATA, info.type });
	}
	return problem;
}

String derive_set_property(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Member member;
	PropertyInfo info;
	String problem = find_property(p_parameters, member, info);
	if (problem.is_empty() && ClassRegistry::get_property_setter(member.owner, member.name).is_empty()) {
		problem = vformat("the property '%s' cannot be set.", member.name);
	}
	if (problem.is_empty()) {
		r_ports.inputs.push_back({ "value", GraphPortSort::DATA, info.type, "value" });
	}
	return problem;
}

String derive_emit_on(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	Member member;
	const String problem = read_class_member(p_parameters, "signal", member);
	if (!problem.is_empty()) {
		return problem;
	}
	MethodInfo info;
	if (!ClassRegistry::get_signal(member.owner, member.name, &info)) {
		return vformat("the class '%s' has no signal named '%s'.", member.owner, member.name);
	}
	add_arguments(info, r_ports.inputs);
	return String();
}

String derive_new(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	StringName class_name;
	String problem = read_class(p_parameters, class_name);
	if (problem.is_empty() && !ClassRegistry::can_instantiate(class_name)) {
		problem = vformat("the class '%s' cannot be made.", class_name);
	}
	if (problem.is_empty()) {
		r_ports.outputs.push_back({ "object", GraphPortSort::DATA, Variant::OBJECT });
	}
	return problem;
}

String resolve_method(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	Member member;
	MethodInfo info;
	String problem = find_class_method(p_step.parameters, member, info);
	if (problem.is_empty()) {
		problem = check_shape(p_step, info, true);
	}
	if (problem.is_empty()) {
		keep_call(p_step, member.name, member.owner, p_step.kind->inputs.count);
	}
	return problem;
}

String resolve_static(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	Member member;
	MethodInfo info;
	String problem = find_class_method(p_step.parameters, member, info);
	if (problem.is_empty()) {
		problem = check_shape(p_step, info, false);
	}
	if (problem.is_empty()) {
		keep_call(p_step, member.name, member.owner, p_step.kind->inputs.count);
	}
	return problem;
}

String resolve_builtin(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	Variant::Type type = Variant::NIL;
	MethodInfo info;
	String problem = find_builtin_method(p_step.parameters, type, info);
	if (problem.is_empty()) {
		problem = check_shape(p_step, info, true);
	}
	const bool is_static = info.flags & METHOD_FLAG_STATIC;
	// A call step hands its target on by value, so only a type that shares its data can be changed.
	if (problem.is_empty() && !p_step.kind->is_pure() && !(info.flags & (METHOD_FLAG_CONST | METHOD_FLAG_STATIC)) && !shares_data(type)) {
		problem = vformat("'%s' changes its target, and a %s is copied into the step. Use a method that gives a new value.", info.name, Variant::get_type_name(type));
	}
	if (problem.is_empty()) {
		keep_call(p_step, info.name, int(type), p_step.kind->inputs.count + (is_static ? 0 : 1));
		p_step.values.push_back(is_static);
	}
	return problem;
}

String resolve_utility(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	MethodInfo info;
	String problem = find_utility_function(p_step.parameters, info);
	if (problem.is_empty()) {
		problem = check_shape(p_step, info, false);
	}
	if (problem.is_empty()) {
		keep_call(p_step, info.name, Variant(), p_step.kind->inputs.count);
	}
	return problem;
}

String resolve_emit(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	StringName signal;
	return read_text(p_step.parameters, "signal", signal);
}

String resolve_connect(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	StringName signal;
	StringName method;
	const String problem = read_text(p_step.parameters, "signal", signal);
	return problem.is_empty() ? read_text(p_step.parameters, "method", method) : problem;
}

String resolve_call_function(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	StringName function;
	String problem = read_text(p_step.parameters, "function", function);
	if (problem.is_empty() && p_step.parameters.get("wait", false).get_type() != Variant::BOOL) {
		problem = "the 'wait' parameter must be true or false.";
	}
	if (problem.is_empty()) {
		// shortcut: the callee is looked up when the step runs, because it may be in another script.
		// Check a call to this script's own functions at compile time when that costs someone a run.
		keep_call(p_step, function, Variant(), p_step.kind->inputs.count);
	}
	return problem;
}

Variant eval_call_method(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = read_target(p_call, p_step, p_step.operation - 1);
	if (!target) {
		return Variant();
	}
	Invocation invocation(p_call, p_step, p_step.operation);
	const StringName name = p_step.values[NAME];
	const Variant result = target->callp(name, invocation.arguments(), invocation.count(), invocation.error);
	invocation.report(target);
	return result;
}

Variant eval_call_builtin(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Invocation invocation(p_call, p_step, p_step.operation);
	const StringName name = p_step.values[NAME];
	const Variant::Type type = Variant::Type(int(p_step.values[OWNER]));
	Variant result;
	if (p_step.values[IS_STATIC]) {
		Variant::call_static(type, name, invocation.arguments(), invocation.count(), result, invocation.error);
	} else {
		Variant target = p_call.input(p_step, p_step.operation - 1);
		target.callp(name, invocation.arguments(), invocation.count(), result, invocation.error);
	}
	invocation.report(nullptr);
	return result;
}

Variant eval_call_utility(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Invocation invocation(p_call, p_step, p_step.operation);
	Variant result;
	Variant::call_utility_function(p_step.values[NAME], &result, invocation.arguments(), invocation.count(), invocation.error);
	invocation.report(nullptr);
	return result;
}

Variant eval_get_property(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = read_target(p_call, p_step, 0);
	if (!target) {
		return Variant();
	}
	const StringName property = p_step.parameters["property"];
	bool valid = false;
	const Variant value = target->get(property, &valid);
	if (!valid) {
		p_call.fail(p_step, vformat("the object has no property named '%s'.", property));
	}
	return value;
}

int run_call_method(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return run_with_result(eval_call_method, p_call, p_step);
}

int run_call_static(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return run_with_result(call_static, p_call, p_step);
}

int run_call_builtin(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return run_with_result(eval_call_builtin, p_call, p_step);
}

int run_call_utility(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return run_with_result(eval_call_utility, p_call, p_step);
}

// A step marked to wait carries on when the function it called has finished. A function that
// does not wait has finished already, so the step carries on at once.
int run_call_function(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant result = call_function(p_call, p_step);
	const Ref<GraphCall> callee = result;
	if (callee.is_null() || !callee->is_waiting() || !bool(p_step.parameters.get("wait", false))) {
		return keep_result(p_call, p_step, result);
	}
	GraphPending frame;
	frame.step = p_step.index;
	frame.resume = resume_call_result;
	p_call.wait_for(callee.ptr(), SNAME("completed"), frame);
	return GraphFlow::END;
}

int run_set_property(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = read_target(p_call, p_step, SET_TARGET);
	if (!target) {
		return GraphFlow::END;
	}
	const StringName property = p_step.parameters["property"];
	bool valid = false;
	target->set(property, p_call.input(p_step, SET_VALUE), &valid);
	if (!valid) {
		p_call.fail(p_step, vformat("the object has no property named '%s'.", property));
		return GraphFlow::END;
	}
	return THEN;
}

int run_new(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *made = ClassRegistry::instantiate(StringName(p_step.parameters["class"]));
	if (!made) {
		p_call.fail(p_step, "could not make an object of that class.");
		return GraphFlow::END;
	}
	p_call.store(p_step, NEW_OBJECT, made);
	return THEN;
}

int run_free(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *object = live_object(p_call, p_step, p_call.input(p_step, FREE_OBJECT));
	if (!object) {
		return GraphFlow::END;
	}
	const String problem = free_problem(object, p_call.get_owner());
	if (!problem.is_empty()) {
		p_call.fail(p_step, problem);
		return GraphFlow::END;
	}
	// An entity may be in the middle of a frame, so the stage deletes it at the end of one.
	if (Entity *entity = Object::cast_to<Entity>(object)) {
		entity->queue_destroy();
	} else {
		memdelete(object);
	}
	return THEN;
}

int run_emit(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return emit_from(p_call, p_step, p_call.get_owner(), EMIT_FIRST_ARGUMENT);
}

int run_emit_on(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = read_target(p_call, p_step, EMIT_ON_TARGET);
	return target ? emit_from(p_call, p_step, target, EMIT_ON_FIRST_ARGUMENT) : GraphFlow::END;
}

int run_connect(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *target = read_target(p_call, p_step, CONNECT_TARGET);
	if (!target) {
		return GraphFlow::END;
	}
	Object *owner = p_call.get_owner();
	const StringName signal = p_step.parameters["signal"];
	const StringName method = p_step.parameters["method"];
	if (!target->has_signal(signal)) {
		p_call.fail(p_step, vformat("the object has no signal named '%s'.", signal));
		return GraphFlow::END;
	}
	if (!owner->has_method(method)) {
		p_call.fail(p_step, vformat("the owner of the script has no method named '%s'.", method));
		return GraphFlow::END;
	}
	const Callable callable(owner, method);
	if (!target->is_connected(signal, callable)) {
		target->connect(signal, callable);
	}
	return THEN;
}

} // namespace GraphStepReflect
