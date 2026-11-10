/**************************************************************************/
/*  history_dock.h                                                        */

#pragma once

#include "editor/docks/editor_dock.h"

class CheckBox;
class ConfigFile;
class ItemList;
class EditorUndoRedoManager;

class HistoryDock : public EditorDock {
	ORBLIT_CLASS(HistoryDock, EditorDock);

	EditorUndoRedoManager *ur_manager;
	ItemList *action_list = nullptr;

	CheckBox *current_scene_checkbox = nullptr;
	CheckBox *global_history_checkbox = nullptr;

	bool need_refresh = true;
	int current_version = 0;

	void on_history_changed();
	void refresh_history();
	void on_version_changed();
	void refresh_version();

protected:
	void _notification(int p_notification);

	virtual void save_layout_to_config(Ref<ConfigFile> &p_layout, const String &p_section) const override;
	virtual void load_layout_from_config(const Ref<ConfigFile> &p_layout, const String &p_section) override;

public:
	void seek_history(int p_index);

	HistoryDock();
};
