/**************************************************************************/
/*  webxr.h                                                               */

#pragma once

#ifdef __cplusplus
extern "C" {
#endif

enum WebXRInputEvent {
	WEBXR_INPUT_EVENT_SELECTSTART,
	WEBXR_INPUT_EVENT_SELECTEND,
	WEBXR_INPUT_EVENT_SQUEEZESTART,
	WEBXR_INPUT_EVENT_SQUEEZEEND,
};

typedef void (*OrblitWebXRSupportedCallback)(char *p_session_mode, int p_supported);
typedef void (*OrblitWebXRStartedCallback)(char *p_reference_space_type, char *p_enabled_features, char *p_environment_blend_mode);
typedef void (*OrblitWebXREndedCallback)();
typedef void (*OrblitWebXRFailedCallback)(char *p_message);
typedef void (*OrblitWebXRInputEventCallback)(int p_event_type, int p_input_source_id);
typedef void (*OrblitWebXRSimpleEventCallback)(char *p_signal_name);

extern int orblit_webxr_is_supported();
extern void orblit_webxr_is_session_supported(const char *p_session_mode, OrblitWebXRSupportedCallback p_callback);

extern void orblit_webxr_initialize(
		const char *p_session_mode,
		const char *p_required_features,
		const char *p_optional_features,
		const char *p_requested_reference_space_types,
		bool p_disable_webxr_layers,
		OrblitWebXRStartedCallback p_on_session_started,
		OrblitWebXREndedCallback p_on_session_ended,
		OrblitWebXRFailedCallback p_on_session_failed,
		OrblitWebXRInputEventCallback p_on_input_event,
		OrblitWebXRSimpleEventCallback p_on_simple_event);
extern void orblit_webxr_uninitialize();

extern int orblit_webxr_get_view_count();
extern bool orblit_webxr_get_render_target_size(int *r_size);
extern bool orblit_webxr_get_transform_for_view(int p_view, float *r_transform);
extern bool orblit_webxr_get_projection_for_view(int p_view, float *r_transform);
extern unsigned int orblit_webxr_get_color_texture();
extern unsigned int orblit_webxr_get_depth_texture();
extern unsigned int orblit_webxr_get_velocity_texture();
extern void orblit_webxr_commit_render_target(unsigned int p_texture_id, int p_layer_count);

extern bool orblit_webxr_update_input_source(
		int p_input_source_id,
		float *r_target_pose,
		int *r_target_ray_mode,
		int *r_touch_index,
		int *r_has_grip_pose,
		float *r_grip_pose,
		int *r_has_standard_mapping,
		int *r_button_count,
		float *r_buttons,
		int *r_axes_count,
		float *r_axes,
		int *r_has_hand_data,
		float *r_hand_joints,
		float *r_hand_radii);

extern char *orblit_webxr_get_visibility_state();
extern int orblit_webxr_get_bounds_geometry(float **r_points);

extern float orblit_webxr_get_frame_rate();
extern void orblit_webxr_update_target_frame_rate(float p_frame_rate);
extern int orblit_webxr_get_supported_frame_rates(float **r_frame_rates);

#ifdef __cplusplus
}
#endif
