/**************************************************************************/
/*  soft_body_spatial.h                                                        */

#pragma once

#include "scene/spatial/mesh_instance_spatial.h"
#include "systems/physics_spatial/physics_system_spatial.h"

class PhysicsBody3D;

class SoftBodyRenderingSystemHandler : public PhysicsSystem3DRenderingSystemHandler {
	friend class SoftBody3D;

	RID mesh;
	int surface = 0;
	Vector<uint8_t> buffer;
	uint32_t stride = 0;
	uint32_t normal_stride = 0;
	uint32_t offset_vertices = 0;
	uint32_t offset_normal = 0;

	uint8_t *write_buffer = nullptr;

private:
	SoftBodyRenderingSystemHandler();
	bool is_ready(RID p_mesh_rid) const { return mesh.is_valid() && mesh == p_mesh_rid; }
	void prepare(RID p_mesh_rid, int p_surface);
	void clear();
	void open();
	void close();
	void commit_changes();

public:
	void set_vertex(int p_vertex_id, const Vector3 &p_vertex) override;
	void set_normal(int p_vertex_id, const Vector3 &p_normal) override;
	void set_aabb(const AABB &p_aabb) override;
};

class SoftBody3D : public MeshInstance3D {
	ORBLIT_CLASS(SoftBody3D, MeshInstance3D);

public:
	enum DisableMode {
		DISABLE_MODE_REMOVE,
		DISABLE_MODE_KEEP_ACTIVE,
	};

	struct PinnedPoint {
		int point_index = -1;
		EntityPath spatial_attachment_path;
		Entity3D *spatial_attachment = nullptr; // Cache
		Vector3 offset;

		PinnedPoint();
		PinnedPoint(const PinnedPoint &obj_tocopy);
		void operator=(const PinnedPoint &obj);
	};

private:
	SoftBodyRenderingSystemHandler *rendering_system_handler = nullptr;

	RID physics_rid;

	DisableMode disable_mode = DISABLE_MODE_REMOVE;

	RID owned_mesh;
	uint32_t collision_mask = 1;
	uint32_t collision_layer = 1;
	EntityPath parent_collision_ignore;
	Vector<PinnedPoint> pinned_points;
	bool simulation_started = false;
	bool pinned_points_cache_dirty = true;

	Ref<ArrayMesh> debug_mesh_cache;
	class MeshInstance3D *debug_mesh = nullptr;

	bool capture_input_on_drag = false;
	bool ray_pickable = true;

	void _update_pickable();

	void _update_physics_system();
	void _draw_soft_mesh();

	void _prepare_physics_system();
	void _become_mesh_owner();

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	bool _set_property_pinned_points_indices(const Array &p_indices);
	bool _set_property_pinned_points_attachment(int p_item, const String &p_what, const Variant &p_value);
	bool _get_property_pinned_points(int p_item, const String &p_what, Variant &r_ret) const;

	void _notification(int p_what);
	static void register_members();

	PackedStringArray get_configuration_warnings() const override;

public:
	RID get_physics_rid() const { return physics_rid; }

	void set_collision_mask(uint32_t p_mask);
	uint32_t get_collision_mask() const;

	void set_collision_layer(uint32_t p_layer);
	uint32_t get_collision_layer() const;

	void set_collision_layer_value(int p_layer_number, bool p_value);
	bool get_collision_layer_value(int p_layer_number) const;

	void set_collision_mask_value(int p_layer_number, bool p_value);
	bool get_collision_mask_value(int p_layer_number) const;

	void set_disable_mode(DisableMode p_mode);
	DisableMode get_disable_mode() const;

	void set_parent_collision_ignore(const EntityPath &p_parent_collision_ignore);
	const EntityPath &get_parent_collision_ignore() const;
	int get_point_count() const;

	void set_pinned_points_indices(Vector<PinnedPoint> p_pinned_points_indices);
	Vector<PinnedPoint> get_pinned_points_indices();

	void set_simulation_precision(int p_simulation_precision);
	int get_simulation_precision();

	void set_total_mass(real_t p_total_mass);
	real_t get_total_mass();

	void set_linear_stiffness(real_t p_linear_stiffness);
	real_t get_linear_stiffness();

	void set_shrinking_factor(real_t p_shrinking_factor);
	real_t get_shrinking_factor();

	void set_pressure_coefficient(real_t p_pressure_coefficient);
	real_t get_pressure_coefficient();

	void set_damping_coefficient(real_t p_damping_coefficient);
	real_t get_damping_coefficient();

	void set_drag_coefficient(real_t p_drag_coefficient);
	real_t get_drag_coefficient();

	TypedArray<PhysicsBody3D> get_collision_exceptions();
	void add_collision_exception_with(RequiredParam<Entity> p_node);
	void remove_collision_exception_with(RequiredParam<Entity> p_node);

	Vector3 get_point_transform(int p_point_index);

	void pin_point(int p_point_index, bool pin, const EntityPath &p_spatial_attachment_path = EntityPath(), int p_insert_at = -1);
	bool is_point_pinned(int p_point_index) const;

	void _pin_point_deferred(int p_point_index, bool pin, const EntityPath p_spatial_attachment_path);

	void set_ray_pickable(bool p_ray_pickable);
	bool is_ray_pickable() const;

	void apply_impulse(int p_point_index, const Vector3 &p_impulse);
	void apply_force(int p_point_index, const Vector3 &p_force);
	void apply_central_impulse(const Vector3 &p_impulse);
	void apply_central_force(const Vector3 &p_force);

	SoftBody3D();
	~SoftBody3D();

private:
	void _make_cache_dirty();
	void _update_cache_pin_points_datas();

	void _pin_point_on_physics_system(int p_point_index, bool pin);
	void _add_pinned_point(int p_point_index, const EntityPath &p_spatial_attachment_path, int p_insert_at = -1);
	void _reset_points_offsets();

	void _remove_pinned_point(int p_point_index);
	int _get_pinned_point(int p_point_index, PinnedPoint *&r_point) const;
	int _has_pinned_point(int p_point_index) const;
};

VARIANT_ENUM_CAST(SoftBody3D::DisableMode);
