/**************************************************************************/
/*  graph_step_scene.cpp                                                  */

#include "graph_step_scene.h"

#include "graph_call.h"

#include "core/config/engine.h"
#include "core/input/input.h"
#include "core/input/input_map.h"
#include "core/io/resource_loader.h"
#include "core/string/entity_path.h"
#include "core/string/print_string.h"
#include "scene/resources/prefab.h"
#include "scene/stage/entity.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp.
constexpr int THEN = 0;
constexpr int PATH = 0;
constexpr int PREFAB = 1;
constexpr int MADE = 1;
constexpr int MESSAGE = 1;

// Asks for a text parameter and says so when it is missing or empty.
String require_text(const GraphCompiledStep &p_step, const char *p_parameter) {
	const Variant text = p_step.parameters.get(p_parameter, Variant());
	return text.get_type() == Variant::NIL || String(text).is_empty() ? vformat("the step needs a '%s' parameter.", p_parameter) : String();
}

struct ActionState {
	const char *name;
	bool (Input::*probe)(const StringName &p_action, bool p_exact) const;
	// Reads the opposite of what the probe says.
	bool negate;
};

// A list of cases is data.
constexpr ActionState ACTION_STATES[] = {
	{ "pressed", &Input::is_action_pressed, false },
	{ "released", &Input::is_action_pressed, true },
	{ "just_pressed", &Input::is_action_just_pressed, false },
	{ "just_released", &Input::is_action_just_released, false },
};

} // namespace

namespace GraphStepScene {

Variant eval_self(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_call.get_owner();
}

Variant eval_stage(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return Stage::get_singleton();
}

Variant eval_singleton(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return Engine::get_singleton()->get_singleton_object(p_step.parameters["name"]);
}

Variant eval_autoload(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const String name = p_step.parameters["name"];
	Stage *stage = Stage::get_singleton();
	Entity *found = stage ? stage->get_root()->get_entity_or_null(EntityPath(name)) : nullptr;
	if (!found) {
		p_call.fail(p_step, vformat("there is no autoload named '%s' under the stage root.", name));
	}
	return found;
}

Variant eval_entity_at_path(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const String path = p_call.input(p_step, PATH);
	const Entity *owner = Object::cast_to<Entity>(p_call.get_owner());
	Entity *found = owner ? owner->get_entity_or_null(EntityPath(path)) : nullptr;
	if (!found) {
		p_call.fail(p_step, vformat("there is no entity at the path '%s'.", path));
	}
	return found;
}

Variant eval_input_action(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const StringName action = p_step.parameters["action"];
	if (!InputMap::get_singleton()->has_action(action)) {
		p_call.fail(p_step, vformat("there is no input action named '%s'.", action));
		return false;
	}
	const ActionState &state = ACTION_STATES[p_step.operation];
	return (Input::get_singleton()->*state.probe)(action, false) != state.negate;
}

int run_instantiate_prefab(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Ref<Prefab> prefab = p_call.input(p_step, PREFAB);
	if (prefab.is_null() || !prefab->can_instantiate()) {
		p_call.fail(p_step, "needs a prefab that can be instantiated.");
		return GraphFlow::END;
	}
	p_call.store(p_step, MADE, prefab->instantiate());
	return THEN;
}

int run_print(GraphCall &p_call, const GraphCompiledStep &p_step) {
	print_line(String(p_call.input(p_step, MESSAGE)));
	return THEN;
}

String resolve_singleton(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = require_text(p_step, "name");
	if (!problem.is_empty()) {
		return problem;
	}
	const String name = p_step.parameters["name"];
	return Engine::get_singleton()->has_singleton(name) ? String() : vformat("there is no singleton named '%s'.", name);
}

String resolve_autoload(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	return require_text(p_step, "name");
}

String resolve_preload(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = require_text(p_step, "path");
	if (!problem.is_empty()) {
		return problem;
	}
	const String path = p_step.parameters["path"];
	const Ref<Resource> resource = ResourceLoader::exists(path) ? ResourceLoader::load(path) : Ref<Resource>();
	if (resource.is_null()) {
		return vformat("cannot load the resource '%s'.", path);
	}
	p_step.values.push_back(resource);
	return String();
}

String resolve_resource_path(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = require_text(p_step, "path");
	if (problem.is_empty()) {
		p_step.values.push_back(p_step.parameters["path"]);
	}
	return problem;
}

String resolve_input_action(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = require_text(p_step, "action");
	if (!problem.is_empty()) {
		return problem;
	}
	const String state = p_step.parameters.get("state", "pressed");
	int index = 0;
	for (const ActionState &candidate : ACTION_STATES) {
		if (state == candidate.name) {
			p_step.operation = index;
			return String();
		}
		index++;
	}
	return vformat("the state '%s' is not known. Use pressed, released, just_pressed or just_released.", state);
}

} // namespace GraphStepScene
