/**************************************************************************/
/*  editor_validation_panel.h                                             */

#pragma once

#include "scene/ui/panel_container.h"

class Button;
class Label;
class VBoxContainer;

class EditorValidationPanel : public PanelContainer {
	ORBLIT_CLASS(EditorValidationPanel, PanelContainer);

public:
	enum MessageType {
		MSG_OK,
		MSG_WARNING,
		MSG_ERROR,
		MSG_INFO,
	};

	static const int MSG_ID_DEFAULT = 0; // Avoids hard-coding ID in dialogs with single-line validation.

private:
	VBoxContainer *message_container = nullptr;

	HashMap<int, String> valid_messages;
	HashMap<int, Label *> labels;

	bool valid = false;
	bool pending_update = false;

	struct ThemeCache {
		Color valid_color;
		Color warning_color;
		Color error_color;
	} theme_cache;

	void _update();

	Callable update_callback;
	Button *accept_button = nullptr;

protected:
	void _notification(int p_what);

public:
	void add_line(int p_id, const String &p_valid_message = "");
	void set_accept_button(Button *p_button);
	void set_update_callback(const Callable &p_callback);

	void update();
	void set_message(int p_id, const String &p_text, MessageType p_type, bool p_auto_prefix = true);
	bool is_valid() const;

	EditorValidationPanel();
};
