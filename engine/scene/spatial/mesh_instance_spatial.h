/**************************************************************************/
/*  mesh_instance_spatial.h                                                    */

#pragma once

#include "core/containers/local_vector.h"
#include "scene/resources/mesh.h"
#include "scene/spatial/visual_instance_spatial.h"

#ifndef NAVIGATION_SPATIAL_DISABLED
class NavigationMesh;
class NavigationMeshSourceGeometryData3D;
#endif // NAVIGATION_SPATIAL_DISABLED
class Skin;
class SkinReference;

class MeshInstance3D : public GeometryInstance3D {
	ORBLIT_CLASS(MeshInstance3D, GeometryInstance3D);

protected:
	Ref<Mesh> mesh;
	Ref<Skin> skin;
	Ref<Skin> skin_internal;
	Ref<SkinReference> skin_ref;
	EntityPath skeleton_path;

	LocalVector<float> blend_shape_tracks;
	HashMap<StringName, int> blend_shape_properties;
	Vector<Ref<Material>> surface_override_materials;

	void _mesh_changed();
	void _resolve_skeleton_path();

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	bool surface_index_0 = false;

	void _notification(int p_what);
	static void register_members();

	bool _property_can_revert(const StringName &p_name) const;
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const;

public:
	static constexpr AncestralClass static_ancestral_class = AncestralClass::MESH_INSTANCE_3D;

	void set_mesh(const Ref<Mesh> &p_mesh);
	Ref<Mesh> get_mesh() const;

	void set_skin(const Ref<Skin> &p_skin);
	Ref<Skin> get_skin() const;

	void set_skeleton_path(const EntityPath &p_skeleton);
	EntityPath get_skeleton_path();

	Ref<SkinReference> get_skin_reference() const;

	int get_blend_shape_count() const;
	int find_blend_shape_by_name(const StringName &p_name);
	float get_blend_shape_value(int p_blend_shape) const;
	void set_blend_shape_value(int p_blend_shape, float p_value);

	int get_surface_override_material_count() const;
	void set_surface_override_material(int p_surface, const Ref<Material> &p_material);
	Ref<Material> get_surface_override_material(int p_surface) const;
	Ref<Material> get_active_material(int p_surface) const;

#ifndef PHYSICS_SPATIAL_DISABLED
	Entity *create_trimesh_collision_node();
	void create_trimesh_collision();

	Entity *create_convex_collision_node(bool p_clean = true, bool p_simplify = false);
	void create_convex_collision(bool p_clean = true, bool p_simplify = false);

	Entity *create_multiple_convex_collisions_node(const Ref<MeshConvexDecompositionSettings> &p_settings = Ref<MeshConvexDecompositionSettings>());
	void create_multiple_convex_collisions(const Ref<MeshConvexDecompositionSettings> &p_settings = Ref<MeshConvexDecompositionSettings>());
#endif // PHYSICS_SPATIAL_DISABLED

	MeshInstance3D *create_debug_tangents_node();
	void create_debug_tangents();

	virtual AABB get_aabb() const override;

	Ref<ArrayMesh> bake_mesh_from_current_blend_shape_mix(Ref<ArrayMesh> p_existing = Ref<ArrayMesh>());
	Ref<ArrayMesh> bake_mesh_from_current_skeleton_pose(Ref<ArrayMesh> p_existing = Ref<ArrayMesh>());

	virtual Ref<TriangleMesh> generate_triangle_mesh() const override;

#ifndef NAVIGATION_SPATIAL_DISABLED
private:
	static Callable _navmesh_source_geometry_parsing_callback;
	static RID _navmesh_source_geometry_parser;
#endif // NAVIGATION_SPATIAL_DISABLED

public:
#ifndef NAVIGATION_SPATIAL_DISABLED
	static void navmesh_parse_init();
	static void navmesh_parse_source_geometry(const Ref<NavigationMesh> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, Entity *p_node);
#endif // NAVIGATION_SPATIAL_DISABLED

	virtual PackedStringArray get_configuration_warnings() const override;

	MeshInstance3D();
};
