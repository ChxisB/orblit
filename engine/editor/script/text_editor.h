/**************************************************************************/
/*  text_editor.h                                                         */

#pragma once

#include "editor/script/script_editor_base.h"

class TextEditor : public TextEditorBase {
	ORBLIT_CLASS(TextEditor, TextEditorBase);

	static ScriptEditorBase *create_editor(const Ref<Resource> &p_resource);

	static inline EditMenus *edit_menus = nullptr;

protected:
	class EditMenusTEB : public EditMenus {
		ORBLIT_CLASS(EditMenusTEB, EditMenus);

	public:
		virtual bool handles(ScriptEditorBase *p_seb) override { return Object::cast_to<TextEditor>(p_seb); }

		EditMenusTEB(DocumentEditorContainer *p_document_editor_container) : EditMenus(p_document_editor_container) {}
	};

	virtual void _validate_script() override;

public:
	virtual void apply_code() override;

	virtual EditMenusBase *create_edit_menu(DocumentEditorContainer *p_document_editor_container) override { return memnew(EditMenusTEB(p_document_editor_container)); }

	static void register_editor();

	TextEditor();
};
