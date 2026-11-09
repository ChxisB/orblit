/**************************************************************************/
/*  root_motion_editor_plugin.h                                           */

#pragma once

#include "editor/inspector/editor_inspector.h"

class Button;
class ConfirmationDialog;
class Tree;

class EditorPropertyRootMotion : public EditorProperty {
	ORBLIT_CLASS(EditorPropertyRootMotion, EditorProperty);

	Button *assign = nullptr;
	Button *clear = nullptr;
	EntityPath base_hint;

	ConfirmationDialog *filter_dialog = nullptr;
	Tree *filters = nullptr;

	void _confirmed();
	void _node_assign();
	void _node_clear();

protected:
	void _notification(int p_what);

public:
	virtual void update_property() override;
	void setup(const EntityPath &p_base_hint);
	EditorPropertyRootMotion();
};

class EditorInspectorRootMotionPlugin : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorRootMotionPlugin, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual bool parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide = false) override;
};
