/**************************************************************************/
/*  editor_translation_preview_button.h                                   */

#pragma once

#include "scene/ui/button.h"

class EditorTranslationPreviewButton : public Button {
	ORBLIT_CLASS(EditorTranslationPreviewButton, Button);

	void _update();

protected:
	virtual void pressed() override;

	void _notification(int p_what);

public:
	EditorTranslationPreviewButton();
};
