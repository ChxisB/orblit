/**************************************************************************/
/*  openxr_interface.h                                                    */

#pragma once

// A note on multithreading and thread safety in OpenXR.
//
// Most entry points will be called from the main thread in Orblit
// however a number of entry points will be called from the
// rendering thread, potentially while we're already processing
// the next frame on the main thread.
//
// OpenXR itself has been designed with threading in mind including
// a high likelihood that the XR runtime runs in separate threads
// as well.
// Hence all the frame timing information, use of swapchains and
// sync functions.
// Do note that repeated calls to tracking APIs will provide
// increasingly more accurate data for the same timestamp as
// tracking data is continuously updated.
//
// For our code we mostly implement this in our OpenXRAPI class.
// We store data accessed from the rendering thread in a separate
// struct, setting values through our renderer command queue.
//
// As some data is setup before we start rendering, and cleaned up
// after we've stopped, that is accessed directly from both threads.

#include "extensions/openxr_hand_tracking_extension.h"
#include "openxr_api.h"

#include "systems/xr/xr_controller_tracker.h"
#include "systems/xr/xr_interface.h"
#include "systems/xr/xr_vrs.h"

// declare some default strings
#define INTERACTION_PROFILE_NONE "/interaction_profiles/none"

class OpenXRInterface : public XRInterface {
	ORBLIT_CLASS(OpenXRInterface, XRInterface);

private:
	OpenXRAPI *openxr_api = nullptr;
	bool initialized = false;
	bool reference_stage_changing = false;
	XRInterface::TrackingStatus tracking_state;

	// At a minimum we need a tracker for our head
	Ref<XRPositionalTracker> head;
	Transform3D head_transform;
	Vector3 head_linear_velocity;
	Vector3 head_angular_velocity;
	XRPose::TrackingConfidence head_confidence;
	Transform3D transform_for_view[2]; // We currently assume 2, but could be 4 for VARJO which we do not support yet

	XRVRS xr_vrs;

	void _load_action_map();

	struct Action { // An action we've registered with OpenXR
		String action_name; // Name of our action as presented to Orblit (can be altered from the action map)
		OpenXRAction::ActionType action_type; // The action type of this action
		RID action_rid; // RID of the action registered with our OpenXR API
	};
	struct ActionSet { // An action set we've registered with OpenXR
		String action_set_name; // Name of our action set
		bool is_active; // If true this action set is active and we will sync it
		Vector<Action *> actions; // List of actions in this action set
		RID action_set_rid; // RID of the action registered with our OpenXR API
	};
	struct Tracker { // A tracker we've registered with OpenXR
		String tracker_name; // Name of our tracker (can be altered from the action map)
		Vector<Action *> actions; // Actions related to this tracker
		Ref<XRControllerTracker> controller_tracker; // Our positional tracker object that holds our tracker state
		RID tracker_rid; // RID of the tracker registered with our OpenXR API
		RID interaction_profile; // RID of the interaction profile bound to this tracker (can be null)
	};

	Vector<ActionSet *> action_sets;
	Vector<RID> interaction_profiles;
	Vector<Tracker *> trackers;

	ActionSet *create_action_set(const String &p_action_set_name, const String &p_localized_name, const int p_priority);
	void free_action_sets();

	Action *create_action(ActionSet *p_action_set, const String &p_action_name, const String &p_localized_name, OpenXRAction::ActionType p_action_type, const Vector<Tracker *> p_trackers);
	Action *find_action(const String &p_action_name);
	void free_actions(ActionSet *p_action_set);

	Tracker *find_tracker(const String &p_tracker_name, bool p_create = false);
	void handle_tracker(Tracker *p_tracker);
	void free_trackers();

	void free_interaction_profiles();

	void _set_default_pos(Transform3D &r_transform, double p_world_scale, uint64_t p_eye);

	void handle_hand_tracking(const String &p_path, OpenXRHandTrackingExtension::HandTrackedHands p_hand);

protected:
	static void register_members();

public:
	virtual StringName get_name() const override;
	virtual uint32_t get_capabilities() const override;

	virtual PackedStringArray get_suggested_tracker_names() const override;
	virtual TrackingStatus get_tracking_status() const override;

	bool is_hand_tracking_supported();
	bool is_hand_interaction_supported() const;
	bool is_eye_gaze_interaction_supported();

	bool initialize_on_startup() const;
	virtual bool is_initialized() const override;
	virtual bool initialize() override;
	virtual void uninitialize() override;
	virtual Dictionary get_system_info() override;

	virtual void trigger_haptic_pulse(const String &p_action_name, const StringName &p_tracker_name, double p_frequency, double p_amplitude, double p_duration_sec, double p_delay_sec = 0) override;

	virtual bool supports_play_area_mode(XRInterface::PlayAreaMode p_mode) override;
	virtual XRInterface::PlayAreaMode get_play_area_mode() const override;
	virtual bool set_play_area_mode(XRInterface::PlayAreaMode p_mode) override;
	virtual PackedVector3Array get_play_area() const override;

	float get_display_refresh_rate() const;
	void set_display_refresh_rate(float p_refresh_rate);
	Array get_available_display_refresh_rates() const;

	bool is_action_set_active(const String &p_action_set) const;
	void set_action_set_active(const String &p_action_set, bool p_active);
	Array get_action_sets() const;

	Size2 get_recommended_target_size() const;

	double get_render_target_size_multiplier() const;
	void set_render_target_size_multiplier(double multiplier);

	bool is_foveation_supported() const;

	int get_foveation_level() const;
	void set_foveation_level(int p_foveation_level);

	bool get_foveation_dynamic() const;
	void set_foveation_dynamic(bool p_foveation_dynamic);

	bool get_foveation_with_subsampled_images() const;
	void set_foveation_with_subsampled_images(bool p_enabled);

	float get_vrs_min_radius() const;
	void set_vrs_min_radius(float p_vrs_min_radius);

	float get_vrs_strength() const;
	void set_vrs_strength(float p_vrs_strength);

	virtual Size2 get_render_target_size() override;
	virtual uint32_t get_view_count() override;
	virtual Transform3D get_camera_transform() override;
	virtual TypedArray<Projection> get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far) override;
	virtual TypedArray<Transform3D> get_camera_offsets(const StringName &p_tracker_name) override;

	virtual Rect2i get_render_region() override;

	virtual RID get_color_texture() override;
	virtual RID get_depth_texture() override;
	virtual RID get_velocity_texture() override;
	virtual RID get_velocity_depth_texture() override;
	virtual Size2i get_velocity_target_size() override;

	virtual void process() override;
	virtual void pre_render() override;
	bool pre_draw_viewport(RID p_render_target) override;
	virtual Vector<RenderingSystemTypes::BlitToScreen> post_draw_viewport(RID p_render_target, const Rect2 &p_screen_rect) override;
	virtual void end_frame() override;

	/** environment blend mode. */
	virtual Array get_supported_environment_blend_modes() override;
	virtual XRInterface::EnvironmentBlendMode get_environment_blend_mode() const override;
	virtual bool set_environment_blend_mode(XRInterface::EnvironmentBlendMode mode) override;

	void on_state_ready();
	void on_state_visible();
	void on_state_focused();
	void on_state_synchronized();
	void on_state_stopping();
	void on_state_loss_pending();
	void on_state_exiting();
	void on_reference_space_change_pending(XrReferenceSpaceType p_type);
	void on_refresh_rate_changes(float p_new_rate);
	void tracker_profile_changed(RID p_tracker, RID p_interaction_profile);

	/** Session */
	enum SessionState { // Should mirror XrSessionState
		SESSION_STATE_UNKNOWN = 0,
		SESSION_STATE_IDLE = 1,
		SESSION_STATE_READY = 2,
		SESSION_STATE_SYNCHRONIZED = 3,
		SESSION_STATE_VISIBLE = 4,
		SESSION_STATE_FOCUSED = 5,
		SESSION_STATE_STOPPING = 6,
		SESSION_STATE_LOSS_PENDING = 7,
		SESSION_STATE_EXITING = 8,
	};

	SessionState get_session_state();

	/** User presence. */
	bool is_user_presence_supported() const;
	bool is_user_present() const;

	/** View configuration */
	enum ViewConfiguration {
		VIEW_CONFIGURATION_MONO = 0,
		VIEW_CONFIGURATION_STEREO = 1,
		VIEW_CONFIGURATION_STEREO_WITH_INSET = 2,
		VIEW_CONFIGURATION_UNSET = 254,
		VIEW_CONFIGURATION_UNKNOWN = 255,
	};

	ViewConfiguration get_active_view_configuration() const;

	/** Hand tracking. */
	enum Hand {
		HAND_LEFT,
		HAND_RIGHT,
		HAND_MAX,
	};

	enum HandMotionRange {
		HAND_MOTION_RANGE_UNOBSTRUCTED,
		HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER,
		HAND_MOTION_RANGE_MAX
	};

	void set_motion_range(const Hand p_hand, const HandMotionRange p_motion_range);
	HandMotionRange get_motion_range(const Hand p_hand) const;

	virtual RID get_vrs_texture() override;
	virtual VRSTextureFormat get_vrs_texture_format() override;

	// Performance settings.
	enum PerfSettingsLevel {
		PERF_SETTINGS_LEVEL_POWER_SAVINGS,
		PERF_SETTINGS_LEVEL_SUSTAINED_LOW,
		PERF_SETTINGS_LEVEL_SUSTAINED_HIGH,
		PERF_SETTINGS_LEVEL_BOOST,
	};

	enum PerfSettingsSubDomain {
		PERF_SETTINGS_SUB_DOMAIN_COMPOSITING,
		PERF_SETTINGS_SUB_DOMAIN_RENDERING,
		PERF_SETTINGS_SUB_DOMAIN_THERMAL,
	};

	enum PerfSettingsNotificationLevel {
		PERF_SETTINGS_NOTIF_LEVEL_NORMAL,
		PERF_SETTINGS_NOTIF_LEVEL_WARNING,
		PERF_SETTINGS_NOTIF_LEVEL_IMPAIRED,
	};

	void set_cpu_level(PerfSettingsLevel p_level);
	void set_gpu_level(PerfSettingsLevel p_level);
	void on_cpu_level_changed(PerfSettingsSubDomain p_sub_domain, PerfSettingsNotificationLevel p_from_level, PerfSettingsNotificationLevel p_to_level);
	void on_gpu_level_changed(PerfSettingsSubDomain p_sub_domain, PerfSettingsNotificationLevel p_from_level, PerfSettingsNotificationLevel p_to_level);

	OpenXRInterface();
	~OpenXRInterface();
};

VARIANT_ENUM_CAST(OpenXRInterface::SessionState)
VARIANT_ENUM_CAST(OpenXRInterface::ViewConfiguration)
VARIANT_ENUM_CAST(OpenXRInterface::Hand)
VARIANT_ENUM_CAST(OpenXRInterface::HandMotionRange)
VARIANT_ENUM_CAST(OpenXRInterface::PerfSettingsLevel)
VARIANT_ENUM_CAST(OpenXRInterface::PerfSettingsSubDomain)
VARIANT_ENUM_CAST(OpenXRInterface::PerfSettingsNotificationLevel)
