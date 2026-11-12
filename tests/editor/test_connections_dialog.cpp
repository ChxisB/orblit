/**************************************************************************/
/*  test_connections_dialog.cpp                                           */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_connections_dialog)

#ifdef TOOLS_ENABLED

#include "core/config/project_settings.h"
#include "core/string/char_utils.h"
#include "editor/scene/connections_dialog.h"
#include "scene/stage/entity.h"

namespace TestConnectionsDialog {

TEST_CASE("[Editor][ConnectDialog] A node name keeps its leading digits") {
	ProjectSettings::get_singleton()->set("editor/naming/default_signal_callback_name", "_on_{NodeName}_{signal_name}");
	ProjectSettings::get_singleton()->set("editor/naming/default_signal_callback_to_self_name", "_on_{signal_name}");

	Entity *source = memnew(Entity);
	Entity *target = memnew(Entity);
	source->set_name("1 Button");

	CHECK(ConnectDialog::generate_method_callback_name(source, "pressed", target) == "_on_1Button_pressed");

	memdelete(source);
	memdelete(target);
}

TEST_CASE("[Editor][ConnectDialog] A name template that starts with the node gives a valid name") {
	ProjectSettings::get_singleton()->set("editor/naming/default_signal_callback_name", "{node_name}_{signal_name}");

	Entity *source = memnew(Entity);
	Entity *target = memnew(Entity);
	source->set_name("1Button");

	const String method = ConnectDialog::generate_method_callback_name(source, "pressed", target);
	CHECK(is_unicode_identifier_start(method[0]));
	CHECK(method.ends_with("button_pressed"));

	memdelete(source);
	memdelete(target);
}

} // namespace TestConnectionsDialog

#endif // TOOLS_ENABLED
