/**************************************************************************/
/*  openxr_spatial_container_state.h                                      */

#pragma once

#include "core/object/ref_counted.h"
#include "core/variant/binder_common.h"

#include <openxr/openxr.h>

class OpenXRSpatialContainerState : public RefCounted {
	ORBLIT_CLASS(OpenXRSpatialContainerState, RefCounted);

public:
	enum BoundsMode {
		BOUNDS_MODE_BOUNDED,
		BOUNDS_MODE_IMMERSIVE,
	};

	BoundsMode get_bounds_mode() const;
	bool is_interactable() const;
	bool is_visible() const;

	OpenXRSpatialContainerState() {}
	OpenXRSpatialContainerState(const XrSpatialContainerStateEXT &p_state);

protected:
	static void register_members();

private:
	friend class OpenXRSpatialContainerExtension;

	static XrSpatialContainerBoundsModeEXT _from_bounds_mode(BoundsMode p_mode);
	static BoundsMode _to_bounds_mode(XrSpatialContainerBoundsModeEXT p_mode);

	BoundsMode bounds_mode = BOUNDS_MODE_BOUNDED;
	bool interactable = false;
	bool visible = false;
};

VARIANT_ENUM_CAST(OpenXRSpatialContainerState::BoundsMode);
