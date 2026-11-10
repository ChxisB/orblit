/**************************************************************************/
/*  openxr_user_presence_extension.h                                      */

#pragma once

#include "openxr_extension_wrapper.h"

// When supported, the user presence extension allows an application to detect and respond to change
// of user presence, such as when the user has taken off or put on an XR headset.
//
// See: https://registry.khronos.org/OpenXR/specs/1.1/html/xrspec.html#XR_EXT_user_presence
// for more information.

class OpenXRUserPresenceExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRUserPresenceExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRUserPresenceExtension *get_singleton();

	OpenXRUserPresenceExtension();
	virtual ~OpenXRUserPresenceExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;
	virtual void *set_system_properties_and_get_next_pointer(void *p_next_pointer) override;

	virtual void on_state_ready() override;
	virtual void on_state_stopping() override;

	virtual bool on_event_polled(const XrEventDataBuffer &event) override;

	bool is_user_present() const;
	bool is_active() const;

private:
	static OpenXRUserPresenceExtension *singleton;

	bool available = false;
	XrSystemUserPresencePropertiesEXT properties;

	bool user_present = true;
};
