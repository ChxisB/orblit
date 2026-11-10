/**************************************************************************/
/*  theme_classic.h                                                       */

#pragma once

#include "editor/themes/editor_theme_manager.h"

class ThemeClassic {
public:
	static void populate_shared_styles(const Ref<EditorTheme> &p_theme, EditorThemeManager::ThemeConfiguration &p_config);
	static void populate_standard_styles(const Ref<EditorTheme> &p_theme, EditorThemeManager::ThemeConfiguration &p_config);
	static void populate_editor_styles(const Ref<EditorTheme> &p_theme, EditorThemeManager::ThemeConfiguration &p_config);
};
