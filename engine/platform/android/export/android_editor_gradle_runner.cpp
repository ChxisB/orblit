/**************************************************************************/
/*  android_editor_gradle_runner.cpp                                      */

#ifdef ANDROID_ENABLED
#include "android_editor_gradle_runner.h"

#include "../display_system_android.h"
#include "../java_wrapper.h"
#include "../os_android.h"

#include "core/object/callable_mp.h"
#include "editor/editor_interface.h"
#include "editor/settings/editor_settings.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/rich_text_label.h"

void AndroidEditorGradleRunner::run_gradle(const String &p_project_path, const String &p_build_path, const String &p_output_path, const String &p_export_format, const List<String> &p_gradle_build_args, const List<String> &p_gradle_copy_args) {
	project_path = p_project_path;
	build_path = p_build_path;
	output_path = p_output_path;
	export_format = p_export_format;
	gradle_build_args = p_gradle_build_args;
	gradle_copy_args = p_gradle_copy_args;

	if (output_dialog == nullptr) {
		output_label = memnew(RichTextLabel);
		output_label->set_selection_enabled(true);
		output_label->set_context_menu_enabled(true);
		output_label->set_scroll_follow(true);

		output_dialog = memnew(ConfirmationDialog);
		output_dialog->set_unparent_when_invisible(true);
		output_dialog->set_title(TTR("Building Android Project (gradle)"));
		output_dialog->add_child(output_label);

		output_dialog->add_button(TTR("Copy Output"), true, copy_output_action);

		output_dialog->connect("canceled", callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_cancel));
		output_dialog->connect("custom_action", callable_mp(this, &AndroidEditorGradleRunner::_output_dialog_custom_action));
	}

	output_label->clear();
	output_dialog->get_ok_button()->set_disabled(true);

	EditorInterface::get_singleton()->popup_dialog_centered_ratio(output_dialog);

	state = STATE_BUILDING;
	_android_gradle_build_connect();
}

void AndroidEditorGradleRunner::_android_gradle_build_connect() {
	_android_gradle_build_output(0, TTR("> Connecting to Gradle Build Environment..."));

	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	if (!orblit_java->build_env_connect(callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_build))) {
		_android_gradle_build_failed(TTR("Unable to connect to Gradle Build Environment service"));
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_disconnect() {
	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	orblit_java->build_env_disconnect();
}

void AndroidEditorGradleRunner::_android_gradle_build_output(int p_type, const String &p_line) {
	if (p_type == 0) {
		print_line(p_line);
		output_label->append_text("[color=green]" + p_line + "[/color]\n");
	} else if (p_type == 1) {
		print_line(p_line);
		output_label->add_text(p_line + "\n");
	} else {
		print_error(p_line);
		output_label->append_text("[color=red]" + p_line + "[/color]\n");
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_build() {
	_android_gradle_build_output(0, TTR("> Starting Gradle build..."));

	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	job_id = orblit_java->build_env_execute(
			"gradle",
			gradle_build_args,
			project_path,
			build_path,
			callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_output),
			callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_build_callback));
	if (job_id < 0) {
		_android_gradle_build_failed(TTR("Failed to execute Gradle command"));
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_build_callback(int p_exit_code) {
	job_id = -1;
	if (p_exit_code != 0) {
		_android_gradle_build_failed();
		return;
	}

	_android_gradle_build_copy();
}

void AndroidEditorGradleRunner::_android_gradle_build_copy() {
	_android_gradle_build_output(0, TTR("> Copying Gradle artifacts..."));

	OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
	job_id = orblit_java->build_env_execute(
			"gradle",
			gradle_copy_args,
			project_path,
			build_path,
			callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_output),
			callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_copy_callback));
	if (job_id < 0) {
		_android_gradle_build_failed(TTR("Failed to execute Gradle command"));
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_copy_callback(int p_exit_code) {
	job_id = -1;
	if (p_exit_code != 0) {
		_android_gradle_build_failed();
	} else {
		_android_gradle_build_clean_project(true);
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_clean_project(bool p_was_successful) {
	if (state != STATE_CLEANING) {
		state = STATE_CLEANING;

		if (p_was_successful) {
			output_dialog->hide();

			bool prompt_apk_install = EDITOR_GET("export/android/install_exported_apk");
			if (prompt_apk_install && export_format == "apk") {
				OS_Android::get_singleton()->shell_open(output_path);
			}
		} else {
			output_dialog->get_ok_button()->set_disabled(false);
		}

		OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
		orblit_java->build_env_clean_project(
				project_path,
				build_path,
				callable_mp(this, &AndroidEditorGradleRunner::_android_gradle_build_clean_project_callback));
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_clean_project_callback() {
	// Ensure we haven't switched back to STATE_BUILDING in the meantime.
	if (state == STATE_CLEANING) {
		_android_gradle_build_disconnect();
		state = STATE_IDLE;
	}
}

void AndroidEditorGradleRunner::_android_gradle_build_failed(const String &p_msg) {
	job_id = -1;

	if (p_msg != "") {
		_android_gradle_build_output(1, p_msg);
	}

	_android_gradle_build_clean_project(false);
}

void AndroidEditorGradleRunner::_android_gradle_build_cancel() {
	if (job_id > 0) {
		OrblitJavaWrapper *orblit_java = OS_Android::get_singleton()->get_orblit_java();
		orblit_java->build_env_cancel(job_id);
		_android_gradle_build_clean_project(false);
	}
}

void AndroidEditorGradleRunner::_output_dialog_custom_action(const String &p_action) {
	if (p_action == copy_output_action) {
		String output = output_label->get_parsed_text();
		DisplaySystemAndroid::get_singleton()->clipboard_set(output);
	}
}

#endif // ANDROID_ENABLED
