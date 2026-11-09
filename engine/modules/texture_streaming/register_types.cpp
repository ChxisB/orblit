/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "texture_streaming.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

#include "modules/register_module_types.h"

static TextureStreaming *_texture_streaming_server = nullptr;

void initialize_texture_streaming_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_CORE) {
		TextureStreaming::register_project_settings();
	}

	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(TextureStreaming);

		_texture_streaming_server = memnew(TextureStreaming);
		Engine::get_singleton()->add_singleton(Engine::Singleton("TextureStreaming", TextureStreaming::get_singleton()));
	}
}

void uninitialize_texture_streaming_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		if (_texture_streaming_server) {
			memdelete(_texture_streaming_server);
		}
	}
}
