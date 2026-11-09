/**************************************************************************/
/*  openxr_action_set.cpp                                                 */

#include "openxr_action_set.h"

#include "core/object/class_registry.h"

void OpenXRActionSet::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_localized_name", "localized_name"), &OpenXRActionSet::set_localized_name);
	ClassRegistry::bind_method(D_METHOD("get_localized_name"), &OpenXRActionSet::get_localized_name);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "localized_name"), "set_localized_name", "get_localized_name");

	ClassRegistry::bind_method(D_METHOD("set_priority", "priority"), &OpenXRActionSet::set_priority);
	ClassRegistry::bind_method(D_METHOD("get_priority"), &OpenXRActionSet::get_priority);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "priority"), "set_priority", "get_priority");

	ClassRegistry::bind_method(D_METHOD("get_action_count"), &OpenXRActionSet::get_action_count);
	ClassRegistry::bind_method(D_METHOD("set_actions", "actions"), &OpenXRActionSet::set_actions);
	ClassRegistry::bind_method(D_METHOD("get_actions"), &OpenXRActionSet::get_actions);
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "actions", PROPERTY_HINT_RESOURCE_TYPE, OpenXRAction::get_static_class_name(), PROPERTY_USAGE_NO_EDITOR), "set_actions", "get_actions");

	ClassRegistry::bind_method(D_METHOD("add_action", "action"), &OpenXRActionSet::add_action);
	ClassRegistry::bind_method(D_METHOD("remove_action", "action"), &OpenXRActionSet::remove_action);
}

Ref<OpenXRActionSet> OpenXRActionSet::new_action_set(const char *p_name, const char *p_localized_name, const int p_priority) {
	// This is a helper function to help build our default action sets

	Ref<OpenXRActionSet> action_set;
	action_set.instantiate();
	action_set->set_name(String(p_name));
	action_set->set_localized_name(p_localized_name);
	action_set->set_priority(p_priority);

	return action_set;
}

void OpenXRActionSet::set_localized_name(const String &p_localized_name) {
	localized_name = p_localized_name;
	emit_changed();
}

String OpenXRActionSet::get_localized_name() const {
	return localized_name;
}

void OpenXRActionSet::set_priority(const int p_priority) {
	priority = p_priority;
	emit_changed();
}

int OpenXRActionSet::get_priority() const {
	return priority;
}

int OpenXRActionSet::get_action_count() const {
	return actions.size();
}

void OpenXRActionSet::clear_actions() {
	// Actions held within our action set should be released and destroyed but just in case they are still used some where else
	if (actions.is_empty()) {
		return;
	}

	for (int i = 0; i < actions.size(); i++) {
		Ref<OpenXRAction> action = actions[i];
		action->action_set = nullptr;
	}
	actions.clear();
	emit_changed();
}

void OpenXRActionSet::set_actions(const Array &p_actions) {
	// Any actions not retained in p_actions should be freed automatically, those held within our Array will have be relinked to our action set.
	clear_actions();

	for (int i = 0; i < p_actions.size(); i++) {
		// add them anew so we verify our action_set pointer
		add_action(p_actions[i]);
	}
}

Array OpenXRActionSet::get_actions() const {
	return actions;
}

Ref<OpenXRAction> OpenXRActionSet::get_action(const String &p_name) const {
	for (int i = 0; i < actions.size(); i++) {
		Ref<OpenXRAction> action = actions[i];
		if (action->get_name() == p_name) {
			return action;
		}
	}

	return Ref<OpenXRAction>();
}

void OpenXRActionSet::add_action(const Ref<OpenXRAction> &p_action) {
	ERR_FAIL_COND(p_action.is_null());

	if (!actions.has(p_action)) {
		if (p_action->action_set && p_action->action_set != this) {
			// action should only relate to our action set
			p_action->action_set->remove_action(p_action);
		}

		p_action->action_set = this;
		actions.push_back(p_action);
		emit_changed();
	}
}

void OpenXRActionSet::remove_action(const Ref<OpenXRAction> &p_action) {
	int idx = actions.find(p_action);
	if (idx != -1) {
		actions.remove_at(idx);

		ERR_FAIL_COND_MSG(p_action->action_set != this, "Removing action that belongs to this action set but had incorrect action set pointer."); // This should never happen!
		p_action->action_set = nullptr;

		emit_changed();
	}
}

Ref<OpenXRAction> OpenXRActionSet::add_new_action(const char *p_name, const char *p_localized_name, const OpenXRAction::ActionType p_action_type, const char *p_toplevel_paths) {
	// This is a helper function to help build our default action sets

	Ref<OpenXRAction> new_action = OpenXRAction::new_action(p_name, p_localized_name, p_action_type, p_toplevel_paths);
	add_action(new_action);
	return new_action;
}

OpenXRActionSet::~OpenXRActionSet() {
	clear_actions();
}
