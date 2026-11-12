/**************************************************************************/
/*  graph_step_values.cpp                                                 */

#include "graph_step_values.h"

#include "graph_call.h"

#include "core/config/engine.h"
#include "core/containers/local_vector.h"
#include "core/core_constants.h"
#include "core/object/class_registry.h"

namespace {

constexpr int VALUE = 0;

// A constant reader finds the value a constant step gives. It returns the problem, or an
// empty string.
using ConstantReader = String (*)(const Dictionary &p_parameters, Variant &r_value);

String read_literal(const Dictionary &p_parameters, Variant &r_value) {
	if (!p_parameters.has("value")) {
		return "the constant has no 'value' parameter.";
	}
	r_value = p_parameters["value"];
	return String();
}

String read_type_constant(const Dictionary &p_parameters, Variant &r_value) {
	const String type_name = p_parameters.get("type", "");
	const String name = p_parameters.get("name", "");
	const Variant::Type type = Variant::get_type_by_name(type_name);
	if (type == Variant::VARIANT_MAX) {
		return vformat("the type '%s' is not known.", type_name);
	}
	if (!Variant::has_constant(type, name)) {
		return vformat("%s has no constant named '%s'.", type_name, name);
	}
	r_value = Variant::get_constant_value(type, name);
	return String();
}

struct MathConstant {
	const char *name;
	double value;
};

constexpr MathConstant MATH_CONSTANTS[] = {
	{ "pi", Math::PI },
	{ "tau", Math::TAU },
	{ "e", Math::E },
	{ "sqrt2", Math::SQRT2 },
	{ "inf", Math::INF },
	{ "nan", Math::NaN },
};

String read_math_constant(const Dictionary &p_parameters, Variant &r_value) {
	const String name = p_parameters.get("name", "");
	for (const MathConstant &constant : MATH_CONSTANTS) {
		if (name == constant.name) {
			r_value = constant.value;
			return String();
		}
	}
	return vformat("there is no math constant named '%s'. Use pi, tau, e, sqrt2, inf or nan.", name);
}

String read_global_constant(const Dictionary &p_parameters, Variant &r_value) {
	const String name = p_parameters.get("name", "");
	if (!CoreConstants::is_global_constant(name)) {
		return vformat("there is no global constant named '%s'.", name);
	}
	r_value = CoreConstants::get_global_constant_value(CoreConstants::get_global_constant_index(name));
	return String();
}

String read_class_constant_of(const StringName &p_class, const String &p_name, Variant &r_value) {
	if (!ClassRegistry::has_integer_constant(p_class, p_name)) {
		return vformat("%s has no constant named '%s'.", p_class, p_name);
	}
	r_value = ClassRegistry::get_integer_constant(p_class, p_name);
	return String();
}

String read_class_constant(const Dictionary &p_parameters, Variant &r_value) {
	const String class_name = p_parameters.get("class", "");
	if (!ClassRegistry::class_exists(class_name)) {
		return vformat("the class '%s' is not known.", class_name);
	}
	return read_class_constant_of(class_name, p_parameters.get("name", ""), r_value);
}

String read_singleton_constant(const Dictionary &p_parameters, Variant &r_value) {
	const String singleton_name = p_parameters.get("singleton", "");
	// get_singleton_object prints an engine error for a name it does not know, and a half-edited
	// step reaches this with an empty name.
	const Engine *engine = Engine::get_singleton();
	const Object *singleton = engine->has_singleton(singleton_name) ? engine->get_singleton_object(singleton_name) : nullptr;
	if (!singleton) {
		return vformat("there is no singleton named '%s'.", singleton_name);
	}
	return read_class_constant_of(singleton->get_class_name(), p_parameters.get("name", ""), r_value);
}

struct ConstantSource {
	const char *name;
	ConstantReader read;
};

// A list of cases is data.
constexpr ConstantSource CONSTANT_SOURCES[] = {
	{ "literal", read_literal },
	{ "type", read_type_constant },
	{ "math", read_math_constant },
	{ "global", read_global_constant },
	{ "class", read_class_constant },
	{ "singleton", read_singleton_constant },
};

// Reads the "type" parameter into the operation of the step. The problem is empty when it is fine.
String resolve_type(GraphCompiledStep &p_step, const char *p_parameter) {
	const String type_name = p_step.parameters.get(p_parameter, "");
	const Variant::Type type = Variant::get_type_by_name(type_name);
	if (type == Variant::VARIANT_MAX) {
		return vformat("the type '%s' is not known.", type_name);
	}
	p_step.operation = type;
	return String();
}

} // namespace

namespace GraphStepValues {

Variant eval_stored(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_step.values[0];
}

Variant eval_make_array(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Array items;
	for (int i = 0; i < p_step.inputs.size(); i++) {
		items.push_back(p_call.input(p_step, i));
	}
	return items;
}

Variant eval_make_dictionary(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Dictionary entries;
	for (int i = 0; i + 1 < p_step.inputs.size(); i += 2) {
		entries[p_call.input(p_step, i)] = p_call.input(p_step, i + 1);
	}
	return entries;
}

Variant eval_compose(GraphCall &p_call, const GraphCompiledStep &p_step) {
	LocalVector<Variant> arguments;
	LocalVector<const Variant *> pointers;
	arguments.reserve(p_step.inputs.size());
	for (int i = 0; i < p_step.inputs.size(); i++) {
		arguments.push_back(p_call.input(p_step, i));
	}
	for (const Variant &argument : arguments) {
		pointers.push_back(&argument);
	}
	Variant made;
	Callable::CallError error;
	Variant::construct(Variant::Type(p_step.operation), made, pointers.ptr(), pointers.size(), error);
	if (error.error != Callable::CallError::CALL_OK) {
		p_call.fail(p_step, vformat("cannot build a %s from these arguments.", Variant::get_type_name(Variant::Type(p_step.operation))));
	}
	return made;
}

Variant eval_decompose(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant value = p_call.input(p_step, VALUE);
	if (value.get_type() != Variant::Type(p_step.operation)) {
		p_call.fail(p_step, vformat("needs a %s and got a %s.", Variant::get_type_name(Variant::Type(p_step.operation)), Variant::get_type_name(value.get_type())));
		return Variant();
	}
	Array parts;
	for (const Variant &member : p_step.values) {
		bool valid = true;
		parts.push_back(value.get_named(member, valid));
	}
	// One output gives its value. Several give an array.
	return parts.size() == 1 ? parts[0] : Variant(parts);
}

Variant eval_convert(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant value = p_call.input(p_step, VALUE);
	const Variant *argument = &value;
	Variant converted;
	Callable::CallError error;
	Variant::construct(Variant::Type(p_step.operation), converted, &argument, 1, error);
	if (error.error != Callable::CallError::CALL_OK) {
		p_call.fail(p_step, vformat("cannot turn a %s into a %s.", Variant::get_type_name(value.get_type()), Variant::get_type_name(Variant::Type(p_step.operation))));
	}
	return converted;
}

Variant eval_cast(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant value = p_call.input(p_step, VALUE);
	const Object *object = value.get_type() == Variant::OBJECT ? value.get_validated_object() : nullptr;
	const bool fits = object && object->is_class(StringName(p_step.values[0]));
	Array outputs;
	outputs.push_back(fits ? value : Variant(static_cast<Object *>(nullptr)));
	outputs.push_back(fits);
	return outputs;
}

String resolve_constant(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String source = p_step.parameters.get("source", "literal");
	for (const ConstantSource &candidate : CONSTANT_SOURCES) {
		if (source == candidate.name) {
			Variant value;
			const String problem = candidate.read(p_step.parameters, value);
			p_step.values.push_back(value);
			return problem;
		}
	}
	return vformat("the source '%s' is not known. Use literal, type, math, global, class or singleton.", source);
}

String resolve_compose(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = resolve_type(p_step, "type");
	if (!problem.is_empty()) {
		return problem;
	}
	const Variant::Type type = Variant::Type(p_step.operation);
	const int count = GraphStepKinds::get_pin_count(*p_step.kind, p_step.parameters);
	for (int i = 0; i < Variant::get_constructor_count(type); i++) {
		if (Variant::get_constructor_argument_count(type, i) == count) {
			return String();
		}
	}
	return vformat("%s has no constructor with %d arguments.", Variant::get_type_name(type), count);
}

String resolve_decompose(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = resolve_type(p_step, "type");
	if (!problem.is_empty()) {
		return problem;
	}
	const Variant::Type type = Variant::Type(p_step.operation);
	List<StringName> members;
	Variant::get_member_list(type, &members);
	const int count = GraphStepKinds::get_pin_count(*p_step.kind, p_step.parameters);
	if (count > members.size()) {
		return vformat("%s has %d members and the step reads %d.", Variant::get_type_name(type), members.size(), count);
	}
	for (const StringName &member : members) {
		if (p_step.values.size() < count) {
			p_step.values.push_back(member);
		}
	}
	return String();
}

String resolve_convert(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	return resolve_type(p_step, "to");
}

String resolve_cast(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String class_name = p_step.parameters.get("class", "");
	if (!ClassRegistry::class_exists(class_name)) {
		return vformat("the class '%s' is not known.", class_name);
	}
	p_step.values.push_back(StringName(class_name));
	return String();
}

} // namespace GraphStepValues
