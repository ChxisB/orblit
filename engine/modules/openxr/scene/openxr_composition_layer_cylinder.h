/**************************************************************************/
/*  openxr_composition_layer_cylinder.h                                   */

#pragma once

#include "openxr_composition_layer.h"

#include <openxr/openxr.h>

class OpenXRCompositionLayerCylinder : public OpenXRCompositionLayer {
	ORBLIT_CLASS(OpenXRCompositionLayerCylinder, OpenXRCompositionLayer);

	float radius = 1.0;
	float aspect_ratio = 1.0;
	float central_angle = Math::PI / 2.0;
	uint32_t fallback_segments = 10;

protected:
	static void register_members();

	virtual Ref<Mesh> _create_fallback_mesh() override;
	virtual XrStructureType _get_openxr_type() const override {
		return XR_TYPE_COMPOSITION_LAYER_CYLINDER_KHR;
	}

public:
	void set_radius(float p_radius);
	float get_radius() const;

	void set_aspect_ratio(float p_aspect_ratio);
	float get_aspect_ratio() const;

	void set_central_angle(float p_angle);
	float get_central_angle() const;

	void set_fallback_segments(uint32_t p_fallback_segments);
	uint32_t get_fallback_segments() const;

	virtual Vector2 intersects_ray(const Vector3 &p_origin, const Vector3 &p_direction) const override;

	OpenXRCompositionLayerCylinder();
	~OpenXRCompositionLayerCylinder();
};
