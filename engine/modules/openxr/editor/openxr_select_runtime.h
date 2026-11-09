/**************************************************************************/
/*  openxr_select_runtime.h                                               */

#pragma once

#include "scene/ui/option_button.h"

class OpenXRSelectRuntime : public OptionButton {
	ORBLIT_CLASS(OpenXRSelectRuntime, OptionButton);

public:
	OpenXRSelectRuntime();

protected:
	void _notification(int p_notification);

private:
	void _update_items();
	void _on_item_selected(int p_which);

	Dictionary _enumerate_runtimes();
	String _try_and_get_runtime_name(const String &p_config_file);
	void _add_runtime(Dictionary &r_runtimes, const String &p_config_file);
};
