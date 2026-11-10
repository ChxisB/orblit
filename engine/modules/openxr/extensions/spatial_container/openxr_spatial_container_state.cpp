/**************************************************************************/
/*  openxr_spatial_container_state.cpp                                    */

#include "openxr_spatial_container_state.h"

#include "core/object/class_registry.h"

OpenXRSpatialContainerState::OpenXRSpatialContainerState(const XrSpatialContainerStateEXT &p_state) {
	bounds_mode = _to_bounds_mode(p_state.boundsMode);
	interactable = p_state.interactable;
	visible = p_state.visible;
}

void OpenXRSpatialContainerState::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_bounds_mode"), &OpenXRSpatialContainerState::get_bounds_mode);
	ClassRegistry::bind_method(D_METHOD("is_interactable"), &OpenXRSpatialContainerState::is_interactable);
	ClassRegistry::bind_method(D_METHOD("is_visible"), &OpenXRSpatialContainerState::is_visible);

	BIND_ENUM_CONSTANT(BOUNDS_MODE_BOUNDED);
	BIND_ENUM_CONSTANT(BOUNDS_MODE_IMMERSIVE);
}

OpenXRSpatialContainerState::BoundsMode OpenXRSpatialContainerState::get_bounds_mode() const {
	return bounds_mode;
}

bool OpenXRSpatialContainerState::is_interactable() const {
	return interactable;
}

bool OpenXRSpatialContainerState::is_visible() const {
	return visible;
}

XrSpatialContainerBoundsModeEXT OpenXRSpatialContainerState::_from_bounds_mode(OpenXRSpatialContainerState::BoundsMode p_mode) {
	switch (p_mode) {
		case BOUNDS_MODE_BOUNDED:
		default: {
			return XR_SPATIAL_CONTAINER_BOUNDS_MODE_BOUNDED_EXT;
		} break;
		case BOUNDS_MODE_IMMERSIVE: {
			return XR_SPATIAL_CONTAINER_BOUNDS_MODE_IMMERSIVE_EXT;
		} break;
	}
}

OpenXRSpatialContainerState::BoundsMode OpenXRSpatialContainerState::_to_bounds_mode(XrSpatialContainerBoundsModeEXT p_mode) {
	switch (p_mode) {
		case XR_SPATIAL_CONTAINER_BOUNDS_MODE_BOUNDED_EXT:
		default: {
			return BOUNDS_MODE_BOUNDED;
		} break;
		case XR_SPATIAL_CONTAINER_BOUNDS_MODE_IMMERSIVE_EXT: {
			return BOUNDS_MODE_IMMERSIVE;
		} break;
	}
}
