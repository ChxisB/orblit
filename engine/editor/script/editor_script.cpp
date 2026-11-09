/**************************************************************************/
/*  editor_script.cpp                                                     */

#include "editor_script.h"

#include "core/object/class_registry.h"
#include "editor/editor_interface.h"
#include "editor/editor_node.h"

void EditorScript::run() {
	ORBLIT_VIRTUAL_CALL(_run);
}

void EditorScript::register_members() {
	ORBLIT_VIRTUAL_BIND(_run);
}
