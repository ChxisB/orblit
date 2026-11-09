/**************************************************************************/
/*  translation_domain.h                                                  */

#pragma once

#include "core/object/ref_counted.h"

class Translation;

class TranslationDomain : public RefCounted {
	ORBLIT_CLASS(TranslationDomain, RefCounted);

	struct PseudolocalizationConfig {
		bool enabled = false;
		bool accents_enabled = true;
		bool double_vowels_enabled = false;
		bool fake_bidi_enabled = false;
		bool override_enabled = false;
		bool skip_placeholders_enabled = true;
		float expansion_ratio = 0.0;
		String prefix = "[";
		String suffix = "]";
	};

	bool enabled = true;

	String locale_override;
	HashSet<Ref<Translation>> translations;
	PseudolocalizationConfig pseudolocalization;

	String _get_override_string(const String &p_message) const;
	String _double_vowels(const String &p_message) const;
	String _replace_with_accented_string(const String &p_message) const;
	String _wrap_with_fakebidi_characters(const String &p_message) const;
	String _add_padding(const String &p_message, int p_length) const;
	const char32_t *_get_accented_version(char32_t p_character) const;
	bool _is_placeholder(const String &p_message, int p_index) const;

protected:
	static void register_members();

public:
	// Methods in this section are not intended for scripting.
	StringName get_message_from_translations(const String &p_locale, const StringName &p_message, const StringName &p_context) const;
	StringName get_message_from_translations(const String &p_locale, const StringName &p_message, const StringName &p_message_plural, int p_n, const StringName &p_context) const;
	PackedStringArray get_loaded_locales() const;

public:
	// These two methods are public for easier TranslationSystem bindings.
	TypedArray<Translation> get_translations_bind() const;
	TypedArray<Translation> find_translations_bind(const String &p_locale, bool p_exact) const;

	void add_translation(const Ref<Translation> &p_translation);
	void remove_translation(const Ref<Translation> &p_translation);
	void clear();

	bool has_translation(const Ref<Translation> &p_translation) const;
	const HashSet<Ref<Translation>> get_translations() const;
	HashSet<Ref<Translation>> find_translations(const String &p_locale, bool p_exact) const;
	bool has_translation_for_locale(const String &p_locale, bool p_exact) const;

	StringName translate(const StringName &p_message, const StringName &p_context) const;
	StringName translate_plural(const StringName &p_message, const StringName &p_message_plural, int p_n, const StringName &p_context) const;

	String get_locale_override() const;
	void set_locale_override(const String &p_locale);

	bool is_enabled() const;
	void set_enabled(bool p_enabled);

	bool is_pseudolocalization_enabled() const;
	void set_pseudolocalization_enabled(bool p_enabled);
	bool is_pseudolocalization_accents_enabled() const;
	void set_pseudolocalization_accents_enabled(bool p_enabled);
	bool is_pseudolocalization_double_vowels_enabled() const;
	void set_pseudolocalization_double_vowels_enabled(bool p_enabled);
	bool is_pseudolocalization_fake_bidi_enabled() const;
	void set_pseudolocalization_fake_bidi_enabled(bool p_enabled);
	bool is_pseudolocalization_override_enabled() const;
	void set_pseudolocalization_override_enabled(bool p_enabled);
	bool is_pseudolocalization_skip_placeholders_enabled() const;
	void set_pseudolocalization_skip_placeholders_enabled(bool p_enabled);
	float get_pseudolocalization_expansion_ratio() const;
	void set_pseudolocalization_expansion_ratio(float p_ratio);
	String get_pseudolocalization_prefix() const;
	void set_pseudolocalization_prefix(const String &p_prefix);
	String get_pseudolocalization_suffix() const;
	void set_pseudolocalization_suffix(const String &p_suffix);

	StringName pseudolocalize(const StringName &p_message) const;
};
