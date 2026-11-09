/**************************************************************************/
/*  openxr_composition_layer_quad.cpp                                     */

#include "openxr_composition_layer_quad.h"

#include "../extensions/openxr_composition_layer_extension.h"

#include "core/object/class_registry.h"
#include "scene/resources/spatial/primitive_meshes.h"

OpenXRCompositionLayerQuad::OpenXRCompositionLayerQuad() {
	if (composition_layer_extension) {
		XrCompositionLayerQuad openxr_composition_layer = {
			XR_TYPE_COMPOSITION_LAYER_QUAD, // type
			nullptr, // next
			0, // layerFlags
			XR_NULL_HANDLE, // space
			XR_EYE_VISIBILITY_BOTH, // eyeVisibility
			{}, // subImage
			{ { 0, 0, 0, 0 }, { 0, 0, 0 } }, // pose
			{ (float)quad_size.x, (float)quad_size.y }, // size
		};
		composition_layer = composition_layer_extension->composition_layer_create((XrCompositionLayerBaseHeader *)&openxr_composition_layer);
	}
}

OpenXRCompositionLayerQuad::~OpenXRCompositionLayerQuad() {
}

void OpenXRCompositionLayerQuad::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_quad_size", "size"), &OpenXRCompositionLayerQuad::set_quad_size);
	ClassRegistry::bind_method(D_METHOD("get_quad_size"), &OpenXRCompositionLayerQuad::get_quad_size);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "quad_size", PROPERTY_HINT_NONE, ""), "set_quad_size", "get_quad_size");
}

Ref<Mesh> OpenXRCompositionLayerQuad::_create_fallback_mesh() {
	Ref<QuadMesh> mesh;
	mesh.instantiate();
	mesh->set_size(quad_size);
	return mesh;
}

void OpenXRCompositionLayerQuad::set_quad_size(const Size2 &p_size) {
	quad_size = p_size;
	if (composition_layer_extension) {
		composition_layer_extension->composition_layer_set_quad_size(composition_layer, p_size);
	}
	update_fallback_mesh();
}

Size2 OpenXRCompositionLayerQuad::get_quad_size() const {
	return quad_size;
}

Vector2 OpenXRCompositionLayerQuad::intersects_ray(const Vector3 &p_origin, const Vector3 &p_direction) const {
	Transform3D quad_transform = get_global_transform();
	Vector3 quad_normal = quad_transform.basis.get_column(2);

	float denom = quad_normal.dot(p_direction);
	if (Math::abs(denom) > 0.0001) {
		Vector3 vector = quad_transform.origin - p_origin;
		float t = vector.dot(quad_normal) / denom;
		if (t < 0.0) {
			return Vector2(-1.0, -1.0);
		}
		Vector3 intersection = p_origin + p_direction * t;

		Vector3 relative_point = intersection - quad_transform.origin;
		Vector2 projected_point = Vector2(
				relative_point.dot(quad_transform.basis.get_column(0)),
				relative_point.dot(quad_transform.basis.get_column(1)));
		if (Math::abs(projected_point.x) > quad_size.x / 2.0) {
			return Vector2(-1.0, -1.0);
		}
		if (Math::abs(projected_point.y) > quad_size.y / 2.0) {
			return Vector2(-1.0, -1.0);
		}

		float u = 0.5 + (projected_point.x / quad_size.x);
		float v = 1.0 - (0.5 + (projected_point.y / quad_size.y));

		return Vector2(u, v);
	}

	return Vector2(-1.0, -1.0);
}
