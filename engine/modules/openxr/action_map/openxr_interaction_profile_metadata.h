/**************************************************************************/
/*  openxr_interaction_profile_metadata.h                                 */

#pragma once

///////////////////////////////////////////////////////////////////////////
// Stores available interaction profile metadata
//
// OpenXR defines and hardcodes all the supported input devices and their
// paths as part of the OpenXR spec. When support for new devices is
// introduced this often starts life as an extension that needs to be enabled
// until it's adopted into the core. As there is no interface to
// enumerate the possibly paths, and that any OpenXR runtime would likely
// limit such enumeration to those input devices supported by that runtime
// there is no other option than to hardcode this.
//
// Note that we need to include paths of our extensions in our action map
// regardless of whether the developers machine supports the extension or
// not. Unsupported paths are filtered out when the action map is submitted
// to the OpenXR runtime.
//
// Note on action type that automatic conversions between boolean and float
// are supported but otherwise action types should match between action and
// input/output paths.

#include "openxr_action.h"

#include "core/containers/hash_map.h"
#include "core/object/object.h"

#define XR_PATH_UNSUPPORTED_NAME "unsupported"

class OpenXRInteractionProfileMetadata : public Object {
	ORBLIT_CLASS(OpenXRInteractionProfileMetadata, Object);

public:
	struct TopLevelPath {
		String display_name; // User friendly display name (i.e. Left controller)
		String openxr_path; // Path in OpenXR (i.e. /user/hand/left)
		String openxr_extension_names; // If set, only available if extension is enabled (i.e. XR_HTCX_vive_tracker_interaction)
	};

	struct IOPath {
		String display_name; // User friendly display name (i.e. Grip pose (left controller))
		String top_level_path; // Top level path identifying the usage of the device in relation to this input/output
		String openxr_path; // Path in OpenXR (i.e. /user/hand/left/input/grip/pose)
		String openxr_extension_names; // If set, only available if extension is enabled (i.e. XR_EXT_palm_pose)
		OpenXRAction::ActionType action_type; // Type of input/output
	};

	struct InteractionProfile {
		String display_name; // User friendly display name (i.e. Simple controller)
		String openxr_path; // Path in OpenXR (i.e. /interaction_profiles/khr/simple_controller)
		String openxr_extension_names; // If set, only available if extension is enabled (i.e. XR_HTCX_vive_tracker_interaction)
		Vector<IOPath> io_paths; // Inputs and outputs for this device

		bool has_io_path(const String &p_io_path) const;
		const IOPath *get_io_path(const String &p_io_path) const;
	};

private:
	static OpenXRInteractionProfileMetadata *singleton;

	HashMap<String, String> profile_renames;
	HashMap<String, String> path_renames;

	Vector<TopLevelPath> top_level_paths;
	Vector<InteractionProfile> interaction_profiles;

	void _register_core_metadata();

protected:
	static void register_members();

public:
	static OpenXRInteractionProfileMetadata *get_singleton() { return singleton; }

	OpenXRInteractionProfileMetadata();
	~OpenXRInteractionProfileMetadata();

	void register_profile_rename(const String &p_old_name, const String &p_new_name);
	String check_profile_name(const String &p_name) const;

	void register_path_rename(const String &p_old_name, const String &p_new_name);
	String check_path_name(const String &p_name) const;

	void register_top_level_path(const String &p_display_name, const String &p_openxr_path, const String &p_openxr_extension_names);
	bool has_top_level_path(const String &p_openxr_path) const;
	String get_top_level_name(const String &p_openxr_path) const;
	String get_top_level_extensions(const String &p_openxr_path) const;

	void register_interaction_profile(const String &p_display_name, const String &p_openxr_path, const String &p_openxr_extension_names);
	bool has_interaction_profile(const String &p_openxr_path) const;
	String get_interaction_profile_extensions(const String &p_openxr_path) const;
	const InteractionProfile *get_profile(const String &p_openxr_path) const;
	PackedStringArray get_interaction_profile_paths() const;

	void register_io_path(const String &p_interaction_profile, const String &p_display_name, const String &p_toplevel_path, const String &p_openxr_path, const String &p_openxr_extension_names, OpenXRAction::ActionType p_action_type);
	const IOPath *get_io_path(const String &p_interaction_profile, const String &p_io_path) const;
};
