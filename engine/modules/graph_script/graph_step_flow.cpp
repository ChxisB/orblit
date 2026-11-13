/**************************************************************************/
/*  graph_step_flow.cpp                                                   */

#include "graph_step_flow.h"

#include "graph_call.h"

#include "core/core_constants.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp.
constexpr int THEN = 0;
constexpr int ELSE = 1;
constexpr int VALUE = 0;
constexpr int CHANCE = 1;
constexpr int ITEMS = 0;
constexpr int CONDITION = 0;
constexpr int IF_TRUE = 1;
constexpr int IF_FALSE = 2;
constexpr int SWITCH_VALUE = 1;
constexpr int DEFAULT_CASE = 0;
constexpr int FIRST_CASE = 1;

// A case reader turns the parameter of one case into the value the value is compared with. It
// returns the problem, or an empty string.
using CaseReader = String (*)(const Variant &p_text, Variant &r_case);

String read_integer_case(const Variant &p_text, Variant &r_case) {
	if (p_text.get_type() != Variant::INT) {
		return "needs an integer.";
	}
	r_case = p_text;
	return String();
}

String read_string_case(const Variant &p_text, Variant &r_case) {
	if (p_text.get_type() != Variant::STRING && p_text.get_type() != Variant::STRING_NAME) {
		return "needs a string.";
	}
	r_case = String(p_text);
	return String();
}

String read_enum_case(const Variant &p_text, Variant &r_case) {
	const String name = p_text.get_type() == Variant::STRING ? String(p_text) : String();
	if (!CoreConstants::is_global_constant(name)) {
		return vformat("needs the name of a global constant, and '%s' is not one.", name);
	}
	r_case = CoreConstants::get_global_constant_value(CoreConstants::get_global_constant_index(name));
	return String();
}

String read_any_case(const Variant &p_text, Variant &r_case) {
	r_case = p_text;
	return String();
}

struct CaseType {
	const char *name;
	CaseReader read;
};

// A list of cases is data.
constexpr CaseType CASE_TYPES[] = {
	{ "integer", read_integer_case },
	{ "string", read_string_case },
	{ "enum", read_enum_case },
	{ "any", read_any_case },
};

const CaseType *find_case_type(const String &p_name) {
	for (const CaseType &type : CASE_TYPES) {
		if (p_name == type.name) {
			return &type;
		}
	}
	return nullptr;
}

} // namespace

namespace GraphStepFlow {

Variant eval_pick_random(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Array items = p_call.input(p_step, ITEMS);
	if (items.is_empty()) {
		p_call.fail(p_step, "there is nothing to pick from. The array is empty.");
		return Variant();
	}
	return items[p_call.get_random().randi_range(0, items.size() - 1)];
}

// Only the chosen side is read, so the other side runs nothing.
Variant eval_select(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_call.input(p_step, bool(p_call.input(p_step, CONDITION)) ? IF_TRUE : IF_FALSE);
}

Variant eval_reroute(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_call.input(p_step, VALUE);
}

int run_reroute(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return THEN;
}

int run_chance(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const double chance = p_call.input(p_step, CHANCE);
	// randf can give exactly 1, so the two ends are decided without it.
	const bool taken = chance >= 1.0 || (chance > 0.0 && p_call.get_random().randf() < chance);
	return taken ? THEN : ELSE;
}

int run_switch(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant value = p_call.input(p_step, SWITCH_VALUE);
	for (int i = 0; i < p_step.values.size(); i++) {
		Variant equal;
		bool valid = true;
		Variant::evaluate(Variant::OP_EQUAL, value, p_step.values[i], equal, valid);
		if (valid && bool(equal)) {
			return FIRST_CASE + i;
		}
	}
	return DEFAULT_CASE;
}

String resolve_switch(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String type_name = p_step.parameters.get("case_type", "integer");
	const CaseType *type = find_case_type(type_name);
	if (!type) {
		return vformat("the case type '%s' is not known. Use integer, string, enum or any.", type_name);
	}
	const int count = GraphStepKinds::get_pin_count(*p_step.kind, p_step.parameters);
	for (int i = 0; i < count; i++) {
		const String name = vformat("case%d", i);
		if (!p_step.parameters.has(name)) {
			return vformat("the step needs a '%s' parameter.", name);
		}
		Variant read;
		const String problem = type->read(p_step.parameters[name], read);
		if (!problem.is_empty()) {
			return vformat("the case '%s' %s", name, problem);
		}
		p_step.values.push_back(read);
	}
	return String();
}

} // namespace GraphStepFlow
