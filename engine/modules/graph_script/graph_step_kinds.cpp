/**************************************************************************/
/*  graph_step_kinds.cpp                                                  */

#include "graph_step_kinds.h"

#include "graph_signature.h"
#include "graph_step_flow.h"
#include "graph_step_latent.h"
#include "graph_step_reflect.h"
#include "graph_step_run.h"
#include "graph_step_scene.h"
#include "graph_step_values.h"

namespace {

constexpr GraphPortSpec flow(const char *p_name) {
	return { p_name, GraphPortSort::FLOW, Variant::NIL };
}

constexpr GraphPortSpec data(const char *p_name, Variant::Type p_type = Variant::NIL) {
	return { p_name, GraphPortSort::DATA, p_type };
}

constexpr GraphPortSpec optional_object(const char *p_name) {
	return { p_name, GraphPortSort::DATA, Variant::OBJECT, true };
}

template <int N>
constexpr GraphPortList list(const GraphPortSpec (&p_ports)[N]) {
	return { p_ports, N };
}

constexpr GraphPortList NO_PORTS = { nullptr, 0 };

constexpr GraphPortSpec RUN_IN[] = { flow("run") };
constexpr GraphPortSpec THEN_OUT[] = { flow("then") };
constexpr GraphPortSpec RETURN_IN[] = { flow("run"), data("value") };
constexpr GraphPortSpec CONDITION_IN[] = { flow("run"), data("condition", Variant::BOOL) };
constexpr GraphPortSpec BRANCH_OUT[] = { flow("then"), flow("else") };
constexpr GraphPortSpec WHILE_OUT[] = { flow("body"), flow("done") };
constexpr GraphPortSpec COUNT_IN[] = { flow("run"), data("from", Variant::INT), data("to", Variant::INT) };
constexpr GraphPortSpec COUNT_OUT[] = { flow("body"), flow("done"), data("index", Variant::INT) };
constexpr GraphPortSpec EACH_IN[] = { flow("run"), data("items") };
constexpr GraphPortSpec EACH_OUT[] = { flow("body"), flow("done"), data("item"), data("index", Variant::INT) };
constexpr GraphPortSpec VALUE_OUT[] = { data("value") };
constexpr GraphPortSpec SET_IN[] = { flow("run"), data("value") };
constexpr GraphPortSpec OPERATOR_IN[] = { data("a"), data("b") };
constexpr GraphPortSpec RESULT_OUT[] = { data("result") };
constexpr GraphPortSpec CHANCE_IN[] = { flow("run"), data("chance", Variant::FLOAT) };
constexpr GraphPortSpec PICK_IN[] = { data("items", Variant::ARRAY) };
constexpr GraphPortSpec SELECT_IN[] = { data("condition", Variant::BOOL), data("a"), data("b") };
constexpr GraphPortSpec SWITCH_IN[] = { flow("run"), data("value") };
constexpr GraphPortSpec DEFAULT_OUT[] = { flow("default") };
constexpr GraphPortSpec CONVERT_IN[] = { data("value") };
constexpr GraphPortSpec ARRAY_OUT[] = { data("array", Variant::ARRAY) };
constexpr GraphPortSpec DICTIONARY_OUT[] = { data("dictionary", Variant::DICTIONARY) };
constexpr GraphPortSpec CAST_OUT[] = { data("object", Variant::OBJECT), data("valid", Variant::BOOL) };
constexpr GraphPortSpec OBJECT_OUT[] = { data("object", Variant::OBJECT) };
constexpr GraphPortSpec PATH_OUT[] = { data("path", Variant::STRING) };
constexpr GraphPortSpec PATH_IN[] = { data("path", Variant::STRING) };
constexpr GraphPortSpec BOOL_OUT[] = { data("value", Variant::BOOL) };
constexpr GraphPortSpec INSTANTIATE_IN[] = { flow("run"), data("prefab", Variant::OBJECT) };
constexpr GraphPortSpec INSTANTIATE_OUT[] = { flow("then"), data("entity", Variant::OBJECT) };
constexpr GraphPortSpec PRINT_IN[] = { flow("run"), data("text") };
// A call with no target wired runs on the owner of the script.
constexpr GraphPortSpec TARGET_IN[] = { optional_object("target") };
constexpr GraphPortSpec TARGET_RUN_IN[] = { flow("run"), optional_object("target") };
constexpr GraphPortSpec FREE_IN[] = { flow("run"), data("object", Variant::OBJECT) };
constexpr GraphPortSpec CALL_FUNCTION_OUT[] = { flow("then"), data("result") };
constexpr GraphPortSpec WAIT_IN[] = { flow("run"), data("seconds", Variant::FLOAT) };
constexpr GraphPortSpec AWAIT_OUT[] = { flow("then"), data("arguments", Variant::ARRAY) };

constexpr GraphPinGrowth THEN_PINS = { GraphPinSide::OUTPUT, "then", nullptr, GraphPortSort::FLOW, Variant::NIL, 2 };
constexpr GraphPinGrowth CASE_PINS = { GraphPinSide::OUTPUT, "case", nullptr, GraphPortSort::FLOW, Variant::NIL, 1 };
constexpr GraphPinGrowth ITEM_PINS = { GraphPinSide::INPUT, "item", nullptr, GraphPortSort::DATA, Variant::NIL, 0 };
constexpr GraphPinGrowth ENTRY_PINS = { GraphPinSide::INPUT, "key", "value", GraphPortSort::DATA, Variant::NIL, 0 };
constexpr GraphPinGrowth ARGUMENT_PINS = { GraphPinSide::INPUT, "argument", nullptr, GraphPortSort::DATA, Variant::NIL, 0 };
constexpr GraphPinGrowth PART_PINS = { GraphPinSide::OUTPUT, "part", nullptr, GraphPortSort::DATA, Variant::NIL, 1 };
// The arguments a vararg method takes past its named ones.
constexpr GraphPinGrowth EXTRA_PINS = { GraphPinSide::INPUT, "extra", nullptr, GraphPortSort::DATA, Variant::NIL, 0 };

// Columns: name, category, inputs, outputs, eval, run, resolve, loop, growth, derive.
constexpr GraphStepKind KINDS[] = {
	{ "entry", "Events", NO_PORTS, list(THEN_OUT), nullptr, nullptr, nullptr, false, {}, GraphSignatures::derive_start },
	{ "event", "Events", NO_PORTS, list(THEN_OUT), nullptr, nullptr, nullptr, false, {}, GraphSignatures::derive_start },
	{ "signal_received", "Events", NO_PORTS, list(THEN_OUT), nullptr, nullptr, nullptr, false, {}, GraphSignatures::derive_start },
	{ "return", "Flow", list(RETURN_IN), NO_PORTS, nullptr, GraphStepRun::run_return, nullptr, false },
	{ "branch", "Flow", list(CONDITION_IN), list(BRANCH_OUT), nullptr, GraphStepRun::run_branch, nullptr, false },
	{ "while", "Flow", list(CONDITION_IN), list(WHILE_OUT), nullptr, GraphStepRun::run_while, nullptr, true },
	{ "count_loop", "Flow", list(COUNT_IN), list(COUNT_OUT), nullptr, GraphStepRun::run_count_loop, nullptr, true },
	{ "each", "Flow", list(EACH_IN), list(EACH_OUT), nullptr, GraphStepRun::run_each, nullptr, true },
	{ "sequence", "Flow", list(RUN_IN), NO_PORTS, nullptr, GraphStepRun::run_sequence, nullptr, false, THEN_PINS },
	{ "chance", "Flow", list(CHANCE_IN), list(BRANCH_OUT), nullptr, GraphStepFlow::run_chance, nullptr, false },
	{ "pick_random", "Flow", list(PICK_IN), list(VALUE_OUT), GraphStepFlow::eval_pick_random, nullptr, nullptr, false },
	{ "select", "Flow", list(SELECT_IN), list(VALUE_OUT), GraphStepFlow::eval_select, nullptr, nullptr, false },
	{ "reroute", "Values", list(CONVERT_IN), list(VALUE_OUT), GraphStepFlow::eval_reroute, nullptr, nullptr, false },
	{ "reroute_flow", "Flow", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepFlow::run_reroute, nullptr, false },
	{ "switch", "Flow", list(SWITCH_IN), list(DEFAULT_OUT), nullptr, GraphStepFlow::run_switch, GraphStepFlow::resolve_switch, false, CASE_PINS },
	{ "break", "Flow", list(RUN_IN), NO_PORTS, nullptr, GraphStepRun::run_break, nullptr, false },
	{ "wait", "Flow", list(WAIT_IN), list(THEN_OUT), nullptr, GraphStepLatent::run_wait, nullptr, false },
	{ "await_signal", "Flow", list(TARGET_RUN_IN), list(AWAIT_OUT), nullptr, GraphStepLatent::run_await_signal, GraphStepLatent::resolve_await_signal, false },
	{ "get", "Variables", NO_PORTS, list(VALUE_OUT), GraphStepRun::eval_variable, nullptr, GraphStepRun::resolve_variable, false },
	{ "set", "Variables", list(SET_IN), list(THEN_OUT), nullptr, GraphStepRun::run_set_variable, GraphStepRun::resolve_variable, false },
	{ "local", "Variables", NO_PORTS, list(VALUE_OUT), GraphStepRun::eval_local, nullptr, GraphStepRun::resolve_local, false },
	{ "set_local", "Variables", list(SET_IN), list(THEN_OUT), nullptr, GraphStepRun::run_set_local, GraphStepRun::resolve_local, false },
	{ "constant", "Values", NO_PORTS, list(VALUE_OUT), GraphStepValues::eval_stored, nullptr, GraphStepValues::resolve_constant, false },
	{ "make_array", "Values", NO_PORTS, list(ARRAY_OUT), GraphStepValues::eval_make_array, nullptr, nullptr, false, ITEM_PINS },
	{ "make_dictionary", "Values", NO_PORTS, list(DICTIONARY_OUT), GraphStepValues::eval_make_dictionary, nullptr, nullptr, false, ENTRY_PINS },
	{ "compose", "Values", NO_PORTS, list(VALUE_OUT), GraphStepValues::eval_compose, nullptr, GraphStepValues::resolve_compose, false, ARGUMENT_PINS },
	{ "decompose", "Values", list(CONVERT_IN), NO_PORTS, GraphStepValues::eval_decompose, nullptr, GraphStepValues::resolve_decompose, false, PART_PINS },
	{ "convert", "Values", list(CONVERT_IN), list(VALUE_OUT), GraphStepValues::eval_convert, nullptr, GraphStepValues::resolve_convert, false },
	{ "cast", "Values", list(CONVERT_IN), list(CAST_OUT), GraphStepValues::eval_cast, nullptr, GraphStepValues::resolve_cast, false },
	{ "self", "Scene", NO_PORTS, list(OBJECT_OUT), GraphStepScene::eval_self, nullptr, nullptr, false },
	{ "stage", "Scene", NO_PORTS, list(OBJECT_OUT), GraphStepScene::eval_stage, nullptr, nullptr, false },
	{ "singleton", "Scene", NO_PORTS, list(OBJECT_OUT), GraphStepScene::eval_singleton, nullptr, GraphStepScene::resolve_singleton, false },
	{ "autoload", "Scene", NO_PORTS, list(OBJECT_OUT), GraphStepScene::eval_autoload, nullptr, GraphStepScene::resolve_autoload, false },
	{ "entity_at_path", "Scene", list(PATH_IN), list(OBJECT_OUT), GraphStepScene::eval_entity_at_path, nullptr, nullptr, false },
	{ "instantiate_prefab", "Scene", list(INSTANTIATE_IN), list(INSTANTIATE_OUT), nullptr, GraphStepScene::run_instantiate_prefab, nullptr, false },
	{ "preload", "Scene", NO_PORTS, list(OBJECT_OUT), GraphStepValues::eval_stored, nullptr, GraphStepScene::resolve_preload, false },
	{ "resource_path", "Scene", NO_PORTS, list(PATH_OUT), GraphStepValues::eval_stored, nullptr, GraphStepScene::resolve_resource_path, false },
	{ "input_action", "Input", NO_PORTS, list(BOOL_OUT), GraphStepScene::eval_input_action, nullptr, GraphStepScene::resolve_input_action, false },
	{ "print", "Debug", list(PRINT_IN), list(THEN_OUT), nullptr, GraphStepScene::run_print, nullptr, false },
	{ "operator", "Operators", list(OPERATOR_IN), list(RESULT_OUT), GraphStepRun::eval_operator, nullptr, GraphStepRun::resolve_operator, false },
	{ "call_method", "Calls", list(TARGET_RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_call_method, GraphStepReflect::resolve_method, false, EXTRA_PINS, GraphStepReflect::derive_method },
	{ "query_method", "Calls", list(TARGET_IN), NO_PORTS, GraphStepReflect::eval_call_method, nullptr, GraphStepReflect::resolve_method, false, EXTRA_PINS, GraphStepReflect::derive_method },
	{ "call_static", "Calls", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_call_static, GraphStepReflect::resolve_static, false, EXTRA_PINS, GraphStepReflect::derive_static },
	{ "call_builtin", "Calls", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_call_builtin, GraphStepReflect::resolve_builtin, false, EXTRA_PINS, GraphStepReflect::derive_builtin },
	{ "query_builtin", "Calls", NO_PORTS, NO_PORTS, GraphStepReflect::eval_call_builtin, nullptr, GraphStepReflect::resolve_builtin, false, EXTRA_PINS, GraphStepReflect::derive_builtin },
	{ "call_utility", "Calls", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_call_utility, GraphStepReflect::resolve_utility, false, EXTRA_PINS, GraphStepReflect::derive_utility },
	{ "query_utility", "Calls", NO_PORTS, NO_PORTS, GraphStepReflect::eval_call_utility, nullptr, GraphStepReflect::resolve_utility, false, EXTRA_PINS, GraphStepReflect::derive_utility },
	{ "call_function", "Calls", list(TARGET_RUN_IN), list(CALL_FUNCTION_OUT), nullptr, GraphStepReflect::run_call_function, GraphStepReflect::resolve_call_function, false, ARGUMENT_PINS },
	{ "get_property", "Properties", list(TARGET_IN), NO_PORTS, GraphStepReflect::eval_get_property, nullptr, nullptr, false, {}, GraphStepReflect::derive_get_property },
	{ "set_property", "Properties", list(TARGET_RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_set_property, nullptr, false, {}, GraphStepReflect::derive_set_property },
	{ "emit", "Signals", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_emit, GraphStepReflect::resolve_emit, false, ARGUMENT_PINS },
	{ "emit_on", "Signals", list(TARGET_RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_emit_on, nullptr, false, {}, GraphStepReflect::derive_emit_on },
	{ "connect", "Signals", list(TARGET_RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_connect, GraphStepReflect::resolve_connect, false },
	{ "new", "Objects", list(RUN_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_new, nullptr, false, {}, GraphStepReflect::derive_new },
	{ "free", "Objects", list(FREE_IN), list(THEN_OUT), nullptr, GraphStepReflect::run_free, nullptr, false },
};

} // namespace

namespace {

const GraphPortList &fixed_ports(const GraphStepKind &p_kind, GraphPinSide p_side) {
	return p_side == GraphPinSide::INPUT ? p_kind.inputs : p_kind.outputs;
}

int ports_per_pin(const GraphStepKind &p_kind) {
	return p_kind.growth.second_name ? 2 : 1;
}

// A step with a problem has no derived ports, so a half-edited step still has its fixed ones.
// shortcut: derive runs on every port query, cache it on the step when the editor profile shows it.
GraphDerivedPorts derived_ports(const GraphStepKind &p_kind, const Dictionary &p_parameters) {
	GraphDerivedPorts ports;
	if (p_kind.derive && !p_kind.derive(p_parameters, ports).is_empty()) {
		return GraphDerivedPorts();
	}
	return ports;
}

const Vector<GraphPort> &derived_side(const GraphDerivedPorts &p_ports, GraphPinSide p_side) {
	return p_side == GraphPinSide::INPUT ? p_ports.inputs : p_ports.outputs;
}

} // namespace

const GraphStepKind *GraphStepKinds::find(const StringName &p_name) {
	for (const GraphStepKind &kind : KINDS) {
		if (p_name == kind.name) {
			return &kind;
		}
	}
	return nullptr;
}

int GraphStepKinds::get_kind_count() {
	return sizeof(KINDS) / sizeof(KINDS[0]);
}

const GraphStepKind &GraphStepKinds::get_kind(int p_index) {
	return KINDS[p_index];
}

int GraphStepKinds::get_pin_count(const GraphStepKind &p_kind, const Dictionary &p_parameters) {
	if (p_kind.growth.side == GraphPinSide::NONE) {
		return 0;
	}
	const Variant pins = p_parameters.get("pins", Variant());
	const int count = pins.get_type() == Variant::INT ? int(pins) : p_kind.growth.minimum;
	return CLAMP(count, p_kind.growth.minimum, MAX_PINS);
}

String GraphStepKinds::get_ports_problem(const GraphStepKind &p_kind, const Dictionary &p_parameters) {
	GraphDerivedPorts ports;
	return p_kind.derive ? p_kind.derive(p_parameters, ports) : String();
}

int GraphStepKinds::get_port_count(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side) {
	const int fixed = fixed_ports(p_kind, p_side).count + derived_side(derived_ports(p_kind, p_parameters), p_side).size();
	return p_kind.growth.side == p_side ? fixed + get_pin_count(p_kind, p_parameters) * ports_per_pin(p_kind) : fixed;
}

GraphPort GraphStepKinds::get_port(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side, int p_index) {
	const GraphPortList &fixed = fixed_ports(p_kind, p_side);
	if (p_index < fixed.count) {
		const GraphPortSpec &spec = fixed.items[p_index];
		return { spec.name, spec.sort, spec.type, spec.name, spec.optional };
	}
	const Vector<GraphPort> derived = derived_side(derived_ports(p_kind, p_parameters), p_side);
	if (p_index - fixed.count < derived.size()) {
		return derived[p_index - fixed.count];
	}
	const int grown = p_index - fixed.count - derived.size();
	const int stride = ports_per_pin(p_kind);
	const char *name = grown % stride == 1 ? p_kind.growth.second_name : p_kind.growth.name;
	const String port_name = vformat("%s%d", name, grown / stride);
	return { port_name, p_kind.growth.sort, p_kind.growth.type, port_name };
}
