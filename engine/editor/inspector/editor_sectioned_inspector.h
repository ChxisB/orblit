/**************************************************************************/
/*  editor_sectioned_inspector.h                                          */

#pragma once

#include "scene/ui/split_container.h"

class CheckButton;
class EditorInspector;
class LineEdit;
class SectionedInspectorFilter;
class Tree;
class TreeItem;

class SectionedInspector : public HSplitContainer {
	ORBLIT_CLASS(SectionedInspector, HSplitContainer);

	ObjectID obj;

	Tree *sections = nullptr;
	SectionedInspectorFilter *filter = nullptr;

	HashMap<String, TreeItem *> section_map;
	EditorInspector *inspector = nullptr;
	LineEdit *search_box = nullptr;
	CheckButton *advanced_toggle = nullptr;

	String selected_category;

	bool restrict_to_basic = false;

	static void register_members();
	void _section_selected();

	void _search_changed(const String &p_what);
	void _advanced_toggled(bool p_toggled_on);

protected:
	void _notification(int p_notification);

public:
	void register_search_box(LineEdit *p_box);
	void register_advanced_toggle(CheckButton *p_toggle);

	EditorInspector *get_inspector();
	void edit(Object *p_object);
	String get_full_item_path(const String &p_item);

	void set_current_section(const String &p_section);
	String get_current_section() const;

	void update_category_list();

	SectionedInspector();
	~SectionedInspector();
};
