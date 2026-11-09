/**************************************************************************/
/*  mesh_instance_spatial_editor_plugin.h                                      */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/spatial/mesh_instance_spatial.h"
#include "scene/ui/option_button.h"

class AcceptDialog;
class AspectRatioContainer;
class ConfirmationDialog;
class MenuButton;
class SpinBox;

class MeshInstance3DEditor : public Control {
	ORBLIT_CLASS(MeshInstance3DEditor, Control);

	enum Menu {
		MENU_OPTION_CREATE_COLLISION_SHAPE,
		MENU_OPTION_CREATE_NAVMESH,
		MENU_OPTION_CREATE_OUTLINE_MESH,
		MENU_OPTION_CREATE_DEBUG_TANGENTS,
		MENU_OPTION_CREATE_UV2,
		MENU_OPTION_DEBUG_UV1,
		MENU_OPTION_DEBUG_UV2,
	};

	enum ShapePlacement {
		SHAPE_PLACEMENT_SIBLING,
		SHAPE_PLACEMENT_STATIC_BODY_CHILD,
	};

	enum ShapeType {
		SHAPE_TYPE_TRIMESH,
		SHAPE_TYPE_SINGLE_CONVEX,
		SHAPE_TYPE_SIMPLIFIED_CONVEX,
		SHAPE_TYPE_MULTIPLE_CONVEX,
		SHAPE_TYPE_BOUNDING_BOX,
		SHAPE_TYPE_CAPSULE,
		SHAPE_TYPE_CYLINDER,
		SHAPE_TYPE_SPHERE,
		SHAPE_TYPE_PRIMITIVE,
	};

	enum ShapeAxis {
		SHAPE_AXIS_X,
		SHAPE_AXIS_Y,
		SHAPE_AXIS_Z,
		SHAPE_AXIS_LONGEST,
	};

	MeshInstance3D *node = nullptr;

	MenuButton *options = nullptr;

	ConfirmationDialog *outline_dialog = nullptr;
	SpinBox *outline_size = nullptr;

	ConfirmationDialog *shape_dialog = nullptr;
	OptionButton *shape_type = nullptr;
	OptionButton *shape_placement = nullptr;
	Label *shape_axis_label = nullptr;
	OptionButton *shape_axis = nullptr;
	Transform3D shape_offset_transform;

	AcceptDialog *err_dialog = nullptr;

	AcceptDialog *debug_uv_dialog = nullptr;
	AspectRatioContainer *debug_uv_arc = nullptr;
	Control *debug_uv = nullptr;
	Vector<Vector2> uv_lines;

	ConfirmationDialog *navigation_mesh_dialog = nullptr;

	void _shape_dialog_about_to_popup();
	void _shape_type_selected(int p_option);
	void _create_collision_shape();
	Vector<Ref<Shape3D>> create_shape_from_mesh(Ref<Mesh> p_mesh, int p_option, bool p_verbose);
	void _menu_option(int p_option);
	void _create_outline_mesh();
	void _create_navigation_mesh();

	void _create_uv_lines(int p_layer);
	friend class MeshInstance3DEditorPlugin;

	void _debug_uv_draw();

protected:
	void _node_removed(Entity *p_node);

	void _notification(int p_what);

public:
	void edit(MeshInstance3D *p_mesh);
	MeshInstance3DEditor();
};

class MeshInstance3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(MeshInstance3DEditorPlugin, EditorPlugin);

	MeshInstance3DEditor *mesh_editor = nullptr;

public:
	virtual String get_plugin_name() const override { return "MeshInstance3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	MeshInstance3DEditorPlugin();
};
