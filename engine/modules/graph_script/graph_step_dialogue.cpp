/**************************************************************************/
/*  graph_step_dialogue.cpp                                               */

#include "graph_step_dialogue.h"

#include "dialogue_box.h"
#include "graph_call.h"
#include "graph_step_latent.h"

#include "core/config/project_settings.h"
#include "core/io/resource_loader.h"
#include "scene/resources/prefab.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp.
constexpr int THEN = 0;
constexpr int CHOICE_TEXT = 0;
constexpr int CHOICE_VISIBLE = 1;
constexpr int SPEAKER = 1;
constexpr int TEXT = 2;
constexpr int PREFAB = 3;
constexpr int FIRST_CHOICE_INPUT = 4;
constexpr int FIRST_CHOICE_OUTPUT = 1;
constexpr int CONTINUE = -1;

// What a message offers: the text of each choice, and the pin each one came from.
struct Offer {
	PackedStringArray texts;
	Array pins;
};

String text_of(const Variant &p_value) {
	return p_value.get_type() == Variant::NIL ? String() : String(p_value);
}

// The prefab input, else the prefab the project setting names, else a DialogueBox. Null after the
// call has failed.
Entity *make_box(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Ref<Prefab> prefab = p_call.input(p_step, PREFAB);
	const String path = GLOBAL_GET(GraphStepDialogue::PREFAB_SETTING);
	if (prefab.is_null() && !path.is_empty()) {
		const Ref<Resource> loaded = ResourceLoader::exists(path) ? ResourceLoader::load(path) : Ref<Resource>();
		prefab = loaded;
		if (prefab.is_null()) {
			p_call.fail(p_step, vformat("cannot load the prefab '%s' that %s names.", path, GraphStepDialogue::PREFAB_SETTING));
			return nullptr;
		}
	}
	if (prefab.is_null()) {
		return memnew(DialogueBox);
	}
	if (!prefab->can_instantiate()) {
		p_call.fail(p_step, "needs a prefab that can be instantiated.");
		return nullptr;
	}
	return prefab->instantiate();
}

// A choice with no wire, or one that says it is not visible, is not offered.
bool is_offered(const Variant &p_choice) {
	return p_choice.get_type() == Variant::DICTIONARY && bool(Dictionary(p_choice).get("visible", true));
}

Offer read_offer(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Offer offer;
	const int count = GraphStepKinds::get_pin_count(*p_step.kind, p_step.parameters);
	for (int pin = 0; pin < count; pin++) {
		const Variant choice = p_call.input(p_step, FIRST_CHOICE_INPUT + pin);
		if (is_offered(choice)) {
			offer.texts.push_back(text_of(Dictionary(choice).get("text", Variant())));
			offer.pins.push_back(pin);
		}
	}
	return offer;
}

// The box answers with the number of the choice it showed. The frame keeps the pin of each
// choice on offer, and the box, so the step can take it off the stage.
int resume_message(GraphCall &p_call, GraphPending &p_frame) {
	p_frame.done = true;
	Entity *box = Object::cast_to<Entity>(ObjectRegistry::get_instance(ObjectID(uint64_t(p_frame.position))));
	if (box && !box->is_queued_for_deletion()) {
		box->queue_destroy();
	}
	const Array heard = p_frame.container;
	const Array pins = p_frame.cursor;
	const int index = heard.is_empty() ? CONTINUE : int(heard[0]);
	return index >= 0 && index < pins.size() ? FIRST_CHOICE_OUTPUT + int(pins[index]) : THEN;
}

} // namespace

namespace GraphStepDialogue {

Variant eval_choice(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant visible = p_call.input(p_step, CHOICE_VISIBLE);
	Dictionary choice;
	choice["text"] = p_call.input(p_step, CHOICE_TEXT);
	choice["visible"] = visible.get_type() == Variant::NIL || bool(visible);
	return choice;
}

int run_message(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Stage *stage = Stage::get_singleton();
	if (!stage) {
		p_call.fail(p_step, "needs a stage to show the box on.");
		return GraphFlow::END;
	}
	Entity *box = make_box(p_call, p_step);
	if (!box) {
		return GraphFlow::END;
	}
	if (!box->has_method("show_message") || !box->has_signal("finished")) {
		memdelete(box);
		p_call.fail(p_step, "the box needs a show_message method and a finished signal.");
		return GraphFlow::END;
	}
	const Offer offer = read_offer(p_call, p_step);
	stage->get_root()->add_child(box);
	box->call("show_message", text_of(p_call.input(p_step, SPEAKER)), text_of(p_call.input(p_step, TEXT)), offer.texts);

	GraphPending frame = GraphStepLatent::make_frame(p_step, resume_message);
	frame.position = int64_t(box->get_instance_id());
	frame.cursor = offer.pins;
	// shortcut: the box stays on the stage when the owner of the script is freed mid-dialogue. Free
	// it from the owner's tree_exiting when a game needs that.
	return GraphStepLatent::suspend(p_call, frame, box, SNAME("finished"));
}

// One data input per choice pin, after the fixed ones. The choice outputs are the pins themselves.
String derive_message(const Dictionary &p_parameters, GraphDerivedPorts &r_ports) {
	const GraphStepKind *kind = GraphStepKinds::find(SNAME("dialogue_message"));
	ERR_FAIL_NULL_V(kind, "the step kind is missing.");
	const int count = GraphStepKinds::get_pin_count(*kind, p_parameters);
	for (int i = 0; i < count; i++) {
		const String name = vformat("choice%d", i);
		r_ports.inputs.push_back({ name, GraphPortSort::DATA, Variant::DICTIONARY, name, true });
	}
	return String();
}

} // namespace GraphStepDialogue
