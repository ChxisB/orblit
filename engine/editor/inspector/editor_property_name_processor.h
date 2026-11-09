/**************************************************************************/
/*  editor_property_name_processor.h                                      */

#pragma once

#include "scene/stage/entity.h"

class EditorPropertyNameProcessor : public Entity {
	ORBLIT_CLASS(EditorPropertyNameProcessor, Entity);

	static EditorPropertyNameProcessor *singleton;

	mutable HashMap<String, String> capitalize_string_cache;
	HashMap<String, String> capitalize_string_remaps;
	LocalVector<String> stop_words; // Exceptions that shouldn't be capitalized.

	HashMap<String, HashMap<String, StringName>> translation_contexts;

	// Capitalizes property path segments.
	String _capitalize_name(const String &p_name) const;

	// Returns the translation context for the given name.
	StringName _get_context(const String &p_name, const String &p_property, const StringName &p_class) const;

public:
	// Matches `interface/inspector/default_property_name_style` editor setting.
	enum Style {
		STYLE_RAW,
		STYLE_CAPITALIZED,
		STYLE_LOCALIZED,
	};

	static EditorPropertyNameProcessor *get_singleton() { return singleton; }

	static Style get_default_inspector_style();
	static Style get_settings_style();
	static Style get_tooltip_style(Style p_style);

	static bool is_localization_available();

	// Turns property path segment into the given style.
	// `p_class` and `p_property` are only used for `STYLE_LOCALIZED`, associating the name with a translation context.
	String process_name(const String &p_name, Style p_style, const String &p_property = "", const StringName &p_class = "") const;

	// Translate plain text group names.
	String translate_group_name(const String &p_name) const;

	EditorPropertyNameProcessor();
	~EditorPropertyNameProcessor();
};
