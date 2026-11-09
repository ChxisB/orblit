/**************************************************************************/
/*  text_editor.cpp                                                       */

#include "text_editor.h"

#include "core/io/json.h"
#include "editor/script/script_editor_plugin.h"
#include "editor/settings/editor_settings.h"

void TextEditor::_validate_script() {
	TextEditorBase::_validate_script();

	Ref<JSON> json_file = edited_res;
	if (json_file.is_valid()) {
		CodeEdit *te = code_editor->get_text_editor();

		te->set_line_background_color(code_editor->get_error_pos().x, Color(0, 0, 0, 0));
		code_editor->set_error("");

		if (json_file->parse(te->get_text(), true) != OK) {
			code_editor->set_error(json_file->get_error_message().replace("[", "[lb]"));
			code_editor->set_error_pos(json_file->get_error_line(), 0);
			te->set_line_background_color(code_editor->get_error_pos().x, EDITOR_GET("text_editor/theme/highlighting/mark_color"));
		}
	}
}

void TextEditor::apply_code() {
	Ref<TextFile> text_file = edited_res;
	if (text_file.is_valid()) {
		text_file->set_text(code_editor->get_text_editor()->get_text());
	}

	Ref<JSON> json_file = edited_res;
	if (json_file.is_valid()) {
		json_file->parse(code_editor->get_text_editor()->get_text(), true);
	}
	code_editor->get_text_editor()->get_syntax_highlighter()->update_cache();
}

ScriptEditorBase *TextEditor::create_editor(const Ref<Resource> &p_resource) {
	if (Object::cast_to<TextFile>(*p_resource) || Object::cast_to<JSON>(*p_resource)) {
		return memnew(TextEditor);
	}
	return nullptr;
}

void TextEditor::register_editor() {
	ScriptEditor::register_create_script_editor_function(create_editor);
}

TextEditor::TextEditor() {
	add_child(code_editor);
}
