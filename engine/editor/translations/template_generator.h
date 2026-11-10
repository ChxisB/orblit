/**************************************************************************/
/*  template_generator.h                                                  */

#pragma once

#include "core/io/file_access.h"
#include "core/string/translation.h"

class TranslationTemplateGenerator {
	static inline TranslationTemplateGenerator *singleton = nullptr;

	struct MessageData {
		String plural;
		HashSet<String> locations;
		HashSet<String> comments;
	};

	using MessageMap = HashMap<Translation::MessageKey, MessageData, Translation::MessageKey>;

	MessageMap parse(const Vector<String> &p_sources, bool p_add_builtin, bool p_add_title) const;

	void _write_to_pot(Ref<FileAccess> p_file, const MessageMap &p_map) const;
	void _write_to_csv(Ref<FileAccess> p_file, const MessageMap &p_map) const;

public:
	static TranslationTemplateGenerator *get_singleton();

	void generate(const String &p_file);

	~TranslationTemplateGenerator();
};
