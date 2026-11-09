/**************************************************************************/
/*  property_selector.h                                                   */

#pragma once

#include "scene/ui/dialogs.h"

class EditorHelpBit;
class FilterLineEdit;
class LineEdit;
class Tree;
class TreeItem;

class PropertySelector : public ConfirmationDialog {
	ORBLIT_CLASS(PropertySelector, ConfirmationDialog);

	FilterLineEdit *search_box = nullptr;
	Tree *search_options = nullptr;

	void _text_changed(const String &p_newtext);
	void _update_search();
	void _confirmed();
	void _item_selected();
	void _hide_requested();

	EditorHelpBit *help_bit = nullptr;

	bool properties = false;
	String selected;
	Variant::Type type = Variant::NIL;
	String base_type;
	ObjectID script;
	Object *instance = nullptr;
	bool virtuals_only = false;

	Vector<Variant::Type> type_filter;

	void _create_subproperties(TreeItem *p_parent_item, Variant::Type p_type);
	void _create_subproperty(TreeItem *p_parent_item, const String &p_name, Variant::Type p_type);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void select_method_from_base_type(const String &p_base, const String &p_current = "", bool p_virtuals_only = false);
	void select_method_from_script(const Ref<Script> &p_script, const String &p_current = "");
	void select_method_from_basic_type(Variant::Type p_type, const String &p_current = "");
	void select_method_from_instance(Object *p_instance, const String &p_current = "");

	void select_property_from_base_type(const String &p_base, const String &p_current = "");
	void select_property_from_script(const Ref<Script> &p_script, const String &p_current = "");
	void select_property_from_basic_type(Variant::Type p_type, const String &p_current = "");
	void select_property_from_instance(Object *p_instance, const String &p_current = "");

	void set_type_filter(const Vector<Variant::Type> &p_type_filter);

	PropertySelector();
};
