/**************************************************************************/
/*  test_file_dialog.cpp                                                  */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_file_dialog)

#include "core/io/dir_access.h"
#include "core/object/message_queue.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/file_dialog.h"
#include "scene/ui/label.h"

namespace TestFileDialog {

TEST_CASE("[Stage][FileDialog] A folder with a non ASCII name is readable") {
	Error err = OK;
	Ref<DirAccess> temp = DirAccess::create_temp("orblit_test", false, &err);
	REQUIRE(err == OK);

	const String folder = String::utf8("Różne");
	REQUIRE(temp->make_dir(folder) == OK);

	FileDialog *dialog = memnew(FileDialog);
	dialog->set_access(FileDialog::ACCESS_FILESYSTEM);
	Stage::get_singleton()->get_root()->add_child(dialog);
	dialog->set_visible(true);
	dialog->set_current_dir(temp->get_current_dir().path_join(folder));
	// The list refreshes on a deferred call.
	MessageQueue::get_singleton()->flush();

	// The overlay that says the folder cannot be read must stay hidden.
	bool overlay_shown = false;
	for (const Variant &child : dialog->find_children("*", "Label", true, false)) {
		Label *label = Object::cast_to<Label>(child);
		overlay_shown = overlay_shown || (label->is_visible() && label->get_text().contains("permission"));
	}
	CHECK_FALSE(overlay_shown);

	memdelete(dialog);
}

} // namespace TestFileDialog
