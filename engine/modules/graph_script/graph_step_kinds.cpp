/**************************************************************************/
/*  graph_step_kinds.cpp                                                  */

#include "graph_step_kinds.h"

namespace {

constexpr GraphPortSpec flow(const char *p_name) {
	return { p_name, GraphPortSort::FLOW, Variant::NIL };
}

constexpr GraphPortSpec data(const char *p_name, Variant::Type p_type = Variant::NIL) {
	return { p_name, GraphPortSort::DATA, p_type };
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
constexpr GraphPortSpec SEQUENCE_OUT[] = { flow("first"), flow("second") };
constexpr GraphPortSpec VALUE_OUT[] = { data("value") };
constexpr GraphPortSpec SET_IN[] = { flow("run"), data("value") };
constexpr GraphPortSpec OPERATOR_IN[] = { data("a"), data("b") };
constexpr GraphPortSpec RESULT_OUT[] = { data("result") };

constexpr GraphStepKind KINDS[] = {
	{ "entry", "Events", NO_PORTS, list(THEN_OUT) },
	{ "event", "Events", NO_PORTS, list(THEN_OUT) },
	{ "return", "Flow", list(RETURN_IN), NO_PORTS },
	{ "branch", "Flow", list(CONDITION_IN), list(BRANCH_OUT) },
	{ "while", "Flow", list(CONDITION_IN), list(WHILE_OUT) },
	{ "count_loop", "Flow", list(COUNT_IN), list(COUNT_OUT) },
	{ "each", "Flow", list(EACH_IN), list(EACH_OUT) },
	{ "sequence", "Flow", list(RUN_IN), list(SEQUENCE_OUT) },
	{ "break", "Flow", list(RUN_IN), NO_PORTS },
	{ "get", "Variables", NO_PORTS, list(VALUE_OUT) },
	{ "set", "Variables", list(SET_IN), list(THEN_OUT) },
	{ "local", "Variables", NO_PORTS, list(VALUE_OUT) },
	{ "set_local", "Variables", list(SET_IN), list(THEN_OUT) },
	{ "constant", "Values", NO_PORTS, list(VALUE_OUT) },
	{ "operator", "Operators", list(OPERATOR_IN), list(RESULT_OUT) },
};

} // namespace

const GraphStepKind *GraphStepKinds::find(const StringName &p_name) {
	for (const GraphStepKind &kind : KINDS) {
		if (p_name == kind.name) {
			return &kind;
		}
	}
	return nullptr;
}
