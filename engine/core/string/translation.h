/**************************************************************************/
/*  translation.h                                                         */

#pragma once

#include "core/io/resource.h"
#include "core/object/orblit_virtual.gen.h"

class PluralRules;

class Translation : public Resource {
	ORBLIT_CLASS(Translation, Resource);
	OBJ_SAVE_TYPE(Translation);
	RES_BASE_EXTENSION("translation");

public:
	struct MessageKey {
		StringName msgctxt;
		StringName msgid;

		// Required to use this struct as a key in HashMap.
		static uint32_t hash(const MessageKey &p_key) {
			uint32_t h = hash_murmur3_one_32(HashMapHasherDefault::hash(p_key.msgctxt));
			return hash_fmix32(hash_murmur3_one_32(HashMapHasherDefault::hash(p_key.msgid), h));
		}
		bool operator==(const MessageKey &p_key) const {
			return msgctxt == p_key.msgctxt && msgid == p_key.msgid;
		}
	};

private:
	String locale = "en";

	HashMap<MessageKey, Vector<StringName>, MessageKey> translation_map;

	mutable PluralRules *plural_rules_cache = nullptr;
	String plural_rules_override;

	virtual Vector<String> _get_message_list() const;

	// For data storage.
	virtual Dictionary _get_messages() const;
	virtual void _set_messages(const Dictionary &p_messages);

protected:
	static void register_members();

	PluralRules *_get_plural_rules() const;

	ORBLIT_VIRTUAL2RC(StringName, _get_message, StringName, StringName);
	ORBLIT_VIRTUAL4RC(StringName, _get_plural_message, StringName, StringName, int, StringName);

public:
	void set_locale(const String &p_locale);
	_FORCE_INLINE_ String get_locale() const { return locale; }

	virtual void add_message(const StringName &p_src_text, const StringName &p_xlated_text, const StringName &p_context = "");
	virtual void add_plural_message(const StringName &p_src_text, const Vector<String> &p_plural_xlated_texts, const StringName &p_context = "");
	virtual StringName get_message(const StringName &p_src_text, const StringName &p_context = "") const; //overridable for other implementations
	virtual StringName get_plural_message(const StringName &p_src_text, const StringName &p_plural_text, int p_n, const StringName &p_context = "") const;
	virtual void erase_message(const StringName &p_src_text, const StringName &p_context = "");
	virtual void get_message_list(List<StringName> *r_messages) const;
	virtual int get_message_count() const;
	virtual Vector<String> get_translated_message_list() const;

	void set_plural_rules_override(const String &p_rules);
	String get_plural_rules_override() const;

	// This method is not exposed to scripting intentionally. It is only used by TranslationLoaderPO and tests.
	int get_nplurals() const;

	~Translation();
};
