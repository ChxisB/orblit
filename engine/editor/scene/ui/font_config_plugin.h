/**************************************************************************/
/*  font_config_plugin.h                                                  */

#pragma once

#include "editor/inspector/editor_properties.h"
#include "editor/inspector/editor_properties_array_dict.h"
#include "editor/plugins/editor_plugin.h"

/*************************************************************************/

class EditorPropertyFontMetaObject : public RefCounted {
	ORBLIT_CLASS(EditorPropertyFontMetaObject, RefCounted);

	Dictionary dict;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;

public:
	void set_dict(const Dictionary &p_dict);
	Dictionary get_dict();
};

/*************************************************************************/

class EditorPropertyFontOTObject : public RefCounted {
	ORBLIT_CLASS(EditorPropertyFontOTObject, RefCounted);

	Dictionary dict;
	Dictionary defaults_dict;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	bool _property_can_revert(const StringName &p_name) const;
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const;

public:
	void set_dict(const Dictionary &p_dict);
	Dictionary get_dict();

	void set_defaults(const Dictionary &p_dict);
	Dictionary get_defaults();
};

/*************************************************************************/

class EditorPropertyFontMetaOverride : public EditorProperty {
	ORBLIT_CLASS(EditorPropertyFontMetaOverride, EditorProperty);

	Ref<EditorPropertyFontMetaObject> object;

	MarginContainer *container = nullptr;
	VBoxContainer *property_vbox = nullptr;

	Button *edit = nullptr;
	PopupMenu *menu = nullptr;
	EditorLocaleDialog *locale_select = nullptr;

	Vector<String> script_codes;

	bool script_editor = false;
	bool updating = false;
	int page_length = 20;
	int page_index = 0;
	EditorPaginator *paginator = nullptr;

protected:
	void _edit_pressed();
	void _page_changed(int p_page);
	void _property_changed(const String &p_property, const Variant &p_value, const String &p_name = "", bool p_changing = false);
	void _remove(Object *p_button, const String &p_key);
	void _add_menu();
	void _add_script(int p_option);
	void _add_lang(const String &p_locale);
	void _object_id_selected(const StringName &p_property, ObjectID p_id);

public:
	virtual void update_property() override;

	EditorPropertyFontMetaOverride(bool p_script);
};

/*************************************************************************/

class EditorPropertyOTVariation : public EditorProperty {
	ORBLIT_CLASS(EditorPropertyOTVariation, EditorProperty);

	Ref<EditorPropertyFontOTObject> object;

	MarginContainer *container = nullptr;
	VBoxContainer *property_vbox = nullptr;

	Button *edit = nullptr;

	bool updating = false;
	int page_length = 20;
	int page_index = 0;
	EditorPaginator *paginator = nullptr;

protected:
	void _edit_pressed();
	void _page_changed(int p_page);
	void _property_changed(const String &p_property, const Variant &p_value, const String &p_name = "", bool p_changing = false);
	void _object_id_selected(const StringName &p_property, ObjectID p_id);

public:
	virtual void update_property() override;

	EditorPropertyOTVariation();
};

/*************************************************************************/

class EditorPropertyOTFeatures : public EditorProperty {
	ORBLIT_CLASS(EditorPropertyOTFeatures, EditorProperty);

	enum FeatureGroups {
		FGRP_STYLISTIC_SET,
		FGRP_CHARACTER_VARIANT,
		FGRP_CAPITLS,
		FGRP_LIGATURES,
		FGRP_ALTERNATES,
		FGRP_EAL,
		FGRP_EAW,
		FGRP_NUMAL,
		FGRP_CUSTOM,
		FGRP_MAX,
	};

	Ref<EditorPropertyFontOTObject> object;

	MarginContainer *container = nullptr;
	VBoxContainer *property_vbox = nullptr;

	Button *edit = nullptr;
	PopupMenu *menu = nullptr;
	PopupMenu *menu_sub[FGRP_MAX];
	String group_names[FGRP_MAX];

	bool updating = false;
	int page_length = 20;
	int page_index = 0;
	EditorPaginator *paginator = nullptr;

protected:
	void _edit_pressed();
	void _page_changed(int p_page);
	void _property_changed(const String &p_property, const Variant &p_value, const String &p_name = "", bool p_changing = false);
	void _remove(Object *p_button, int p_key);
	void _add_menu();
	void _add_feature(int p_option);
	void _object_id_selected(const StringName &p_property, ObjectID p_id);

public:
	virtual void update_property() override;

	EditorPropertyOTFeatures();
};

/*************************************************************************/

class EditorInspectorPluginFontVariation : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginFontVariation, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual bool parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide = false) override;
};

/*************************************************************************/

class FontPreview : public Control {
	ORBLIT_CLASS(FontPreview, Control);

protected:
	void _notification(int p_what);

	Ref<Font> prev_font;

	void _preview_changed();

public:
	virtual Size2 get_minimum_size() const override;

	void set_data(const Ref<Font> &p_f);

	FontPreview();
};

/*************************************************************************/

class EditorInspectorPluginFontPreview : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginFontPreview, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
	virtual bool parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide = false) override;
};

/*************************************************************************/

class EditorPropertyFontNamesArray : public EditorPropertyArray {
	ORBLIT_CLASS(EditorPropertyFontNamesArray, EditorPropertyArray);

	PopupMenu *menu = nullptr;

protected:
	virtual void _add_element() override;

	void _add_font(int p_option);

public:
	EditorPropertyFontNamesArray();
};

/*************************************************************************/

class EditorInspectorPluginSystemFont : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginSystemFont, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual bool parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide = false) override;
};

/*************************************************************************/

class FontEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(FontEditorPlugin, EditorPlugin);

public:
	FontEditorPlugin();

	virtual String get_plugin_name() const override { return "Font"; }
};
