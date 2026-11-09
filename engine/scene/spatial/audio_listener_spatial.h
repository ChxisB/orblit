/**************************************************************************/
/*  audio_listener_spatial.h                                                   */

#pragma once

#include "scene/spatial/entity_spatial.h"
#include "scene/spatial/velocity_tracker_spatial.h"

class AudioListener3D : public Entity3D {
	ORBLIT_CLASS(AudioListener3D, Entity3D);

public:
	enum DopplerTracking {
		DOPPLER_TRACKING_DISABLED,
		DOPPLER_TRACKING_IDLE_STEP,
		DOPPLER_TRACKING_PHYSICS_STEP,
	};

	void make_current();
	void clear_current();
	bool is_current() const;

	virtual Transform3D get_listener_transform() const;

	void set_doppler_tracking(DopplerTracking p_tracking);
	DopplerTracking get_doppler_tracking() const;

	Vector3 get_doppler_tracked_velocity() const;

	AudioListener3D();
	~AudioListener3D();

private:
	bool force_change = false;
	bool current = false;

	RID scenario_id;

	friend class Viewport;
	void _update_audio_listener_state();

	DopplerTracking doppler_tracking = DOPPLER_TRACKING_DISABLED;
	Ref<VelocityTracker3D> velocity_tracker;

protected:
	void _update_listener();
	virtual void _request_listener_update();

	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	void _notification(int p_what);

	static void register_members();
};

VARIANT_ENUM_CAST(AudioListener3D::DopplerTracking);
