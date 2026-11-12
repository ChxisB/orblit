/**************************************************************************/
/*  graph_step_kinds.cpp                                                  */

#include "graph_step_kinds.h"

#include "graph_step_run.h"

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

// Columns: name, category, inputs, outputs, eval, run, resolve, loop.
constexpr GraphStepKind KINDS[] = {
	{ "entry", "Events", NO_PORTS, list(THEN_OUT), nullptr, nullptr, nullptr, false },
	{ "event", "Events", NO_PORTS, list(THEN_OUT), nullptr, nullptr, nullptr, false },
	{ "return", "Flow", list(RETURN_IN), NO_PORTS, nullptr, GraphStepRun::run_return, nullptr, false },
	{ "branch", "Flow", list(CONDITION_IN), list(BRANCH_OUT), nullptr, GraphStepRun::run_branch, nullptr, false },
	{ "while", "Flow", list(CONDITION_IN), list(WHILE_OUT), nullptr, GraphStepRun::run_while, nullptr, true },
	{ "count_loop", "Flow", list(COUNT_IN), list(COUNT_OUT), nullptr, GraphStepRun::run_count_loop, nullptr, true },
	{ "each", "Flow", list(EACH_IN), list(EACH_OUT), nullptr, GraphStepRun::run_each, nullptr, true },
	{ "sequence", "Flow", list(RUN_IN), list(SEQUENCE_OUT), nullptr, GraphStepRun::run_sequence, nullptr, false },
	{ "break", "Flow", list(RUN_IN), NO_PORTS, nullptr, GraphStepRun::run_break, nullptr, false },
	{ "get", "Variables", NO_PORTS, list(VALUE_OUT), GraphStepRun::eval_variable, nullptr, GraphStepRun::resolve_variable, false },
	{ "set", "Variables", list(SET_IN), list(THEN_OUT), nullptr, GraphStepRun::run_set_variable, GraphStepRun::resolve_variable, false },
	{ "local", "Variables", NO_PORTS, list(VALUE_OUT), GraphStepRun::eval_local, nullptr, GraphStepRun::resolve_local, false },
	{ "set_local", "Variables", list(SET_IN), list(THEN_OUT), nullptr, GraphStepRun::run_set_local, GraphStepRun::resolve_local, false },
	{ "constant", "Values", NO_PORTS, list(VALUE_OUT), GraphStepRun::eval_constant, nullptr, GraphStepRun::resolve_constant, false },
	{ "operator", "Operators", list(OPERATOR_IN), list(RESULT_OUT), GraphStepRun::eval_operator, nullptr, GraphStepRun::resolve_operator, false },
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
