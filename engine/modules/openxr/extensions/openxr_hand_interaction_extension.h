/**************************************************************************/
/*  openxr_hand_interaction_extension.h                                   */

#pragma once

#include "openxr_extension_wrapper.h"

// When supported the hand interaction extension introduces an interaction
// profile that becomes active when the user either lets go of their
// controllers or isn't using controllers at all.
//
// The OpenXR specification states that all XR runtimes that support this
// interaction profile should also allow it's controller to use this
// interaction profile.
// This means that if you only supply this interaction profile in your
// action map, it should work both when the player is holding a controller
// or using visual hand tracking.
//
// This allows easier portability between games that use controller
// tracking or hand tracking.
//
// See: https://registry.khronos.org/OpenXR/specs/1.0/html/xrspec.html#XR_EXT_hand_interaction
// for more information.

class OpenXRInteractionProfileMetadata;

class OpenXRHandInteractionExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRHandInteractionExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRHandInteractionExtension *get_singleton();

	OpenXRHandInteractionExtension();
	virtual ~OpenXRHandInteractionExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available();

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	static OpenXRHandInteractionExtension *singleton;

	bool available = false;
};
