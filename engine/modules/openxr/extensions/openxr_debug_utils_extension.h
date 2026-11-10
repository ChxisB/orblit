/**************************************************************************/
/*  openxr_debug_utils_extension.h                                        */

#pragma once

#include "../util.h"
#include "openxr_extension_wrapper.h"

class OpenXRDebugUtilsExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRDebugUtilsExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRDebugUtilsExtension *get_singleton();

	OpenXRDebugUtilsExtension();
	virtual ~OpenXRDebugUtilsExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;
	virtual void on_instance_created(const XrInstance p_instance) override;
	virtual void on_instance_destroyed() override;

	bool get_active();

	void set_object_name(XrObjectType p_object_type, uint64_t p_object_handle, const char *p_object_name);
	void begin_debug_label_region(const char *p_label_name);
	void end_debug_label_region();
	void insert_debug_label(const char *p_label_name);

private:
	static OpenXRDebugUtilsExtension *singleton;

	// related extensions
	bool debug_utils_ext = false;

	// debug handlers
	XrDebugUtilsMessengerEXT default_messenger = XR_NULL_HANDLE;

	static XrBool32 XRAPI_PTR _debug_callback(XrDebugUtilsMessageSeverityFlagsEXT p_message_severity, XrDebugUtilsMessageTypeFlagsEXT p_message_types, const XrDebugUtilsMessengerCallbackDataEXT *p_callback_data, void *p_user_data);
	XrBool32 debug_callback(XrDebugUtilsMessageSeverityFlagsEXT p_message_severity, XrDebugUtilsMessageTypeFlagsEXT p_message_types, const XrDebugUtilsMessengerCallbackDataEXT *p_callback_data, void *p_user_data);

	// OpenXR API call wrappers
	EXT_PROTO_XRRESULT_FUNC3(xrCreateDebugUtilsMessengerEXT, (XrInstance), p_instance, (const XrDebugUtilsMessengerCreateInfoEXT *), p_create_info, (XrDebugUtilsMessengerEXT *), p_messenger)
	EXT_PROTO_XRRESULT_FUNC1(xrDestroyDebugUtilsMessengerEXT, (XrDebugUtilsMessengerEXT), p_messenger)
	EXT_PROTO_XRRESULT_FUNC2(xrSetDebugUtilsObjectNameEXT, (XrInstance), p_instance, (const XrDebugUtilsObjectNameInfoEXT *), p_name_info)
	EXT_PROTO_XRRESULT_FUNC2(xrSessionBeginDebugUtilsLabelRegionEXT, (XrSession), p_session, (const XrDebugUtilsLabelEXT *), p_label_info)
	EXT_PROTO_XRRESULT_FUNC1(xrSessionEndDebugUtilsLabelRegionEXT, (XrSession), p_session)
	EXT_PROTO_XRRESULT_FUNC2(xrSessionInsertDebugUtilsLabelEXT, (XrSession), p_session, (const XrDebugUtilsLabelEXT *), p_label_info)
};
