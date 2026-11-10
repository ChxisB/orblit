/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "fastnoise_lite.h"
#include "noise.h"
#include "noise_texture_planar.h"
#include "noise_texture_spatial.h"

#include "core/object/class_registry.h"

#ifdef TOOLS_ENABLED
#include "editor/noise_editor_plugin.h"
#endif

#ifdef TOOLS_ENABLED
#include "editor/plugins/editor_plugin.h"
#endif

void initialize_noise_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(NoiseTexture3D);
		ORBLIT_REGISTER_CLASS(NoiseTexture2D);
		ORBLIT_REGISTER_ABSTRACT_CLASS(Noise);
		ORBLIT_REGISTER_CLASS(FastNoiseLite);
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<NoiseEditorPlugin>();
	}
#endif
}

void uninitialize_noise_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
