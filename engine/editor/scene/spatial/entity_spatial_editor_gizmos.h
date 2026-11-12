/**************************************************************************/
/*  entity_spatial_editor_gizmos.h                                             */

#pragma once

#include "core/containers/hash_map.h"
#include "core/math/dynamic_bvh.h"
#include "scene/resources/mesh.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/spatial/entity_spatial.h"
#include "scene/spatial/skeleton_spatial.h"

class Timer;
class EditorEntity3DGizmoPlugin;

class EditorEntity3DGizmo : public Entity3DGizmo {
	ORBLIT_CLASS(EditorEntity3DGizmo, Entity3DGizmo);

	struct Instance {
		RID instance;
		Ref<Mesh> mesh;
		Ref<Material> material;
		Ref<SkinReference> skin_reference;
		bool extra_margin = false;
		Transform3D xform;

		void create_instance(Entity3D *p_base, bool p_hidden = false);
	};

	bool selected;

	Vector<Vector3> collision_segments;
	LocalVector<Ref<TriangleMesh>> collision_meshes;
	bool collision_meshes_are_snap_source = false;

	Vector<Vector3> handles;
	Vector<int> handle_ids;
	Vector<Vector3> secondary_handles;
	Vector<int> secondary_handle_ids;

	real_t selectable_icon_size;
	bool billboard_handle;

	bool valid;
	bool hidden;
	Vector<Instance> instances;
	Entity3D *spatial_node = nullptr;

	DynamicBVH::ID bvh_node_id;

	void _set_node_3d(Entity *p_node) { set_entity_3d(Object::cast_to<Entity3D>(p_node)); }

	void _update_bvh();
	bool _is_selectable() const;

protected:
	static void register_members();

	EditorEntity3DGizmoPlugin *gizmo_plugin = nullptr;

	ORBLIT_VIRTUAL0(_redraw)
	ORBLIT_VIRTUAL2RC(String, _get_handle_name, int, bool)
	ORBLIT_VIRTUAL2RC(bool, _is_handle_highlighted, int, bool)
	ORBLIT_VIRTUAL2RC(Variant, _get_handle_value, int, bool)
	ORBLIT_VIRTUAL2(_begin_handle_action, int, bool)
	ORBLIT_VIRTUAL4(_set_handle, int, bool, const Camera3D *, Vector2)
	ORBLIT_VIRTUAL4(_commit_handle, int, bool, Variant, bool)

	ORBLIT_VIRTUAL2RC(int, _subgizmos_intersect_ray, const Camera3D *, Vector2)
	ORBLIT_VIRTUAL2RC(Vector<int>, _subgizmos_intersect_frustum, const Camera3D *, TypedArray<Plane>)
	ORBLIT_VIRTUAL1RC(Transform3D, _get_subgizmo_transform, int)
	ORBLIT_VIRTUAL2(_set_subgizmo_transform, int, Transform3D)
	ORBLIT_VIRTUAL3(_commit_subgizmos, Vector<int>, TypedArray<Transform3D>, bool)
public:
	void add_lines(const Vector<Vector3> &p_lines, const Ref<Material> &p_material, bool p_billboard = false, const Color &p_modulate = Color(1, 1, 1));
	void add_vertices(const Vector<Vector3> &p_vertices, const Ref<Material> &p_material, Mesh::PrimitiveType p_primitive_type, bool p_billboard = false, const Color &p_modulate = Color(1, 1, 1));
	void add_mesh(const Ref<Mesh> &p_mesh, const Ref<Material> &p_material = Ref<Material>(), const Transform3D &p_xform = Transform3D(), const Ref<SkinReference> &p_skin_reference = Ref<SkinReference>());
	void add_collision_segments(const Vector<Vector3> &p_lines);
	void add_collision_triangles(const Ref<TriangleMesh> &p_tmesh);
	void set_collision_meshes_are_snap_source(bool p_enable) { collision_meshes_are_snap_source = p_enable; }
	bool get_collision_meshes_are_snap_source() const { return collision_meshes_are_snap_source; }
	const LocalVector<Ref<TriangleMesh>> &get_collision_meshes() const { return collision_meshes; }
	const Vector<Vector3> &get_collision_segments() const { return collision_segments; }
	void add_unscaled_billboard(const Ref<Material> &p_material, real_t p_scale = 1, const Color &p_modulate = Color(1, 1, 1));
	void add_handles(const Vector<Vector3> &p_handles, const Ref<Material> &p_material, const Vector<int> &p_ids = Vector<int>(), bool p_billboard = false, bool p_secondary = false);
	void add_solid_box(const Ref<Material> &p_material, Vector3 p_size, Vector3 p_position = Vector3(), const Transform3D &p_xform = Transform3D());

	virtual bool is_handle_highlighted(int p_id, bool p_secondary) const;
	virtual String get_handle_name(int p_id, bool p_secondary) const;
	virtual Variant get_handle_value(int p_id, bool p_secondary) const;
	virtual void begin_handle_action(int p_id, bool p_secondary);
	virtual void set_handle(int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point);
	virtual void commit_handle(int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false);

	virtual int subgizmos_intersect_ray(Camera3D *p_camera, const Vector2 &p_point) const;
	virtual Vector<int> subgizmos_intersect_frustum(const Camera3D *p_camera, const Vector<Plane> &p_frustum) const;
	virtual Transform3D get_subgizmo_transform(int p_id) const;
	virtual void set_subgizmo_transform(int p_id, Transform3D p_transform);
	virtual void commit_subgizmos(const Vector<int> &p_ids, const Vector<Transform3D> &p_restore, bool p_cancel = false);

	void set_selected(bool p_selected) { selected = p_selected; }
	bool is_selected() const { return selected; }

	void set_entity_3d(Entity3D *p_node);
	Entity3D *get_entity_3d() const { return spatial_node; }
	Ref<EditorEntity3DGizmoPlugin> get_plugin() const { return gizmo_plugin; }
	bool intersect_frustum(const Camera3D *p_camera, const Vector<Plane> &p_frustum);
	void handles_intersect_ray(Camera3D *p_camera, const Vector2 &p_point, bool p_shift_pressed, int &r_id, bool &r_secondary);
	bool intersect_ray(Camera3D *p_camera, const Point2 &p_point, Vector3 &r_pos, Vector3 &r_normal);
	bool is_subgizmo_selected(int p_id) const;
	Vector<int> get_subgizmo_selection() const;

	virtual void clear() override;
	virtual void create() override;
	virtual void transform() override;
	virtual void redraw() override;
	virtual void free() override;

	virtual bool is_editable() const;

	void set_hidden(bool p_hidden);
	void set_plugin(EditorEntity3DGizmoPlugin *p_plugin);

	EditorEntity3DGizmo();
	~EditorEntity3DGizmo();
};

class EditorEntity3DGizmoPlugin : public Resource {
	ORBLIT_CLASS(EditorEntity3DGizmoPlugin, Resource);

public:
	static const int VISIBLE = 0;
	static const int HIDDEN = 1;
	static const int ON_TOP = 2;

protected:
	int current_state;
	HashSet<EditorEntity3DGizmo *> current_gizmos;
	HashMap<String, Vector<Ref<StandardMaterial3D>>> materials;

	static void register_members();
	virtual bool has_gizmo(Entity3D *p_spatial);
	virtual Ref<EditorEntity3DGizmo> create_gizmo(Entity3D *p_spatial);

	ORBLIT_VIRTUAL1RC(bool, _has_gizmo, Entity3D *)
	ORBLIT_VIRTUAL1RC(Ref<EditorEntity3DGizmo>, _create_gizmo, Entity3D *)

	ORBLIT_VIRTUAL0RC(String, _get_gizmo_name)
	ORBLIT_VIRTUAL0RC(int, _get_priority)
	ORBLIT_VIRTUAL0RC(bool, _can_be_hidden)
	ORBLIT_VIRTUAL0RC(bool, _is_selectable_when_hidden)
	ORBLIT_VIRTUAL0RC(bool, _can_commit_handle_on_click)

	ORBLIT_VIRTUAL1(_redraw, Ref<EditorEntity3DGizmo>)
	ORBLIT_VIRTUAL3RC(String, _get_handle_name, Ref<EditorEntity3DGizmo>, int, bool)
	ORBLIT_VIRTUAL3RC(bool, _is_handle_highlighted, Ref<EditorEntity3DGizmo>, int, bool)
	ORBLIT_VIRTUAL3RC(Variant, _get_handle_value, Ref<EditorEntity3DGizmo>, int, bool)

	ORBLIT_VIRTUAL3(_begin_handle_action, Ref<EditorEntity3DGizmo>, int, bool)
	ORBLIT_VIRTUAL5(_set_handle, Ref<EditorEntity3DGizmo>, int, bool, const Camera3D *, Vector2)
	ORBLIT_VIRTUAL5(_commit_handle, Ref<EditorEntity3DGizmo>, int, bool, Variant, bool)

	ORBLIT_VIRTUAL3RC(int, _subgizmos_intersect_ray, Ref<EditorEntity3DGizmo>, const Camera3D *, Vector2)
	ORBLIT_VIRTUAL3RC(Vector<int>, _subgizmos_intersect_frustum, Ref<EditorEntity3DGizmo>, const Camera3D *, TypedArray<Plane>)
	ORBLIT_VIRTUAL2RC(Transform3D, _get_subgizmo_transform, Ref<EditorEntity3DGizmo>, int)
	ORBLIT_VIRTUAL3(_set_subgizmo_transform, Ref<EditorEntity3DGizmo>, int, Transform3D)
	ORBLIT_VIRTUAL4(_commit_subgizmos, Ref<EditorEntity3DGizmo>, Vector<int>, TypedArray<Transform3D>, bool)

public:
	void create_material(const String &p_name, const Color &p_color, bool p_billboard = false, bool p_on_top = false, bool p_use_vertex_color = false);
	void create_icon_material(const String &p_name, const Ref<Texture2D> &p_texture, bool p_on_top = false, const Color &p_albedo = Color(1, 1, 1, 1));
	void create_handle_material(const String &p_name, bool p_billboard = false, const Ref<Texture2D> &p_texture = nullptr);
	void add_material(const String &p_name, Ref<StandardMaterial3D> p_material);

	Ref<StandardMaterial3D> get_material(const String &p_name, const Ref<EditorEntity3DGizmo> &p_gizmo = Ref<EditorEntity3DGizmo>());

	virtual String get_gizmo_name() const;
	virtual int get_priority() const;
	virtual bool can_be_hidden() const;
	virtual bool is_selectable_when_hidden() const;
	virtual bool can_commit_handle_on_click() const;

	virtual void redraw(EditorEntity3DGizmo *p_gizmo);
	virtual bool is_handle_highlighted(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const;
	virtual String get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const;
	virtual Variant get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const;
	virtual void begin_handle_action(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary);
	virtual void set_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point);
	virtual void commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false);

	virtual int subgizmos_intersect_ray(const EditorEntity3DGizmo *p_gizmo, Camera3D *p_camera, const Vector2 &p_point) const;
	virtual Vector<int> subgizmos_intersect_frustum(const EditorEntity3DGizmo *p_gizmo, const Camera3D *p_camera, const Vector<Plane> &p_frustum) const;
	virtual Transform3D get_subgizmo_transform(const EditorEntity3DGizmo *p_gizmo, int p_id) const;
	virtual void set_subgizmo_transform(const EditorEntity3DGizmo *p_gizmo, int p_id, Transform3D p_transform);
	virtual void commit_subgizmos(const EditorEntity3DGizmo *p_gizmo, const Vector<int> &p_ids, const Vector<Transform3D> &p_restore, bool p_cancel = false);

	Ref<EditorEntity3DGizmo> get_gizmo(Entity3D *p_spatial);
	void set_state(int p_state);
	int get_state() const;
	void unregister_gizmo(EditorEntity3DGizmo *p_gizmo);

	EditorEntity3DGizmoPlugin();
	virtual ~EditorEntity3DGizmoPlugin();
};
