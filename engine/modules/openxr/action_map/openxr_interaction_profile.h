/**************************************************************************/
/*  openxr_interaction_profile.h                                          */

#pragma once

#include "openxr_action.h"
#include "openxr_binding_modifier.h"

#include "core/io/resource.h"

class OpenXRActionMap;

class OpenXRIPBinding : public Resource {
	ORBLIT_CLASS(OpenXRIPBinding, Resource);

private:
	Ref<OpenXRAction> action;
	String binding_path;
	Vector<Ref<OpenXRActionBindingModifier>> binding_modifiers;

protected:
	friend class OpenXRActionMap;

	OpenXRActionMap *action_map = nullptr;

	static void register_members();

public:
	static Ref<OpenXRIPBinding> new_binding(const Ref<OpenXRAction> &p_action, const String &p_binding_path); // Helper function for adding a new binding.

	OpenXRActionMap *get_action_map() { return action_map; } // Return the action map we're a part of.

	void set_action(const Ref<OpenXRAction> &p_action); // Set the action for this binding.
	Ref<OpenXRAction> get_action() const; // Get the action for this binding.

	void set_binding_path(const String &p_path);
	String get_binding_path() const;

	int get_binding_modifier_count() const; // Retrieve the number of binding modifiers in this profile path
	Ref<OpenXRActionBindingModifier> get_binding_modifier(int p_index) const;
	void clear_binding_modifiers(); // Remove all binding modifiers
	void set_binding_modifiers(const Array &p_bindings); // Set the binding modifiers (for loading from a resource)
	Array get_binding_modifiers() const; // Get the binding modifiers (for saving to a resource)

	void add_binding_modifier(const Ref<OpenXRActionBindingModifier> &p_binding_modifier); // Add a binding modifier object
	void remove_binding_modifier(const Ref<OpenXRActionBindingModifier> &p_binding_modifier); // Remove a binding modifier object

	// TODO add validation that we can display in the interface that checks if no two paths belong to the same top level path

	~OpenXRIPBinding();
};

class OpenXRInteractionProfile : public Resource {
	ORBLIT_CLASS(OpenXRInteractionProfile, Resource);

private:
	String interaction_profile_path;
	Array bindings;
	Vector<Ref<OpenXRIPBindingModifier>> binding_modifiers;

protected:
	friend class OpenXRActionMap;

	OpenXRActionMap *action_map = nullptr;

	static void register_members();

public:
	static Ref<OpenXRInteractionProfile> new_profile(const char *p_input_profile_path); // Helper function to create a new interaction profile

	OpenXRActionMap *get_action_map() { return action_map; }

	void set_interaction_profile_path(const String &p_input_profile_path); // Set our input profile path
	String get_interaction_profile_path() const; // get our input profile path

	int get_binding_count() const; // Retrieve the number of bindings in this profile path
	Ref<OpenXRIPBinding> get_binding(int p_index) const;
	void set_bindings(const Array &p_bindings); // Set the bindings (for loading from a resource)
	Array get_bindings() const; // Get the bindings (for saving to a resource)

	Ref<OpenXRIPBinding> find_binding(const Ref<OpenXRAction> &p_action, const String &p_binding_path) const; // Get our binding record
	Vector<Ref<OpenXRIPBinding>> get_bindings_for_action(const Ref<OpenXRAction> &p_action) const; // Get our binding record for a given action
	void add_binding(const Ref<OpenXRIPBinding> &p_binding); // Add a binding object
	void remove_binding(const Ref<OpenXRIPBinding> &p_binding); // Remove a binding object

	void add_new_binding(const Ref<OpenXRAction> &p_action, const String &p_paths); // Create a new binding for this profile
	void remove_binding_for_action(const Ref<OpenXRAction> &p_action); // Remove all bindings for this action
	bool has_binding_for_action(const Ref<OpenXRAction> &p_action); // Returns true if we have a binding for this action

	int get_binding_modifier_count() const; // Retrieve the number of binding modifiers in this profile path
	Ref<OpenXRIPBindingModifier> get_binding_modifier(int p_index) const;
	void clear_binding_modifiers(); // Remove all binding modifiers
	void set_binding_modifiers(const Array &p_bindings); // Set the binding modifiers (for loading from a resource)
	Array get_binding_modifiers() const; // Get the binding modifiers (for saving to a resource)

	void add_binding_modifier(const Ref<OpenXRIPBindingModifier> &p_binding_modifier); // Add a binding modifier object
	void remove_binding_modifier(const Ref<OpenXRIPBindingModifier> &p_binding_modifier); // Remove a binding modifier object

	~OpenXRInteractionProfile();
};
