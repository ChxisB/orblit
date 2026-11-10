/**************************************************************************/
/*  editor_translation_preview_button.cpp                                 */

#include "editor_translation_preview_button.h"

#include "core/object/callable_mp.h"
#include "core/string/translation_system.h"
#include "editor/editor_node.h"

void EditorTranslationPreviewButton::_update() {
	const String &locale = EditorNode::get_singleton()->get_preview_locale();
	bool pseudo = EditorNode::get_singleton()->is_pseudolocalization_enabled();

	if (!pseudo && locale.is_empty()) {
		hide();
		return;
	}

	if (!locale.is_empty() && locale != TranslationSystem::get_singleton()->get_fallback_locale()) {
		const String name = TranslationSystem::get_singleton()->get_locale_name(locale);
		set_text(vformat(TTR("Previewing: %s"), name == locale ? locale : name + " [" + locale + "]") + (pseudo ? " (" + TTR("pseudolocalized") + ")" : ""));
	} else {
		set_text(TTR("Previewing pseudolocalization"));
	}
	show();
}

void EditorTranslationPreviewButton::pressed() {
	EditorNode::get_singleton()->set_preview_locale(String(), false);
}

void EditorTranslationPreviewButton::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			set_button_icon(get_editor_theme_icon(SNAME("Translation")));
		} break;

		case NOTIFICATION_TRANSLATION_CHANGED: {
			_update();
		} break;

		case NOTIFICATION_START: {
			EditorNode::get_singleton()->connect("preview_locale_changed", callable_mp(this, &EditorTranslationPreviewButton::_update));
		} break;
	}
}

EditorTranslationPreviewButton::EditorTranslationPreviewButton() {
	set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	set_tooltip_auto_translate_mode(AUTO_TRANSLATE_MODE_ALWAYS);
	set_accessibility_name(TTRC("Disable Translation Preview"));
	set_tooltip_text(TTRC("Previewing translation. Click to disable."));
	set_focus_mode(FOCUS_NONE);
	set_visible(false);
}
