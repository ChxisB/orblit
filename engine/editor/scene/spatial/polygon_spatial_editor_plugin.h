/**************************************************************************/
/*  polygon_spatial_editor_plugin.h                                            */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/ui/box_container.h"

class ArrayMesh;
class CanvasItemEditor;
class ImmediateMesh;
class MenuButton;
class MeshInstance3D;
class StandardMaterial3D;

class Polygon3DEditor : public HBoxContainer {
	ORBLIT_CLASS(Polygon3DEditor, HBoxContainer);

	enum Mode {
		MODE_CREATE,
		MODE_EDIT,

	};

	Mode mode;

	Button *button_create = nullptr;
	Button *button_edit = nullptr;

	Ref<StandardMaterial3D> line_material;
	Ref<StandardMaterial3D> handle_material;

	Panel *panel = nullptr;
	Entity3D *node = nullptr;
	Ref<Resource> node_resource;
	Ref<ImmediateMesh> imesh;
	MeshInstance3D *imgeom = nullptr;
	MeshInstance3D *pointsm = nullptr;
	Ref<ArrayMesh> m;

	int edited_point = 0;
	Vector2 edited_point_pos;
	PackedVector2Array pre_move_edit;
	PackedVector2Array wip;
	bool wip_active;
	bool snap_ignore;

	float prev_depth = 0.0f;

	void _wip_close();
	void _polygon_draw();
	void _menu_option(int p_option);

	float _get_depth();
	PackedVector2Array _get_polygon();
	void _set_polygon(const PackedVector2Array &p_poly);

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);
	static void register_members();

public:
	virtual EditorPlugin::AfterGUIInput forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event);
	void edit(Entity *p_node);
	Polygon3DEditor();
	~Polygon3DEditor();
};

class Polygon3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Polygon3DEditorPlugin, EditorPlugin);

	Polygon3DEditor *polygon_editor = nullptr;

public:
	virtual EditorPlugin::AfterGUIInput forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event) override { return polygon_editor->forward_3d_gui_input(p_camera, p_event); }

	virtual String get_plugin_name() const override { return "Polygon3DEditor"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	Polygon3DEditorPlugin();
};
