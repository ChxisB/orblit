/**************************************************************************/
/*  project_settings_native_extension.h                                   */

#pragma once

#include "scene/ui/box_container.h"

class Tree;

class ProjectSettingsNativeExtension : public VBoxContainer {
	ORBLIT_CLASS(ProjectSettingsNativeExtension, VBoxContainer);

	enum {
		COLUMN_PATH,
		COLUMN_MIN_VERSION,
		COLUMN_MAX_VERSION,
		COLUMN_RELOAD,
		COLUMN_MAX,
	};

	Tree *extension_list = nullptr;

	void _cell_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);
	void _on_item_activated();
	void _update_extension_tree();

protected:
	void _notification(int p_what);

public:
	ProjectSettingsNativeExtension();
};
