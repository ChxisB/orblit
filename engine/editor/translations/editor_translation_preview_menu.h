/**************************************************************************/
/*  editor_translation_preview_menu.h                                     */

#pragma once

#include "scene/ui/popup_menu.h"

class EditorTranslationPreviewMenu : public PopupMenu {
	ORBLIT_CLASS(EditorTranslationPreviewMenu, PopupMenu);

	static constexpr int MENU_ID_PSEUDOLOCALIZATION = 1;

	void _prepare();
	void _pressed(int p_index);

protected:
	void _notification(int p_what);

public:
	EditorTranslationPreviewMenu();
};
