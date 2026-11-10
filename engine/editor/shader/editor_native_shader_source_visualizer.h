/**************************************************************************/
/*  editor_native_shader_source_visualizer.h                              */

#pragma once

#include "scene/ui/dialogs.h"

class CodeHighlighter;
class TabContainer;

class EditorNativeShaderSourceVisualizer : public AcceptDialog {
	ORBLIT_CLASS(EditorNativeShaderSourceVisualizer, AcceptDialog)
	TabContainer *versions = nullptr;
	Ref<CodeHighlighter> syntax_highlighter;

	void _load_theme_settings();
	void _inspect_shader(RID p_shader);

protected:
	static void register_members();

public:
	EditorNativeShaderSourceVisualizer();
};
