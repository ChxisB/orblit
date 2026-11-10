/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "text_system_advanced.h"

#include "core/object/class_registry.h"

void initialize_text_system_advanced_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}

	ORBLIT_REGISTER_CLASS(TextSystemAdvanced);
	TextSystemManager *tsman = TextSystemManager::get_singleton();
	if (tsman) {
		Ref<TextSystemAdvanced> ts;
		ts.instantiate();
		tsman->add_interface(ts);
	}
}

void uninitialize_text_system_advanced_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
