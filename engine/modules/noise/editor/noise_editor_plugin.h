/**************************************************************************/
/*  noise_editor_plugin.h                                                 */

#pragma once

#include "editor/plugins/editor_plugin.h"

class NoiseEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(NoiseEditorPlugin, EditorPlugin)

public:
	String get_plugin_name() const override;

	NoiseEditorPlugin();
};
