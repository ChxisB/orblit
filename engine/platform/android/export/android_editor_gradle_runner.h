/**************************************************************************/
/*  android_editor_gradle_runner.h                                        */

#pragma once

#ifdef ANDROID_ENABLED

#include "core/object/object.h"

class ConfirmationDialog;
class RichTextLabel;

class AndroidEditorGradleRunner : public Object {
	ORBLIT_CLASS(AndroidEditorGradleRunner, Object);

	RichTextLabel *output_label = nullptr;
	ConfirmationDialog *output_dialog = nullptr;

	enum State {
		STATE_IDLE,
		STATE_BUILDING,
		STATE_CLEANING,
	};
	State state = STATE_IDLE;

	String project_path;
	String build_path;
	String output_path;
	String export_format;
	List<String> gradle_build_args;
	List<String> gradle_copy_args;
	int64_t job_id;

	void _android_gradle_build_connect();
	void _android_gradle_build_disconnect();
	void _android_gradle_build_output(int p_type, const String &p_line);
	void _android_gradle_build_build();
	void _android_gradle_build_build_callback(int p_exit_code);
	void _android_gradle_build_copy();
	void _android_gradle_build_copy_callback(int p_exit_code);
	void _android_gradle_build_clean_project(bool p_was_successful);
	void _android_gradle_build_clean_project_callback();

	void _android_gradle_build_failed(const String &p_msg = String());
	void _android_gradle_build_cancel();

	const String copy_output_action = "copy_output";
	void _output_dialog_custom_action(const String &p_action);

public:
	void run_gradle(const String &p_project_path, const String &p_build_path, const String &p_output_path, const String &p_export_format, const List<String> &p_gradle_build_args, const List<String> &p_gradle_copy_args);
};

#endif // ANDROID_ENABLED
