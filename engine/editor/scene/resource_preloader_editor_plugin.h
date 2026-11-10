/**************************************************************************/
/*  resource_preloader_editor_plugin.h                                    */

#pragma once

#include "editor/docks/editor_dock.h"
#include "editor/plugins/editor_plugin.h"

class AcceptDialog;
class Button;
class FilterLineEdit;
class EditorFileDialog;
class ResourcePreloader;
class Tree;

class ResourcePreloaderEditor : public EditorDock {
	ORBLIT_CLASS(ResourcePreloaderEditor, EditorDock);

	enum {
		BUTTON_OPEN_SCENE,
		BUTTON_EDIT_RESOURCE,
		BUTTON_REMOVE
	};

	Button *load = nullptr;
	Button *paste = nullptr;
	FilterLineEdit *search = nullptr;
	MarginContainer *mc = nullptr;
	Tree *tree = nullptr;

	bool loading_scene = false;

	EditorFileDialog *file = nullptr;

	AcceptDialog *dialog = nullptr;

	ResourcePreloader *preloader = nullptr;

	void _load_pressed();
	void _files_load_request(const Vector<String> &p_paths);
	void _paste_pressed();
	void _search_text_changed(const String &p_new_text) const;
	void _remove_resource(const String &p_to_remove);
	void _update_library();
	void _cell_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);
	void _item_edited();

	Variant get_drag_data_fw(const Point2 &p_point, Control *p_from);
	bool can_drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from) const;
	void drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from);

protected:
	void _notification(int p_what);
	static void register_members();

	virtual void update_layout(EditorDock::DockLayout p_layout, int p_slot) override;

public:
	void edit(ResourcePreloader *p_preloader);
	ResourcePreloaderEditor();
};

class ResourcePreloaderEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(ResourcePreloaderEditorPlugin, EditorPlugin);

	ResourcePreloaderEditor *preloader_editor = nullptr;

public:
	virtual String get_plugin_name() const override { return "ResourcePreloader"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	ResourcePreloaderEditorPlugin();
};
