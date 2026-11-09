/**************************************************************************/
/*  embedded_process.h                                                    */

#pragma once

#include "core/os/process_id.h"
#include "scene/ui/control.h"

class ScriptEditorDebugger;
class Timer;

class EmbeddedProcessBase : public Control {
	ORBLIT_CLASS(EmbeddedProcessBase, Control);

	void _draw();

protected:
	Ref<StyleBox> focus_style_box;
	Size2i window_size;
	bool keep_aspect = false;
	Point2i margin_top_left;
	Point2i margin_bottom_right;
	Window *window = nullptr;

	bool transp_enabled = false;
	Color clear_color;
	Ref<Texture2D> checkerboard;

	void _project_settings_changed();

	static void register_members();
	void _notification(int p_what);

public:
	virtual void set_script_debugger(ScriptEditorDebugger *p_debugger) {}

	virtual bool is_embedding_completed() const = 0;
	virtual bool is_embedding_in_progress() const = 0;
	virtual bool is_process_focused() const = 0;
	virtual void embed_process(ProcessID p_pid) = 0;
	virtual int get_embedded_pid() const = 0;
	virtual void reset() = 0;
	virtual void reset_timers() = 0;
	virtual void request_close() = 0;
	virtual void queue_update_embedded_process() = 0;

	void set_window_size(const Size2i &p_window_size);
	void set_keep_aspect(bool p_keep_aspect);
	virtual Rect2i get_adjusted_embedded_window_rect(const Rect2i &p_rect) const = 0;
	Rect2i get_screen_embedded_window_rect() const;
	int get_margin_size(Side p_side) const;
	Size2 get_margins_size() const;

	EmbeddedProcessBase();
	virtual ~EmbeddedProcessBase();
};

class EmbeddedProcess : public EmbeddedProcessBase {
	ORBLIT_CLASS(EmbeddedProcess, EmbeddedProcessBase);

	bool application_has_focus = true;
	uint64_t last_application_focus_time = 0;
	ProcessID focused_process_id = 0;
	ProcessID current_process_id = 0;
	bool embedding_grab_focus = false;
	bool embedding_completed = false;
	uint64_t start_embedding_time = 0;
	bool updated_embedded_process_queued = false;
	bool last_updated_embedded_process_focused = false;

	Timer *timer_embedding = nullptr;
	Timer *timer_update_embedded_process = nullptr;

	const int embedding_timeout = 45000;

	Rect2i last_global_rect;

	void _try_embed_process();
	void _update_embedded_process();
	void _timer_embedding_timeout();
	void _timer_update_embedded_process_timeout();
	void _check_mouse_over();
	void _check_focused_process_id();
	bool _is_embedded_process_updatable();
	Window *_get_current_modal_window();

protected:
	void _notification(int p_what);

public:
	bool is_embedding_in_progress() const override;
	bool is_embedding_completed() const override;
	bool is_process_focused() const override;
	void embed_process(ProcessID p_pid) override;
	int get_embedded_pid() const override;
	void reset() override;
	void reset_timers() override;
	void request_close() override;
	void queue_update_embedded_process() override;

	Rect2i get_adjusted_embedded_window_rect(const Rect2i &p_rect) const override;

	EmbeddedProcess();
	~EmbeddedProcess() override;
};
