/**************************************************************************/
/*  openxr_select_interaction_profile_dialog.cpp                          */

#include "openxr_select_interaction_profile_dialog.h"

#include "../action_map/openxr_interaction_profile_metadata.h"
#include "../openxr_api.h"

#include "core/object/callable_mp.h"
#include "editor/themes/editor_scale.h"

void OpenXRSelectInteractionProfileDialog::register_members() {
	ADD_SIGNAL(MethodInfo("interaction_profile_selected", PropertyInfo(Variant::STRING, "interaction_profile")));
}

void OpenXRSelectInteractionProfileDialog::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			scroll->add_theme_style_override(SceneStringName(panel), get_theme_stylebox(SceneStringName(panel), SNAME("Tree")));
		} break;
	}
}

void OpenXRSelectInteractionProfileDialog::_on_select_interaction_profile(const String &p_interaction_profile) {
	if (selected_interaction_profile != "") {
		EntityPath button_path = ip_buttons[selected_interaction_profile];
		Button *button = Object::cast_to<Button>(get_entity(button_path));
		if (button != nullptr) {
			button->set_flat(true);
		}
	}

	selected_interaction_profile = p_interaction_profile;

	if (selected_interaction_profile != "") {
		EntityPath button_path = ip_buttons[selected_interaction_profile];
		Button *button = Object::cast_to<Button>(get_entity(button_path));
		if (button != nullptr) {
			button->set_flat(false);
		}
	}
}

void OpenXRSelectInteractionProfileDialog::open(const PackedStringArray &p_do_not_include) {
	int available_count = 0;

	OpenXRInteractionProfileMetadata *meta_data = OpenXRInteractionProfileMetadata::get_singleton();
	ERR_FAIL_NULL(meta_data);

	// Out with the old.
	while (main_vb->get_child_count() > 1) {
		memdelete(main_vb->get_child(1));
	}

	PackedStringArray requested_extensions = OpenXRAPI::get_all_requested_extensions(0);

	selected_interaction_profile = "";
	ip_buttons.clear();

	// In with the new.
	PackedStringArray interaction_profiles = meta_data->get_interaction_profile_paths();
	for (const String &path : interaction_profiles) {
		const Vector<String> extensions = meta_data->get_interaction_profile_extensions(path).split(",", false);
		bool extension_is_requested = extensions.is_empty(); // If none, then yes we can use this.
		for (const String &extension : extensions) {
			extension_is_requested |= requested_extensions.has(extension);
		}
		if (!p_do_not_include.has(path) && extension_is_requested) {
			Button *ip_button = memnew(Button);
			ip_button->set_flat(true);
			ip_button->set_text(meta_data->get_profile(path)->display_name);
			ip_button->set_text_alignment(HORIZONTAL_ALIGNMENT_LEFT);
			ip_button->connect(SceneStringName(pressed), callable_mp(this, &OpenXRSelectInteractionProfileDialog::_on_select_interaction_profile).bind(path));
			main_vb->add_child(ip_button);

			ip_buttons[path] = ip_button->get_path();
			available_count++;
		}
	}

	all_selected->set_visible(available_count == 0);
	get_cancel_button()->set_visible(available_count > 0);
	popup_centered();
}

void OpenXRSelectInteractionProfileDialog::ok_pressed() {
	if (selected_interaction_profile != "") {
		emit_signal("interaction_profile_selected", selected_interaction_profile);
	}

	hide();
}

OpenXRSelectInteractionProfileDialog::OpenXRSelectInteractionProfileDialog() {
	set_title(TTR("Select an interaction profile"));

	scroll = memnew(ScrollContainer);
	scroll->set_custom_minimum_size(Size2(600.0 * EDSCALE, 400.0 * EDSCALE));
	add_child(scroll);

	main_vb = memnew(VBoxContainer);
	main_vb->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	scroll->add_child(main_vb);

	all_selected = memnew(Label);
	all_selected->set_focus_mode(Control::FOCUS_ACCESSIBILITY);
	all_selected->set_text(TTR("All interaction profiles have been added to the action map."));
	main_vb->add_child(all_selected);
}
