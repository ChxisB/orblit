/**************************************************************************/
/*  skeleton_planar_editor_plugin.h                                           */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/skeleton_planar.h"

class AcceptDialog;
class MenuButton;

class Skeleton2DEditor : public Control {
	ORBLIT_CLASS(Skeleton2DEditor, Control);

	enum Menu {
		MENU_OPTION_SET_REST,
		MENU_OPTION_MAKE_REST,
	};

	Skeleton2D *node = nullptr;

	MenuButton *options = nullptr;
	AcceptDialog *err_dialog = nullptr;

	void _menu_option(int p_option);

	//void _create_uv_lines();
	friend class Skeleton2DEditorPlugin;

protected:
	void _node_removed(Entity *p_node);

public:
	void edit(Skeleton2D *p_sprite);
	Skeleton2DEditor();
};

class Skeleton2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Skeleton2DEditorPlugin, EditorPlugin);

	Skeleton2DEditor *sprite_editor = nullptr;

public:
	virtual String get_plugin_name() const override { return "Skeleton2D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	Skeleton2DEditorPlugin();
};
