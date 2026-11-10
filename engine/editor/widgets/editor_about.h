/**************************************************************************/
/*  editor_about.h                                                        */

#pragma once

#include "scene/ui/dialogs.h"

class Label;
class RichTextLabel;
class TextureRect;

/**
 * NOTE: Do not assume the EditorNode singleton to be available in this class' methods.
 * EditorAbout is also used from the project manager where EditorNode isn't initialized.
 */
class EditorAbout : public AcceptDialog {
	ORBLIT_CLASS(EditorAbout, AcceptDialog);

private:
	Label *_about_text_label = nullptr;
	RichTextLabel *license_text_label = nullptr;
	TextureRect *_logo = nullptr;

protected:
	void _notification(int p_what);

public:
	EditorAbout();
};
