/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "ogg_packet_sequence.h"

#include "core/object/class_registry.h"

void initialize_ogg_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ORBLIT_REGISTER_CLASS(OggPacketSequence);
	ORBLIT_REGISTER_CLASS(OggPacketSequencePlayback);
}

void uninitialize_ogg_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
