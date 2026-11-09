/**************************************************************************/
/*  ray_cast_spatial.cpp                                                       */

#include "ray_cast_spatial.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/spatial/physics/collision_object_spatial.h"
#include "scene/stage/stage.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/rendering/rendering_system.h"

void RayCast3D::set_target_position(const Vector3 &p_point) {
	target_position = p_point;
	update_gizmos();

	if (Engine::get_singleton()->is_editor_hint()) {
		if (is_inside_stage()) {
			_update_debug_shape_vertices();
		}
	} else if (debug_instance.is_valid()) {
		_update_debug_shape();
	}
}

Vector3 RayCast3D::get_target_position() const {
	return target_position;
}

void RayCast3D::set_collision_mask(uint32_t p_mask) {
	collision_mask = p_mask;
}

uint32_t RayCast3D::get_collision_mask() const {
	return collision_mask;
}

void RayCast3D::set_collision_mask_value(int p_layer_number, bool p_value) {
	ERR_FAIL_COND_MSG(p_layer_number < 1, "Collision layer number must be between 1 and 32 inclusive.");
	ERR_FAIL_COND_MSG(p_layer_number > 32, "Collision layer number must be between 1 and 32 inclusive.");
	uint32_t mask = get_collision_mask();
	if (p_value) {
		mask |= 1 << (p_layer_number - 1);
	} else {
		mask &= ~(1 << (p_layer_number - 1));
	}
	set_collision_mask(mask);
}

bool RayCast3D::get_collision_mask_value(int p_layer_number) const {
	ERR_FAIL_COND_V_MSG(p_layer_number < 1, false, "Collision layer number must be between 1 and 32 inclusive.");
	ERR_FAIL_COND_V_MSG(p_layer_number > 32, false, "Collision layer number must be between 1 and 32 inclusive.");
	return get_collision_mask() & (1 << (p_layer_number - 1));
}

bool RayCast3D::is_colliding() const {
	return collided;
}

Object *RayCast3D::get_collider() const {
	if (against.is_null()) {
		return nullptr;
	}

	return ObjectRegistry::get_instance(against);
}

RID RayCast3D::get_collider_rid() const {
	return against_rid;
}

int RayCast3D::get_collider_shape() const {
	return against_shape;
}

Vector3 RayCast3D::get_collision_point() const {
	return collision_point;
}

Vector3 RayCast3D::get_collision_normal() const {
	return collision_normal;
}

int RayCast3D::get_collision_face_index() const {
	return collision_face_index;
}

void RayCast3D::set_enabled(bool p_enabled) {
	enabled = p_enabled;
	update_gizmos();

	if (is_inside_stage() && !Engine::get_singleton()->is_editor_hint()) {
		set_internal_step_enabled(p_enabled);
	}
	if (!p_enabled) {
		collided = false;
	}

	if (is_inside_stage() && PhysicsSystem3D::get_singleton()->debug_is_enabled()) {
		if (p_enabled) {
			_update_debug_shape();
		} else {
			_clear_debug_shape();
		}
	}
}

bool RayCast3D::is_enabled() const {
	return enabled;
}

void RayCast3D::set_exclude_parent_body(bool p_exclude_parent_body) {
	if (exclude_parent_body == p_exclude_parent_body) {
		return;
	}

	exclude_parent_body = p_exclude_parent_body;

	if (!is_inside_stage()) {
		return;
	}

	if (Object::cast_to<CollisionObject3D>(get_parent())) {
		if (exclude_parent_body) {
			exclude.insert(Object::cast_to<CollisionObject3D>(get_parent())->get_rid());
		} else {
			exclude.erase(Object::cast_to<CollisionObject3D>(get_parent())->get_rid());
		}
	}
}

bool RayCast3D::get_exclude_parent_body() const {
	return exclude_parent_body;
}

void RayCast3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (Engine::get_singleton()->is_editor_hint()) {
				_update_debug_shape_vertices();
			}
			if (enabled && !Engine::get_singleton()->is_editor_hint()) {
				set_internal_step_enabled(true);
			} else {
				set_internal_step_enabled(false);
			}

			if (PhysicsSystem3D::get_singleton()->debug_is_enabled()) {
				_update_debug_shape();
			}

			if (Object::cast_to<CollisionObject3D>(get_parent())) {
				if (exclude_parent_body) {
					exclude.insert(Object::cast_to<CollisionObject3D>(get_parent())->get_rid());
				} else {
					exclude.erase(Object::cast_to<CollisionObject3D>(get_parent())->get_rid());
				}
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (enabled) {
				set_internal_step_enabled(false);
			}

			if (debug_instance.is_valid()) {
				_clear_debug_shape();
			}
		} break;

		case NOTIFICATION_VISIBILITY_CHANGED: {
			if (is_inside_stage() && debug_instance.is_valid()) {
				RenderingSystem::get_singleton()->instance_set_visible(debug_instance, is_visible_in_stage());
			}
		} break;

		case NOTIFICATION_INTERNAL_STEP: {
			if (!enabled) {
				break;
			}

			bool prev_collision_state = collided;
			_update_raycast_state();
			if (PhysicsSystem3D::get_singleton()->debug_is_enabled()) {
				if (prev_collision_state != collided) {
					_update_debug_shape_material(true);
				}
				if (is_inside_stage() && debug_instance.is_valid()) {
					RenderingSystem::get_singleton()->instance_set_transform(debug_instance, get_global_transform());
				}
			}
		} break;
	}
}

void RayCast3D::_update_raycast_state() {
	Ref<World3D> w3d = get_world_3d();
	ERR_FAIL_COND(w3d.is_null());

	PhysicsDirectSpaceState3D *dss = PhysicsSystem3D::get_singleton()->space_get_direct_state(w3d->get_space());
	ERR_FAIL_NULL(dss);

	Transform3D gt = get_global_transform();

	Vector3 to = target_position;
	if (to == Vector3()) {
		to = Vector3(0, 0.01, 0);
	}

	PS3DT::RayParameters ray_params;
	ray_params.from = gt.get_origin();
	ray_params.to = gt.xform(to);
	ray_params.exclude = exclude;
	ray_params.collision_mask = collision_mask;
	ray_params.collide_with_bodies = collide_with_bodies;
	ray_params.collide_with_areas = collide_with_areas;
	ray_params.hit_from_inside = hit_from_inside;
	ray_params.hit_back_faces = hit_back_faces;

	PS3DT::RayResult rr;
	if (dss->intersect_ray(ray_params, rr)) {
		collided = true;
		against = rr.collider_id;
		against_rid = rr.rid;
		collision_point = rr.position;
		collision_normal = rr.normal;
		collision_face_index = rr.face_index;
		against_shape = rr.shape;
	} else {
		collided = false;
		against = ObjectID();
		against_rid = RID();
		against_shape = 0;
	}
}

void RayCast3D::force_raycast_update() {
	_update_raycast_state();
}

void RayCast3D::add_exception_rid(const RID &p_rid) {
	exclude.insert(p_rid);
}

void RayCast3D::add_exception(RequiredParam<const CollisionObject3D> p_node) {
	EXTRACT_PARAM_OR_FAIL_MSG(node, p_node, "The passed Entity must be an instance of CollisionObject3D.");
	add_exception_rid(node->get_rid());
}

void RayCast3D::remove_exception_rid(const RID &p_rid) {
	exclude.erase(p_rid);
}

void RayCast3D::remove_exception(RequiredParam<const CollisionObject3D> p_node) {
	EXTRACT_PARAM_OR_FAIL_MSG(node, p_node, "The passed Entity must be an instance of CollisionObject3D.");
	remove_exception_rid(node->get_rid());
}

void RayCast3D::clear_exceptions() {
	exclude.clear();

	if (exclude_parent_body && is_inside_stage()) {
		CollisionObject3D *parent = Object::cast_to<CollisionObject3D>(get_parent());
		if (parent) {
			exclude.insert(parent->get_rid());
		}
	}
}

void RayCast3D::set_collide_with_areas(bool p_enabled) {
	collide_with_areas = p_enabled;
}

bool RayCast3D::is_collide_with_areas_enabled() const {
	return collide_with_areas;
}

void RayCast3D::set_collide_with_bodies(bool p_enabled) {
	collide_with_bodies = p_enabled;
}

bool RayCast3D::is_collide_with_bodies_enabled() const {
	return collide_with_bodies;
}

void RayCast3D::set_hit_from_inside(bool p_enabled) {
	hit_from_inside = p_enabled;
}

bool RayCast3D::is_hit_from_inside_enabled() const {
	return hit_from_inside;
}

void RayCast3D::set_hit_back_faces(bool p_enabled) {
	hit_back_faces = p_enabled;
}

bool RayCast3D::is_hit_back_faces_enabled() const {
	return hit_back_faces;
}

void RayCast3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_enabled", "enabled"), &RayCast3D::set_enabled);
	ClassRegistry::bind_method(D_METHOD("is_enabled"), &RayCast3D::is_enabled);

	ClassRegistry::bind_method(D_METHOD("set_target_position", "local_point"), &RayCast3D::set_target_position);
	ClassRegistry::bind_method(D_METHOD("get_target_position"), &RayCast3D::get_target_position);

	ClassRegistry::bind_method(D_METHOD("is_colliding"), &RayCast3D::is_colliding);
	ClassRegistry::bind_method(D_METHOD("force_raycast_update"), &RayCast3D::force_raycast_update);

	ClassRegistry::bind_method(D_METHOD("get_collider"), &RayCast3D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &RayCast3D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &RayCast3D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collision_point"), &RayCast3D::get_collision_point);
	ClassRegistry::bind_method(D_METHOD("get_collision_normal"), &RayCast3D::get_collision_normal);
	ClassRegistry::bind_method(D_METHOD("get_collision_face_index"), &RayCast3D::get_collision_face_index);

	ClassRegistry::bind_method(D_METHOD("add_exception_rid", "rid"), &RayCast3D::add_exception_rid);
	ClassRegistry::bind_method(D_METHOD("add_exception", "node"), &RayCast3D::add_exception);

	ClassRegistry::bind_method(D_METHOD("remove_exception_rid", "rid"), &RayCast3D::remove_exception_rid);
	ClassRegistry::bind_method(D_METHOD("remove_exception", "node"), &RayCast3D::remove_exception);

	ClassRegistry::bind_method(D_METHOD("clear_exceptions"), &RayCast3D::clear_exceptions);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "mask"), &RayCast3D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &RayCast3D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask_value", "layer_number", "value"), &RayCast3D::set_collision_mask_value);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask_value", "layer_number"), &RayCast3D::get_collision_mask_value);

	ClassRegistry::bind_method(D_METHOD("set_exclude_parent_body", "mask"), &RayCast3D::set_exclude_parent_body);
	ClassRegistry::bind_method(D_METHOD("get_exclude_parent_body"), &RayCast3D::get_exclude_parent_body);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &RayCast3D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &RayCast3D::is_collide_with_areas_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &RayCast3D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &RayCast3D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_hit_from_inside", "enable"), &RayCast3D::set_hit_from_inside);
	ClassRegistry::bind_method(D_METHOD("is_hit_from_inside_enabled"), &RayCast3D::is_hit_from_inside_enabled);

	ClassRegistry::bind_method(D_METHOD("set_hit_back_faces", "enable"), &RayCast3D::set_hit_back_faces);
	ClassRegistry::bind_method(D_METHOD("is_hit_back_faces_enabled"), &RayCast3D::is_hit_back_faces_enabled);

	ClassRegistry::bind_method(D_METHOD("set_debug_shape_custom_color", "debug_shape_custom_color"), &RayCast3D::set_debug_shape_custom_color);
	ClassRegistry::bind_method(D_METHOD("get_debug_shape_custom_color"), &RayCast3D::get_debug_shape_custom_color);

	ClassRegistry::bind_method(D_METHOD("set_debug_shape_thickness", "debug_shape_thickness"), &RayCast3D::set_debug_shape_thickness);
	ClassRegistry::bind_method(D_METHOD("get_debug_shape_thickness"), &RayCast3D::get_debug_shape_thickness);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "enabled"), "set_enabled", "is_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "exclude_parent"), "set_exclude_parent_body", "get_exclude_parent_body");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "target_position", PROPERTY_HINT_NONE, "suffix:m"), "set_target_position", "get_target_position");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_3D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "hit_from_inside"), "set_hit_from_inside", "is_hit_from_inside_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "hit_back_faces"), "set_hit_back_faces", "is_hit_back_faces_enabled");

	ADD_GROUP("Collide With", "collide_with");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");

	ADD_GROUP("Debug Shape", "debug_shape");
	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "debug_shape_custom_color"), "set_debug_shape_custom_color", "get_debug_shape_custom_color");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "debug_shape_thickness", PROPERTY_HINT_RANGE, "1,5"), "set_debug_shape_thickness", "get_debug_shape_thickness");
}

int RayCast3D::get_debug_shape_thickness() const {
	return debug_shape_thickness;
}

void RayCast3D::_update_debug_shape_vertices() {
	debug_shape_vertices.clear();
	debug_line_vertices.clear();

	if (target_position == Vector3()) {
		return;
	}

	debug_line_vertices.push_back(Vector3());
	debug_line_vertices.push_back(target_position);

	if (debug_shape_thickness > 1) {
		float scale_factor = 100.0;
		Vector3 dir = Vector3(target_position).normalized();
		// Draw truncated pyramid
		Vector3 normal = (std::abs(dir.x) + std::abs(dir.y) > CMP_EPSILON) ? Vector3(-dir.y, dir.x, 0).normalized() : Vector3(0, -dir.z, dir.y).normalized();
		normal *= debug_shape_thickness / scale_factor;
		int vertices_strip_order[14] = { 4, 5, 0, 1, 2, 5, 6, 4, 7, 0, 3, 2, 7, 6 };
		for (int v = 0; v < 14; v++) {
			Vector3 vertex = vertices_strip_order[v] < 4 ? normal : normal / 3.0 + target_position;
			debug_shape_vertices.push_back(vertex.rotated(dir, Math::PI * (0.5 * (vertices_strip_order[v] % 4) + 0.25)));
		}
	}
}

void RayCast3D::set_debug_shape_thickness(const int p_debug_shape_thickness) {
	debug_shape_thickness = p_debug_shape_thickness;
	update_gizmos();

	if (Engine::get_singleton()->is_editor_hint()) {
		if (is_inside_stage()) {
			_update_debug_shape_vertices();
		}
	} else if (debug_instance.is_valid()) {
		_update_debug_shape();
	}
}

const Vector<Vector3> &RayCast3D::get_debug_shape_vertices() const {
	return debug_shape_vertices;
}

const Vector<Vector3> &RayCast3D::get_debug_line_vertices() const {
	return debug_line_vertices;
}

void RayCast3D::set_debug_shape_custom_color(const Color &p_color) {
	debug_shape_custom_color = p_color;
	if (debug_material.is_valid()) {
		_update_debug_shape_material();
	}
}

Ref<StandardMaterial3D> RayCast3D::get_debug_material() {
	_update_debug_shape_material();
	return debug_material;
}

const Color &RayCast3D::get_debug_shape_custom_color() const {
	return debug_shape_custom_color;
}

void RayCast3D::_create_debug_shape() {
	_update_debug_shape_material();

	if (!debug_instance.is_valid()) {
		debug_instance = RenderingSystem::get_singleton()->instance_create();
	}

	if (debug_mesh.is_null()) {
		debug_mesh.instantiate();
	}
}

void RayCast3D::_update_debug_shape_material(bool p_check_collision) {
	if (debug_material.is_null()) {
		Ref<StandardMaterial3D> material = memnew(StandardMaterial3D);
		debug_material = material;

		material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
		material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
		// Use double-sided rendering so that the RayCast can be seen if the camera is inside.
		material->set_cull_mode(BaseMaterial3D::CULL_DISABLED);
		material->set_transparency(BaseMaterial3D::TRANSPARENCY_ALPHA);
	}

	Color color = debug_shape_custom_color;
	if (color == Color(0.0, 0.0, 0.0)) {
		// Use the default debug shape color defined in the Project Settings.
		color = get_stage()->get_debug_collisions_color();
	}

	if (p_check_collision && collided) {
		if ((color.get_h() < 0.055 || color.get_h() > 0.945) && color.get_s() > 0.5 && color.get_v() > 0.5) {
			// If base color is already quite reddish, highlight collision with green color
			color = Color(0.0, 1.0, 0.0, color.a);
		} else {
			// Else, highlight collision with red color
			color = Color(1.0, 0, 0, color.a);
		}
	}

	Ref<StandardMaterial3D> material = static_cast<Ref<StandardMaterial3D>>(debug_material);
	material->set_albedo(color);
}

void RayCast3D::_update_debug_shape() {
	if (!enabled) {
		return;
	}

	if (!debug_instance.is_valid()) {
		_create_debug_shape();
	}

	if (!debug_instance.is_valid() || debug_mesh.is_null()) {
		return;
	}

	_update_debug_shape_vertices();

	debug_mesh->clear_surfaces();

	Array a;
	a.resize(Mesh::ARRAY_MAX);

	uint32_t flags = 0;
	int surface_count = 0;

	if (!debug_line_vertices.is_empty()) {
		a[Mesh::ARRAY_VERTEX] = debug_line_vertices;
		debug_mesh->add_surface_from_arrays(Mesh::PRIMITIVE_LINES, a, Array(), Dictionary(), flags);
		debug_mesh->surface_set_material(surface_count, debug_material);
		++surface_count;
	}

	if (!debug_shape_vertices.is_empty()) {
		a[Mesh::ARRAY_VERTEX] = debug_shape_vertices;
		debug_mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLE_STRIP, a, Array(), Dictionary(), flags);
		debug_mesh->surface_set_material(surface_count, debug_material);
		++surface_count;
	}

	RenderingSystem::get_singleton()->instance_set_base(debug_instance, debug_mesh->get_rid());
	if (is_inside_stage()) {
		RenderingSystem::get_singleton()->instance_set_scenario(debug_instance, get_world_3d()->get_scenario());
		RenderingSystem::get_singleton()->instance_set_visible(debug_instance, is_visible_in_stage());
		RenderingSystem::get_singleton()->instance_set_transform(debug_instance, get_global_transform());
	}
}

void RayCast3D::_clear_debug_shape() {
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	if (debug_instance.is_valid()) {
		RenderingSystem::get_singleton()->free_rid(debug_instance);
		debug_instance = RID();
	}
	if (debug_mesh.is_valid()) {
		RenderingSystem::get_singleton()->free_rid(debug_mesh->get_rid());
		debug_mesh = Ref<ArrayMesh>();
	}
}

void RayCast3D::_physics_debug_changed() {
	if (PhysicsSystem3D::get_singleton()->debug_is_enabled()) {
		_update_debug_shape();
	} else {
		_clear_debug_shape();
	}
}

RayCast3D::RayCast3D() {
#ifdef DEBUG_ENABLED
	PhysicsSystem3D::get_singleton()->connect("_debug_changed", callable_mp(this, &RayCast3D::_physics_debug_changed));
#endif
}
