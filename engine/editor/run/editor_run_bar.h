/**************************************************************************/
/*  editor_run_bar.h                                                      */

#pragma once

#include "core/os/process_id.h"
#include "editor/run/editor_run.h"
#include "scene/ui/margin_container.h"

class AcceptDialog;
class Button;
class EditorExportPreset;
class EditorRunNative;
class HBoxContainer;
class MenuButton;
class PanelContainer;

class EditorRunBar : public MarginContainer {
	ORBLIT_CLASS(EditorRunBar, MarginContainer);

	static EditorRunBar *singleton;

	enum RunMode {
		STOPPED = 0,
		RUN_MAIN,
		RUN_CURRENT,
		RUN_CUSTOM,
	};

	enum RunXRModeMenuItem {
		INVALID = -1,
		OFF = 0,
		ON = 1,
	};

	PanelContainer *main_panel = nullptr;
	HBoxContainer *main_hbox = nullptr;
	HBoxContainer *outer_hbox = nullptr;

	Button *profiler_autostart_indicator = nullptr;

	PanelContainer *recovery_mode_panel = nullptr;
	Button *recovery_mode_button = nullptr;
	Button *recovery_mode_reload_button = nullptr;
	AcceptDialog *recovery_mode_popup = nullptr;

	Button *play_button = nullptr;
	Button *pause_button = nullptr;
	Button *stop_button = nullptr;
	Button *play_scene_button = nullptr;
	Button *play_custom_scene_button = nullptr;

	EditorRun editor_run;
	EditorRunNative *run_native = nullptr;
	bool is_native = false;

	enum MovieMakerMenuItem {
		MOVIE_MAKER_TOGGLE,
		MOVIE_MAKER_OPEN_SETTINGS,
	};
	PanelContainer *write_movie_panel = nullptr;
	MenuButton *write_movie_button = nullptr;
	bool movie_maker_enabled = false;

	RunMode current_mode = RunMode::STOPPED;
	String run_custom_filename;
	String run_current_filename;

	void _reset_play_buttons();
	void _update_play_buttons();

	void _movie_maker_item_pressed(int p_id);
	void _write_movie_toggled(bool p_enabled);
	void _quick_run_selected(const String &p_file_path, int p_menu_item = RunXRModeMenuItem::INVALID);

	void _play_current_pressed(int p_menu_item = RunXRModeMenuItem::INVALID);
	void _play_custom_pressed(int p_menu_item = RunXRModeMenuItem::INVALID);

	void _run_scene(const String &p_scene_path = "", const Vector<String> &p_run_args = Vector<String>());
	void _run_native(const Ref<EditorExportPreset> &p_preset);

	void _profiler_autostart_indicator_pressed();

private:
	static Vector<String> _get_xr_mode_play_args(RunXRModeMenuItem p_menu_item);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	static EditorRunBar *get_singleton() { return singleton; }

	void recovery_mode_show_dialog();
	void recovery_mode_reload_project();

	void play_main_scene(bool p_from_native = false, const Vector<String> &p_play_args = Vector<String>());
	void play_current_scene(bool p_reload = false, const Vector<String> &p_play_args = Vector<String>());
	void play_custom_scene(const String &p_custom, const Vector<String> &p_play_args = Vector<String>());

	void stop_playing();
	bool is_playing() const;
	String get_playing_scene() const;

	Error start_native_device(int p_device_id) const;
	bool is_running_natively() const;

	ProcessID has_child_process(ProcessID p_pid) const;
	void stop_child_process(ProcessID p_pid);
	ProcessID get_current_process() const;

	void set_movie_maker_enabled(bool p_enabled);
	bool is_movie_maker_enabled() const;

	void update_profiler_autostart_indicator();

	Button *get_pause_button() { return pause_button; }

	HBoxContainer *get_buttons_container();

	EditorRunBar();
};
