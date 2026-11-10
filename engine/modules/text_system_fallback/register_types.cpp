/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "text_system_fallback.h"

#include "core/object/class_registry.h"

void initialize_text_system_fallback_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}

	ORBLIT_REGISTER_CLASS(TextSystemFallback);
	TextSystemManager *tsman = TextSystemManager::get_singleton();
	if (tsman) {
		Ref<TextSystemFallback> ts;
		ts.instantiate();
		tsman->add_interface(ts);
	}
}

void uninitialize_text_system_fallback_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
