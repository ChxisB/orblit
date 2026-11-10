/**************************************************************************/
/*  openxr_composition_layer_quad.h                                       */

#pragma once

#include "openxr_composition_layer.h"

#include <openxr/openxr.h>

class OpenXRCompositionLayerQuad : public OpenXRCompositionLayer {
	ORBLIT_CLASS(OpenXRCompositionLayerQuad, OpenXRCompositionLayer);

	Size2 quad_size = Size2(1.0, 1.0);

protected:
	static void register_members();

	virtual Ref<Mesh> _create_fallback_mesh() override;
	virtual XrStructureType _get_openxr_type() const override {
		return XR_TYPE_COMPOSITION_LAYER_QUAD;
	}

public:
	void set_quad_size(const Size2 &p_size);
	Size2 get_quad_size() const;

	virtual Vector2 intersects_ray(const Vector3 &p_origin, const Vector3 &p_direction) const override;

	OpenXRCompositionLayerQuad();
	~OpenXRCompositionLayerQuad();
};
