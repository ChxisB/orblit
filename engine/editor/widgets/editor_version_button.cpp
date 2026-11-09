/**************************************************************************/
/*  editor_version_button.cpp                                             */

#include "editor_version_button.h"

#include "core/os/time.h"
#include "core/version.h"
#include "editor/widgets/editor_toaster.h"
#include "systems/display/display_system.h"

String _get_version_string(EditorVersionButton::VersionFormat p_format) {
	String main;
	switch (p_format) {
		case EditorVersionButton::FORMAT_BASIC: {
			return ORBLIT_VERSION_FULL_CONFIG;
		} break;
		case EditorVersionButton::FORMAT_WITH_BUILD: {
			main = "v" ORBLIT_VERSION_FULL_BUILD;
		} break;
		case EditorVersionButton::FORMAT_WITH_NAME_AND_BUILD: {
			main = ORBLIT_VERSION_FULL_NAME;
		} break;
		default: {
			ERR_FAIL_V_MSG(ORBLIT_VERSION_FULL_NAME, "Unexpected format: " + itos(p_format));
		} break;
	}

	String hash = ORBLIT_VERSION_HASH;
	if (!hash.is_empty()) {
		hash = vformat(" [%s]", hash.left(9));
	}
	return main + hash;
}

void EditorVersionButton::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_POSTINITIALIZE: {
			// This can't be done in the constructor because theme cache is not ready yet.
			set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
			set_text(_get_version_string(format));
		} break;

		case NOTIFICATION_TRANSLATION_CHANGED: {
			String build_date;
			if (ORBLIT_VERSION_TIMESTAMP > 0) {
				build_date = Time::get_singleton()->get_datetime_string_from_unix_time(ORBLIT_VERSION_TIMESTAMP, true) + " UTC";
			} else {
				build_date = TTR("(unknown)");
			}
			set_tooltip_text(vformat(TTR("Git commit date: %s\nClick to copy the version information."), build_date));
		} break;
	}
}

void EditorVersionButton::pressed() {
	DisplaySystem::get_singleton()->clipboard_set(_get_version_string(FORMAT_WITH_BUILD));
	EditorToaster *toaster = EditorToaster::get_singleton();
	if (toaster) {
		toaster->popup_str(TTR("Copied Orblit editor version."));
	}
}

EditorVersionButton::EditorVersionButton(VersionFormat p_format) {
	format = p_format;
	set_underline_mode(LinkButton::UNDERLINE_MODE_ON_HOVER);
}
