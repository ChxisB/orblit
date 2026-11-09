/**************************************************************************/
/*  openxr_action_map.h                                                   */

#pragma once

#include "openxr_action.h"
#include "openxr_action_set.h"
#include "openxr_interaction_profile.h"

#include "core/io/resource.h"

class OpenXRActionMap : public Resource {
	ORBLIT_CLASS(OpenXRActionMap, Resource);

private:
	Array action_sets;
	Array interaction_profiles;

protected:
	static void register_members();

public:
	void set_action_sets(const Array &p_action_sets); // Set our actions sets by providing an array with action sets (for loading from resource)
	Array get_action_sets() const; // Get our action sets as an array (for saving to resource)

	int get_action_set_count() const; // Retrieve the number of action sets we have
	Ref<OpenXRActionSet> find_action_set(const String &p_name) const; // Find an action set by name
	Ref<OpenXRActionSet> get_action_set(int p_idx) const; // Retrieve an action set by index
	void add_action_set(const Ref<OpenXRActionSet> &p_action_set); // Add an action set to our action map
	void remove_action_set(const Ref<OpenXRActionSet> &p_action_set); // Remove an action set from our action map

	void clear_interaction_profiles(); // Remove all our interaction profiles
	void set_interaction_profiles(const Array &p_interaction_profiles); // Set our interaction profiles by providing an array (for loading from resource)
	Array get_interaction_profiles() const; // Get our interaction profiles as an array (for saving to resource)

	int get_interaction_profile_count() const; // Retrieve the number of interaction profiles we have
	Ref<OpenXRInteractionProfile> find_interaction_profile(const String &p_path) const; // Find an interaction profile by path
	Ref<OpenXRInteractionProfile> get_interaction_profile(int p_idx) const; // Retrieve an interaction profile by index
	void add_interaction_profile(const Ref<OpenXRInteractionProfile> &p_interaction_profile); // Add an interaction profile to our action map
	void remove_interaction_profile(const Ref<OpenXRInteractionProfile> &p_interaction_profile); // remove an interaction profile from our action map

	void create_default_action_sets(); // Create our default action set for runtime
	void create_editor_action_sets(); // Create our action set for the editor

	// Helper functions for editor
	Ref<OpenXRAction> get_action(const String &p_path) const; // Retrieve an action using <action name>/<action> as our parameter
	void remove_action(const String &p_path, bool p_remove_interaction_profiles = false); // Remove action from action set, also removes it from interaction profiles
	PackedStringArray get_top_level_paths(const Ref<OpenXRAction> &p_action); // Determines the top level paths based on where an action is bound in interaction profiles

	// TODO add validation to display in the interface that checks if we have action sets with the same name or if we have interaction profiles for the same path

	~OpenXRActionMap();
};
