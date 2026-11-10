/**************************************************************************/
/*  gltf_physics_shape.h                                                  */

#pragma once

#include "../../gltf_defines.h"

#include "scene/spatial/physics/collision_shape_spatial.h"

class ImporterMesh;

// GLTFPhysicsShape is an intermediary between Orblit's collision shape nodes
// and the OMI_physics_shape extension.
// https://github.com/omigroup/gltf-extensions/tree/main/extensions/2.0/OMI_physics_shape

class GLTFPhysicsShape : public Resource {
	ORBLIT_CLASS(GLTFPhysicsShape, Resource)

protected:
	static void register_members();

private:
	String shape_type;
	Vector3 size = Vector3(1.0, 1.0, 1.0);
	real_t radius = 0.5;
	real_t height = 2.0;
	bool is_trigger = false;
	GLTFMeshIndex mesh_index = -1;
	Ref<ImporterMesh> importer_mesh = nullptr;
	// Internal only, for caching Orblit shape resources. Used in `to_resource` and `to_node`.
	Ref<Shape3D> _shape_cache = nullptr;

public:
	String get_shape_type() const;
	void set_shape_type(const String &p_shape_type);

	Vector3 get_size() const;
	void set_size(const Vector3 &p_size);

	real_t get_radius() const;
	void set_radius(real_t p_radius);

	real_t get_height() const;
	void set_height(real_t p_height);

	bool get_is_trigger() const;
	void set_is_trigger(bool p_is_trigger);

	GLTFMeshIndex get_mesh_index() const;
	void set_mesh_index(GLTFMeshIndex p_mesh_index);

	Ref<ImporterMesh> get_importer_mesh() const;
	void set_importer_mesh(const Ref<ImporterMesh> &p_importer_mesh);

	static Ref<GLTFPhysicsShape> from_node(const CollisionShape3D *p_shape_node);
	CollisionShape3D *to_node(bool p_cache_shapes = false);

	static Ref<GLTFPhysicsShape> from_resource(const Ref<Shape3D> &p_shape_resource);
	Ref<Shape3D> to_resource(bool p_cache_shapes = false);

	static Ref<GLTFPhysicsShape> from_dictionary(const Dictionary &p_dictionary);
	Dictionary to_dictionary() const;
};
