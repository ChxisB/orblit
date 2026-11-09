/**************************************************************************/
/*  xr_pose.h                                                             */

#pragma once

#include "core/object/ref_counted.h"
#include "core/variant/type_info.h"

class XRPose : public RefCounted {
	ORBLIT_CLASS(XRPose, RefCounted);

public:
	// TrackingConfidence gives an indication of how reliable our transform data is.
	enum TrackingConfidence {
		XR_TRACKING_CONFIDENCE_NONE, // No tracking information is available for this pose.
		XR_TRACKING_CONFIDENCE_LOW, // Tracking information may be inaccurate or estimated.
		XR_TRACKING_CONFIDENCE_HIGH // Tracking information is deemed accurate and up to date.
	};

private:
	bool has_tracking_data = false;
	StringName name;
	Transform3D transform;
	Vector3 linear_velocity;
	Vector3 angular_velocity;
	TrackingConfidence tracking_confidence = XR_TRACKING_CONFIDENCE_NONE;

protected:
	static void register_members();

public:
	void set_has_tracking_data(const bool p_has_tracking_data);
	bool get_has_tracking_data() const;

	void set_name(const StringName &p_name);
	StringName get_name() const;

	void set_transform(const Transform3D p_transform);
	Transform3D get_transform() const;
	Transform3D get_adjusted_transform() const;

	void set_linear_velocity(const Vector3 p_velocity);
	Vector3 get_linear_velocity() const;

	void set_angular_velocity(const Vector3 p_velocity);
	Vector3 get_angular_velocity() const;

	void set_tracking_confidence(const TrackingConfidence p_tracking_confidence);
	TrackingConfidence get_tracking_confidence() const;
};

VARIANT_ENUM_CAST(XRPose::TrackingConfidence);
