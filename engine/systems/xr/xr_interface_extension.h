/**************************************************************************/
/*  xr_interface_extension.h                                              */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "systems/xr/xr_interface.h"

class XRInterfaceExtension : public XRInterface {
	ORBLIT_CLASS(XRInterfaceExtension, XRInterface);

public:
private:
	bool can_add_blits = false;
	Vector<RenderingSystemTypes::BlitToScreen> blits;

protected:
	_THREAD_SAFE_CLASS_

	static void register_members();

public:
	/** general interface information **/
	virtual StringName get_name() const override;
	virtual uint32_t get_capabilities() const override;

	ORBLIT_VIRTUAL0RC(StringName, _get_name);
	ORBLIT_VIRTUAL0RC(uint32_t, _get_capabilities);

	virtual bool is_initialized() const override;
	virtual bool initialize() override;
	virtual void uninitialize() override;
	virtual Dictionary get_system_info() override;

	ORBLIT_VIRTUAL0RC(bool, _is_initialized);
	ORBLIT_VIRTUAL0R(bool, _initialize);
	ORBLIT_VIRTUAL0(_uninitialize);
	ORBLIT_VIRTUAL0RC(Dictionary, _get_system_info);

	/** input and output **/

	virtual PackedStringArray get_suggested_tracker_names() const override; /* return a list of likely/suggested tracker names */
	virtual PackedStringArray get_suggested_pose_names(const StringName &p_tracker_name) const override; /* return a list of likely/suggested action names for this tracker */
	virtual TrackingStatus get_tracking_status() const override;
	virtual void trigger_haptic_pulse(const String &p_action_name, const StringName &p_tracker_name, double p_frequency, double p_amplitude, double p_duration_sec, double p_delay_sec = 0) override;

	ORBLIT_VIRTUAL0RC(PackedStringArray, _get_suggested_tracker_names);
	ORBLIT_VIRTUAL1RC(PackedStringArray, _get_suggested_pose_names, const StringName &);
	ORBLIT_VIRTUAL0RC(XRInterface::TrackingStatus, _get_tracking_status);
	ORBLIT_VIRTUAL6(_trigger_haptic_pulse, const String &, const StringName &, double, double, double, double);

	/** specific to VR **/
	virtual bool supports_play_area_mode(XRInterface::PlayAreaMode p_mode) override; /* query if this interface supports this play area mode */
	virtual XRInterface::PlayAreaMode get_play_area_mode() const override; /* get the current play area mode */
	virtual bool set_play_area_mode(XRInterface::PlayAreaMode p_mode) override; /* change the play area mode, note that this should return false if the mode is not available */
	virtual PackedVector3Array get_play_area() const override; /* if available, returns an array of vectors denoting the play area the player can move around in */

	ORBLIT_VIRTUAL1RC(bool, _supports_play_area_mode, XRInterface::PlayAreaMode);
	ORBLIT_VIRTUAL0RC(XRInterface::PlayAreaMode, _get_play_area_mode);
	ORBLIT_VIRTUAL1RC(bool, _set_play_area_mode, XRInterface::PlayAreaMode);
	ORBLIT_VIRTUAL0RC(PackedVector3Array, _get_play_area);

	/** specific to AR **/
	virtual bool get_anchor_detection_is_enabled() const override;
	virtual void set_anchor_detection_is_enabled(bool p_enable) override;
	virtual int get_camera_feed_id() override;

	ORBLIT_VIRTUAL0RC(bool, _get_anchor_detection_is_enabled);
	ORBLIT_VIRTUAL1(_set_anchor_detection_is_enabled, bool);
	ORBLIT_VIRTUAL0RC(int, _get_camera_feed_id);

	/** rendering and internal **/

	virtual Size2 get_render_target_size() override;
	virtual uint32_t get_view_count() override;
	virtual Transform3D get_camera_transform() override;
	virtual TypedArray<Projection> get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far) override;
	virtual TypedArray<Transform3D> get_camera_offsets(const StringName &p_tracker_name) override;
	virtual RID get_vrs_texture() override;
	virtual VRSTextureFormat get_vrs_texture_format() override;
	virtual RID get_color_texture() override;
	virtual RID get_depth_texture() override;
	virtual RID get_velocity_texture() override;

	ORBLIT_VIRTUAL0R(Size2, _get_render_target_size);
	ORBLIT_VIRTUAL0R(uint32_t, _get_view_count);
	ORBLIT_VIRTUAL0R(Transform3D, _get_camera_transform);
	ORBLIT_VIRTUAL4R(TypedArray<Projection>, _get_camera_projections, const StringName &, double, double, double);
	ORBLIT_VIRTUAL1R(TypedArray<Transform3D>, _get_camera_offsets, const StringName &);
	ORBLIT_VIRTUAL0R(RID, _get_vrs_texture);
	ORBLIT_VIRTUAL0R(VRSTextureFormat, _get_vrs_texture_format);
	ORBLIT_VIRTUAL0R(RID, _get_color_texture);
	ORBLIT_VIRTUAL0R(RID, _get_depth_texture);
	ORBLIT_VIRTUAL0R(RID, _get_velocity_texture);

	void add_blit(RID p_render_target, Rect2 p_src_rect, Rect2i p_dst_rect, bool p_use_layer = false, uint32_t p_layer = 0, bool p_apply_lens_distortion = false, Vector2 p_eye_center = Vector2(), double p_k1 = 0.0, double p_k2 = 0.0, double p_upscale = 1.0, double p_aspect_ratio = 1.0);

	virtual void process() override;
	virtual void pre_render() override;
	virtual bool pre_draw_viewport(RID p_render_target) override;
	virtual Vector<RenderingSystemTypes::BlitToScreen> post_draw_viewport(RID p_render_target, const Rect2 &p_screen_rect) override;
	virtual void end_frame() override;

	ORBLIT_VIRTUAL0(_process);
	ORBLIT_VIRTUAL0(_pre_render);
	ORBLIT_VIRTUAL1R(bool, _pre_draw_viewport, RID);
	ORBLIT_VIRTUAL2(_post_draw_viewport, RID, const Rect2 &);
	ORBLIT_VIRTUAL0(_end_frame);

	/* access to some internals we need */
	RID get_render_target_texture(RID p_render_target);
	// RID get_render_target_depth(RID p_render_target);
};
