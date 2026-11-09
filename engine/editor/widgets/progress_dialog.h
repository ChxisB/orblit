/**************************************************************************/
/*  progress_dialog.h                                                     */

#pragma once

#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/center_container.h"
#include "scene/ui/label.h"
#include "scene/ui/progress_bar.h"

class BackgroundProgress : public HBoxContainer {
	ORBLIT_CLASS(BackgroundProgress, HBoxContainer);

	_THREAD_SAFE_CLASS_

	struct Task {
		HBoxContainer *hb = nullptr;
		ProgressBar *progress = nullptr;
	};

	HashMap<String, Task> tasks;
	HashMap<String, int> updates;
	void _update();

protected:
	void _add_task(const String &p_task, const String &p_label, int p_steps);
	void _task_step(const String &p_task, int p_step = -1);
	void _end_task(const String &p_task);

public:
	void add_task(const String &p_task, const String &p_label, int p_steps);
	void task_step(const String &p_task, int p_step = -1);
	void end_task(const String &p_task);
};

class PanelContainer;

class ProgressDialog : public CenterContainer {
	ORBLIT_CLASS(ProgressDialog, CenterContainer);
	struct Task {
		String task;
		VBoxContainer *vb = nullptr;
		ProgressBar *progress = nullptr;
		Label *state = nullptr;
		uint64_t last_progress_tick = 0;
	};
	HBoxContainer *cancel_hb = nullptr;
	Button *cancel = nullptr;

	HashMap<String, Task> tasks;
	PanelContainer *center_panel = nullptr;
	VBoxContainer *main = nullptr;

	LocalVector<ObjectID> host_windows;

	Size2 main_border_size;

	static ProgressDialog *singleton;
	void _popup();

	void _cancel_pressed();

	void _update_ui();
	void _reparent_and_show();
	bool canceled = false;

protected:
	void _notification(int p_what);

public:
	static ProgressDialog *get_singleton() { return singleton; }
	void add_task(const String &p_task, const String &p_label, int p_steps, bool p_can_cancel = false);
	bool task_step(const String &p_task, const String &p_state, int p_step = -1, bool p_force_redraw = true);
	void end_task(const String &p_task);

	void add_host_window(ObjectID p_window);
	void remove_host_window(ObjectID p_window);

	ProgressDialog();
	~ProgressDialog();
};
