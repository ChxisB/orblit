/**************************************************************************/
/*  shape_spatial.cpp                                                          */

#include "shape_spatial.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "scene/stage/stage.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"
#include "systems/physics_spatial/physics_system_spatial_manager.h"

void Shape3D::add_vertices_to_array(Vector<Vector3> &array, const Transform3D &p_xform) {
	Vector<Vector3> toadd = get_debug_mesh_lines();

	if (toadd.size()) {
		int base = array.size();
		array.resize(base + toadd.size());
		Vector3 *w = array.ptrw();
		for (int i = 0; i < toadd.size(); i++) {
			w[i + base] = p_xform.xform(toadd[i]);
		}
	}
}

void Shape3D::set_custom_solver_bias(real_t p_bias) {
	custom_bias = p_bias;
	PhysicsSystem3D::get_singleton()->shape_set_custom_solver_bias(shape, custom_bias);
}

real_t Shape3D::get_custom_solver_bias() const {
	return custom_bias;
}

real_t Shape3D::get_margin() const {
	return margin;
}

void Shape3D::set_margin(real_t p_margin) {
	margin = p_margin;
	PhysicsSystem3D::get_singleton()->shape_set_margin(shape, margin);
}

void Shape3D::set_debug_color(const Color &p_color) {
	if (p_color == debug_color) {
		return;
	}

	debug_color = p_color;
#ifdef DEBUG_ENABLED
	debug_properties_edited = true;
#endif // DEBUG_ENABLED
	_update_shape();
}

Color Shape3D::get_debug_color() const {
	return debug_color;
}

void Shape3D::set_debug_fill(bool p_fill) {
	if (p_fill == debug_fill) {
		return;
	}

	debug_fill = p_fill;
#ifdef DEBUG_ENABLED
	debug_properties_edited = true;
#endif // DEBUG_ENABLED
	_update_shape();
}

bool Shape3D::get_debug_fill() const {
	return debug_fill;
}

Ref<ArrayMesh> Shape3D::get_debug_mesh() {
	if (debug_mesh_cache.is_valid()) {
		return debug_mesh_cache;
	}

	Vector<Vector3> lines = get_debug_mesh_lines();

	debug_mesh_cache.instantiate();

	if (!lines.is_empty()) {
		Vector<Color> colors;
		colors.resize(lines.size());
		colors.fill(debug_color);

		Array lines_array;
		lines_array.resize(Mesh::ARRAY_MAX);
		lines_array[Mesh::ARRAY_VERTEX] = lines;
		lines_array[Mesh::ARRAY_COLOR] = colors;

		debug_mesh_cache->add_surface_from_arrays(Mesh::PRIMITIVE_LINES, lines_array);

		Stage *scene_tree = Stage::get_singleton();
		if (scene_tree) {
			debug_mesh_cache->surface_set_material(0, scene_tree->get_debug_collision_material());
		}

		if (debug_fill) {
			Ref<ArrayMesh> array_mesh = get_debug_arraymesh_faces(debug_color * Color(1.0, 1.0, 1.0, 0.0625));
			if (array_mesh.is_valid() && array_mesh->get_surface_count() > 0) {
				Array solid_array = array_mesh->surface_get_arrays(0);
				debug_mesh_cache->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, solid_array);
				if (scene_tree) {
					debug_mesh_cache->surface_set_material(1, scene_tree->get_debug_collision_material());
				}
			}
		}
	}

	return debug_mesh_cache;
}

void Shape3D::_update_shape() {
	emit_changed();
	debug_mesh_cache.unref();
}

void Shape3D::_validate_property(PropertyInfo &p_property) const {
	if (p_property.name == "custom_solver_bias" && GLOBAL_GET(PhysicsSystem3DManager::setting_property_name) == PhysicsSystem3DManager::JOLT_PHYSICS_NAME) {
		// This property is not used by Jolt Physics. Hide it from the editor to avoid confusion.
		// Third-party physics engines may make use of this property, so we leave it visible for those.
		p_property.usage = PROPERTY_USAGE_STORAGE;
	}

	if (p_property.name == "margin" && GLOBAL_GET(PhysicsSystem3DManager::setting_property_name) == PhysicsSystem3DManager::ORBLIT_PHYSICS_3D_NAME) {
		// This property is not used by OrblitPhysics3D. Hide it from the editor to avoid confusion.
		// Third-party physics engines may make use of this property, so we leave it visible for those.
		p_property.usage = PROPERTY_USAGE_STORAGE;
	}
}

void Shape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_custom_solver_bias", "bias"), &Shape3D::set_custom_solver_bias);
	ClassRegistry::bind_method(D_METHOD("get_custom_solver_bias"), &Shape3D::get_custom_solver_bias);

	ClassRegistry::bind_method(D_METHOD("set_margin", "margin"), &Shape3D::set_margin);
	ClassRegistry::bind_method(D_METHOD("get_margin"), &Shape3D::get_margin);

	ClassRegistry::bind_method(D_METHOD("get_debug_mesh"), &Shape3D::get_debug_mesh);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "custom_solver_bias", PROPERTY_HINT_RANGE, "0,1,0.001"), "set_custom_solver_bias", "get_custom_solver_bias");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "margin", PROPERTY_HINT_RANGE, "0,10,0.001,or_greater,suffix:m"), "set_margin", "get_margin");
}

Shape3D::Shape3D() {
	ERR_PRINT("Default constructor must not be called!");
}

Shape3D::Shape3D(RID p_shape) :
		shape(p_shape) {}

Shape3D::~Shape3D() {
	ERR_FAIL_NULL(PhysicsSystem3D::get_singleton());
	PhysicsSystem3D::get_singleton()->free_rid(shape);
}
