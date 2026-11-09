/**************************************************************************/
/*  velocity_tracker_spatial.h                                                 */

#pragma once

#include "core/object/ref_counted.h"

class VelocityTracker3D : public RefCounted {
	ORBLIT_SOFTCLASS(VelocityTracker3D, RefCounted);

	struct PositionHistory {
		uint64_t frame = 0;
		Vector3 position;
	};

	bool physics_step = false;
	Vector<PositionHistory> position_history;
	int position_history_len = 0;

public:
	void reset(const Vector3 &p_new_pos);
	void set_track_physics_step(bool p_track_physics_step);
	bool is_tracking_physics_step() const;
	void update_position(const Vector3 &p_position);
	Vector3 get_tracked_linear_velocity() const;

	VelocityTracker3D();
};
