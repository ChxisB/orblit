/**************************************************************************/
/*  multimesh.cpp                                                         */

#include "multimesh.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

void MultiMesh::set_buffer(const Vector<float> &p_buffer) {
	if (instance_count == 0) {
		return;
	}

	uint32_t stride = transform_format == TRANSFORM_2D ? 8 : 12;
	stride += use_colors ? 4 : 0;
	stride += use_custom_data ? 4 : 0;
	ERR_FAIL_COND_MSG(stride * instance_count != p_buffer.size(), "Cannot set a buffer on a Multimesh that is a different size from the Multimesh's existing buffer.");

	RS::get_singleton()->multimesh_set_buffer(multimesh, p_buffer);
}

Vector<float> MultiMesh::get_buffer() const {
	return RS::get_singleton()->multimesh_get_buffer(multimesh);
}

void MultiMesh::set_buffer_interpolated(const Vector<float> &p_buffer_curr, const Vector<float> &p_buffer_prev) {
	RS::get_singleton()->multimesh_set_buffer_interpolated(multimesh, p_buffer_curr, p_buffer_prev);
}

void MultiMesh::set_mesh(const Ref<Mesh> &p_mesh) {
	mesh = p_mesh;
	if (mesh.is_valid()) {
		RenderingSystem::get_singleton()->multimesh_set_mesh(multimesh, mesh->get_rid());
	} else {
		RenderingSystem::get_singleton()->multimesh_set_mesh(multimesh, RID());
	}
}

Ref<Mesh> MultiMesh::get_mesh() const {
	return mesh;
}

void MultiMesh::set_instance_count(int p_count) {
	ERR_FAIL_COND(p_count < 0);
	RenderingSystem::get_singleton()->multimesh_allocate_data(multimesh, p_count, RSE::MultimeshTransformFormat(transform_format), use_colors, use_custom_data);
	instance_count = p_count;
}

int MultiMesh::get_instance_count() const {
	return instance_count;
}

void MultiMesh::set_visible_instance_count(int p_count) {
	ERR_FAIL_COND(p_count < -1);
	ERR_FAIL_COND(p_count > instance_count);
	RenderingSystem::get_singleton()->multimesh_set_visible_instances(multimesh, p_count);
	visible_instance_count = p_count;
}

int MultiMesh::get_visible_instance_count() const {
	return visible_instance_count;
}

void MultiMesh::set_physics_interpolation_quality(PhysicsInterpolationQuality p_quality) {
	_physics_interpolation_quality = p_quality;
	RenderingSystem::get_singleton()->multimesh_set_physics_interpolation_quality(multimesh, (RSE::MultimeshPhysicsInterpolationQuality)p_quality);
}

void MultiMesh::set_instance_transform(int p_instance, const Transform3D &p_transform) {
	ERR_FAIL_INDEX_MSG(p_instance, instance_count, "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_MSG(transform_format == TRANSFORM_2D, "Can't set Transform3D on a Multimesh configured to use Transform2D. Ensure that you have set the `transform_format` to `TRANSFORM_3D`.");
	RenderingSystem::get_singleton()->multimesh_instance_set_transform(multimesh, p_instance, p_transform);
}

void MultiMesh::set_instance_transform_2d(int p_instance, const Transform2D &p_transform) {
	ERR_FAIL_INDEX_MSG(p_instance, instance_count, "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_MSG(transform_format == TRANSFORM_3D, "Can't set Transform2D on a Multimesh configured to use Transform3D. Ensure that you have set the `transform_format` to `TRANSFORM_2D`.");
	RenderingSystem::get_singleton()->multimesh_instance_set_transform_2d(multimesh, p_instance, p_transform);
	emit_changed();
}

Transform3D MultiMesh::get_instance_transform(int p_instance) const {
	ERR_FAIL_INDEX_V_MSG(p_instance, instance_count, Transform3D(), "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_V_MSG(transform_format == TRANSFORM_2D, Transform3D(), "Can't get Transform3D on a Multimesh configured to use Transform2D. Ensure that you have set the `transform_format` to `TRANSFORM_3D`.");
	return RenderingSystem::get_singleton()->multimesh_instance_get_transform(multimesh, p_instance);
}

Transform2D MultiMesh::get_instance_transform_2d(int p_instance) const {
	ERR_FAIL_INDEX_V_MSG(p_instance, instance_count, Transform2D(), "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_V_MSG(transform_format == TRANSFORM_3D, Transform2D(), "Can't get Transform2D on a Multimesh configured to use Transform3D. Ensure that you have set the `transform_format` to `TRANSFORM_2D`.");
	return RenderingSystem::get_singleton()->multimesh_instance_get_transform_2d(multimesh, p_instance);
}

void MultiMesh::set_instance_color(int p_instance, const Color &p_color) {
	ERR_FAIL_INDEX_MSG(p_instance, instance_count, "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_MSG(!use_colors, "Can't set instance color on a Multimesh that isn't using colors. Ensure that you have `use_colors` property of this Multimesh set to `true`.");
	RenderingSystem::get_singleton()->multimesh_instance_set_color(multimesh, p_instance, p_color);
}

Color MultiMesh::get_instance_color(int p_instance) const {
	ERR_FAIL_INDEX_V_MSG(p_instance, instance_count, Color(), "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_V_MSG(!use_colors, Color(), "Can't get instance color on a Multimesh that isn't using colors. Ensure that you have `use_colors` property of this Multimesh set to `true`.");
	return RenderingSystem::get_singleton()->multimesh_instance_get_color(multimesh, p_instance);
}

void MultiMesh::set_instance_custom_data(int p_instance, const Color &p_custom_data) {
	ERR_FAIL_INDEX_MSG(p_instance, instance_count, "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_MSG(!use_custom_data, "Can't get instance custom data on a Multimesh that isn't using custom data. Ensure that you have `use_custom_data` property of this Multimesh set to `true`.");
	RenderingSystem::get_singleton()->multimesh_instance_set_custom_data(multimesh, p_instance, p_custom_data);
}

Color MultiMesh::get_instance_custom_data(int p_instance) const {
	ERR_FAIL_INDEX_V_MSG(p_instance, instance_count, Color(), "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	ERR_FAIL_COND_V_MSG(!use_custom_data, Color(), "Can't get instance custom data on a Multimesh that isn't using custom data. Ensure that you have `use_custom_data` property of this Multimesh set to `true`.");
	return RenderingSystem::get_singleton()->multimesh_instance_get_custom_data(multimesh, p_instance);
}

void MultiMesh::reset_instance_physics_interpolation(int p_instance) {
	ERR_FAIL_INDEX_MSG(p_instance, instance_count, "Instance index out of bounds. Instance index must be less than `instance_count` and greater than or equal to zero.");
	RenderingSystem::get_singleton()->multimesh_instance_reset_physics_interpolation(multimesh, p_instance);
}

void MultiMesh::reset_instances_physics_interpolation() {
	RenderingSystem::get_singleton()->multimesh_instances_reset_physics_interpolation(multimesh);
}

void MultiMesh::set_physics_interpolated(bool p_interpolated) {
	RenderingSystem::get_singleton()->multimesh_set_physics_interpolated(multimesh, p_interpolated);
}

void MultiMesh::set_custom_aabb(const AABB &p_custom) {
	custom_aabb = p_custom;
	RS::get_singleton()->multimesh_set_custom_aabb(multimesh, custom_aabb);
	emit_changed();
}

AABB MultiMesh::get_custom_aabb() const {
	return custom_aabb;
}

AABB MultiMesh::get_aabb() const {
	return RenderingSystem::get_singleton()->multimesh_get_aabb(multimesh);
}

RID MultiMesh::get_rid() const {
	return multimesh;
}

void MultiMesh::set_use_colors(bool p_enable) {
	ERR_FAIL_COND_MSG(instance_count > 0, "Instance count must be 0 to toggle whether colors are used.");
	use_colors = p_enable;
}

bool MultiMesh::is_using_colors() const {
	return use_colors;
}

void MultiMesh::set_use_custom_data(bool p_enable) {
	ERR_FAIL_COND_MSG(instance_count > 0, "Instance count must be 0 to toggle whether custom data is used.");
	use_custom_data = p_enable;
}

bool MultiMesh::is_using_custom_data() const {
	return use_custom_data;
}

void MultiMesh::set_transform_format(TransformFormat p_transform_format) {
	ERR_FAIL_COND_MSG(instance_count > 0, "Instance count must be 0 to change the transform format.");
	transform_format = p_transform_format;
}

MultiMesh::TransformFormat MultiMesh::get_transform_format() const {
	return transform_format;
}

void MultiMesh::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_mesh", "mesh"), &MultiMesh::set_mesh);
	ClassRegistry::bind_method(D_METHOD("get_mesh"), &MultiMesh::get_mesh);
	ClassRegistry::bind_method(D_METHOD("set_use_colors", "enable"), &MultiMesh::set_use_colors);
	ClassRegistry::bind_method(D_METHOD("is_using_colors"), &MultiMesh::is_using_colors);
	ClassRegistry::bind_method(D_METHOD("set_use_custom_data", "enable"), &MultiMesh::set_use_custom_data);
	ClassRegistry::bind_method(D_METHOD("is_using_custom_data"), &MultiMesh::is_using_custom_data);
	ClassRegistry::bind_method(D_METHOD("set_transform_format", "format"), &MultiMesh::set_transform_format);
	ClassRegistry::bind_method(D_METHOD("get_transform_format"), &MultiMesh::get_transform_format);

	ClassRegistry::bind_method(D_METHOD("set_instance_count", "count"), &MultiMesh::set_instance_count);
	ClassRegistry::bind_method(D_METHOD("get_instance_count"), &MultiMesh::get_instance_count);
	ClassRegistry::bind_method(D_METHOD("set_visible_instance_count", "count"), &MultiMesh::set_visible_instance_count);
	ClassRegistry::bind_method(D_METHOD("get_visible_instance_count"), &MultiMesh::get_visible_instance_count);
	ClassRegistry::bind_method(D_METHOD("set_physics_interpolation_quality", "quality"), &MultiMesh::set_physics_interpolation_quality);
	ClassRegistry::bind_method(D_METHOD("get_physics_interpolation_quality"), &MultiMesh::get_physics_interpolation_quality);
	ClassRegistry::bind_method(D_METHOD("set_instance_transform", "instance", "transform"), &MultiMesh::set_instance_transform);
	ClassRegistry::bind_method(D_METHOD("set_instance_transform_2d", "instance", "transform"), &MultiMesh::set_instance_transform_2d);
	ClassRegistry::bind_method(D_METHOD("get_instance_transform", "instance"), &MultiMesh::get_instance_transform);
	ClassRegistry::bind_method(D_METHOD("get_instance_transform_2d", "instance"), &MultiMesh::get_instance_transform_2d);
	ClassRegistry::bind_method(D_METHOD("set_instance_color", "instance", "color"), &MultiMesh::set_instance_color);
	ClassRegistry::bind_method(D_METHOD("get_instance_color", "instance"), &MultiMesh::get_instance_color);
	ClassRegistry::bind_method(D_METHOD("set_instance_custom_data", "instance", "custom_data"), &MultiMesh::set_instance_custom_data);
	ClassRegistry::bind_method(D_METHOD("get_instance_custom_data", "instance"), &MultiMesh::get_instance_custom_data);
	ClassRegistry::bind_method(D_METHOD("reset_instance_physics_interpolation", "instance"), &MultiMesh::reset_instance_physics_interpolation);
	ClassRegistry::bind_method(D_METHOD("reset_instances_physics_interpolation"), &MultiMesh::reset_instances_physics_interpolation);
	ClassRegistry::bind_method(D_METHOD("set_custom_aabb", "aabb"), &MultiMesh::set_custom_aabb);
	ClassRegistry::bind_method(D_METHOD("get_custom_aabb"), &MultiMesh::get_custom_aabb);
	ClassRegistry::bind_method(D_METHOD("get_aabb"), &MultiMesh::get_aabb);

	ClassRegistry::bind_method(D_METHOD("get_buffer"), &MultiMesh::get_buffer);
	ClassRegistry::bind_method(D_METHOD("set_buffer", "buffer"), &MultiMesh::set_buffer);

	ClassRegistry::bind_method(D_METHOD("set_buffer_interpolated", "buffer_curr", "buffer_prev"), &MultiMesh::set_buffer_interpolated);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "transform_format", PROPERTY_HINT_ENUM, "2D,3D"), "set_transform_format", "get_transform_format");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "use_colors"), "set_use_colors", "is_using_colors");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "use_custom_data"), "set_use_custom_data", "is_using_custom_data");
	ADD_PROPERTY(PropertyInfo(Variant::AABB, "custom_aabb", PROPERTY_HINT_NONE, "suffix:m"), "set_custom_aabb", "get_custom_aabb");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "instance_count", PROPERTY_HINT_RANGE, "0,16384,1,or_greater"), "set_instance_count", "get_instance_count");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "visible_instance_count", PROPERTY_HINT_RANGE, "-1,16384,1,or_greater"), "set_visible_instance_count", "get_visible_instance_count");
	// Specify all types for explicit ordering in the inspector,
	// but list generic Mesh last to allow extended types to show up at the end.
	// Keep the order in sync with MeshInstance3D's `mesh` property.
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "mesh", PROPERTY_HINT_RESOURCE_TYPE, "BoxMesh,SphereMesh,CapsuleMesh,CylinderMesh,PrismMesh,TorusMesh,PlaneMesh,QuadMesh,TextMesh,RibbonTrailMesh,TubeTrailMesh,PointMesh,ArrayMesh,ImmediateMesh,PlaceholderMesh,Mesh", PROPERTY_USAGE_DEFAULT | PROPERTY_USAGE_EDITOR_INSTANTIATE_OBJECT), "set_mesh", "get_mesh");
	ADD_PROPERTY(PropertyInfo(Variant::PACKED_FLOAT32_ARRAY, "buffer", PROPERTY_HINT_NONE), "set_buffer", "get_buffer");

	ADD_GROUP("Physics Interpolation", "physics_interpolation");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "physics_interpolation_quality", PROPERTY_HINT_ENUM, "Fast,High"), "set_physics_interpolation_quality", "get_physics_interpolation_quality");

	BIND_ENUM_CONSTANT(TRANSFORM_2D);
	BIND_ENUM_CONSTANT(TRANSFORM_3D);

	BIND_ENUM_CONSTANT(INTERP_QUALITY_FAST);
	BIND_ENUM_CONSTANT(INTERP_QUALITY_HIGH);
}

MultiMesh::MultiMesh() {
	multimesh = RenderingSystem::get_singleton()->multimesh_create();
}

MultiMesh::~MultiMesh() {
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	RenderingSystem::get_singleton()->free_rid(multimesh);
}
