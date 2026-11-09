/**************************************************************************/
/*  animation_tree_editor_plugin.h                                        */

#pragma once

#include "editor/docks/editor_dock.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/animation/animation_tree.h"
#include "scene/ui/box_container.h"

class Button;
class EditorFileDialog;
class PanelContainer;
class ScrollContainer;
class RichTextLabel;

class AnimationTreeNodeEditorPlugin : public VBoxContainer {
	ORBLIT_CLASS(AnimationTreeNodeEditorPlugin, VBoxContainer);

public:
	virtual bool can_edit(const Ref<AnimationNode> &p_node) = 0;
	virtual void edit(const Ref<AnimationNode> &p_node) = 0;

private:
	String last_error_key;
};

class AnimationTreeEditor : public EditorDock {
	ORBLIT_CLASS(AnimationTreeEditor, EditorDock);

	ScrollContainer *path_edit = nullptr;
	HBoxContainer *path_hb = nullptr;
	HBoxContainer *status_bar = nullptr;
	RichTextLabel *current_scope_error_label = nullptr;
	Button *error_button = nullptr;
	ScrollContainer *error_scroll = nullptr;
	RichTextLabel *error_label = nullptr;

	AnimationTree *tree = nullptr;
	MarginContainer *editor_base = nullptr;

	Vector<String> button_path;
	Vector<String> edited_path;
	Vector<AnimationTreeNodeEditorPlugin *> editors;

	void _update_path();
	void _clear_editors();
	ObjectID current_root;

	void _path_button_pressed(int p_path);
	void _animation_list_changed();

	void _toggle_error_panel();
	void _update_error_message();

	static LocalVector<StringName> get_animation_list();

protected:
	void _meta_clicked(Variant p_meta);
	void _notification(int p_what);
	void _node_removed(Entity *p_node);

	static AnimationTreeEditor *singleton;

public:
	AnimationTree *get_animation_tree() { return tree; }
	void add_plugin(AnimationTreeNodeEditorPlugin *p_editor);
	void remove_plugin(AnimationTreeNodeEditorPlugin *p_editor);

	String get_base_path();

	bool can_edit(const Ref<AnimationNode> &p_node) const;

	void edit_path(const Vector<String> &p_path);
	Vector<String> get_edited_path() const;

	void enter_editor(const String &p_path = "");
	static AnimationTreeEditor *get_singleton() { return singleton; }
	void edit(AnimationTree *p_tree);
	AnimationTreeEditor();
};

class AnimationTreeEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(AnimationTreeEditorPlugin, EditorPlugin);

	AnimationTreeEditor *anim_tree_editor = nullptr;

public:
	virtual String get_plugin_name() const override { return "AnimationTree"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	AnimationTreeEditorPlugin();
};
