/**************************************************************************/
/*  embedded_process_macos.mm                                             */

#include "embedded_process_macos.h"

#include "../display_system_macos.h"
#include "../display_system_macos_embedded.h"

#include "core/input/input_event_codec.h"
#include "core/os/main_loop.h"
#include "core/os/os.h"
#include "editor/debugger/script_editor_debugger.h"
#include "editor/editor_main_screen.h"
#include "editor/editor_node.h"
#include "editor/run/game_view_plugin.h"
#include "editor/settings/editor_settings.h"
#include "scene/stage/window.h"
#include "scene/ui/control.h"

void EmbeddedProcessMacOS::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			set_notify_transform(true);
		} break;
		case NOTIFICATION_TRANSFORM_CHANGED:
		case NOTIFICATION_VISIBILITY_CHANGED: {
			update_embedded_process();
		} break;
	}
}

void EmbeddedProcessMacOS::update_embedded_process() {
	layer_host->set_rect(get_adjusted_embedded_window_rect(get_rect()));
	if (is_embedding_completed()) {
		ds->embed_process_update(window->get_window_id(), this);
		Rect2i rect = get_screen_embedded_window_rect();
		script_debugger->send_message("embed:window_size", { rect.size });
	}
}

void EmbeddedProcessMacOS::set_context_id(uint32_t p_context_id) {
	if (!window) {
		return;
	}

	context_id = p_context_id;

	_try_embed_process();
}

void EmbeddedProcessMacOS::set_script_debugger(ScriptEditorDebugger *p_debugger) {
	script_debugger = p_debugger;
	layer_host->set_script_debugger(script_debugger);
	_try_embed_process();
}

void EmbeddedProcessMacOS::embed_process(ProcessID p_pid) {
	if (!window) {
		return;
	}

	if (current_process_id != 0) {
		// Stop embedding the last process.
		OS::get_singleton()->kill(current_process_id);
	}

	reset();

	current_process_id = p_pid;
	embedding_state = EmbeddingState::IN_PROGRESS;
	// Attempt to embed the process, but if it has just started and the window is not ready yet,
	// we will retry in this case.
	_try_embed_process();
}

void EmbeddedProcessMacOS::reset() {
	if (!ds) {
		ds = static_cast<DisplaySystemMacOS *>(DisplaySystem::get_singleton());
	}
	if (current_process_id != 0 && is_embedding_completed()) {
		ds->remove_embedded_process(current_process_id);
	}
	DisplaySystem *ds = DisplaySystem::get_singleton();
	for (int i = 0; i < DisplaySystemEnums::CURSOR_MAX; i++) {
		ds->cursor_set_custom_image(Ref<Resource>(), (DisplaySystemEnums::CursorShape)i, Vector2());
	}
	if (ds->mouse_get_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
		ds->mouse_set_mode(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
	}
	current_process_id = 0;
	embedding_state = EmbeddingState::IDLE;
	context_id = 0;
	script_debugger = nullptr;
	queue_redraw();
}

void EmbeddedProcessMacOS::request_close() {
	if (current_process_id != 0 && is_embedding_completed()) {
		script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_CLOSE_REQUEST });
	}
	reset();
}

void EmbeddedProcessMacOS::display_state_changed() {
	DisplaySystemMacOSEmbeddedState state;
	state.screen_max_scale = ds->screen_get_max_scale();
	state.screen_dpi = ds->screen_get_dpi();
	DisplaySystemEnums::WindowID wid = window->get_window_id();
	state.screen_window_scale = ds->screen_get_scale(ds->window_get_current_screen(wid));
	state.display_id = ds->window_get_display_id(wid);

	CGFloat max_edr, max_potential_edr;
	ds->window_get_edr_values(wid, &max_edr, &max_potential_edr);
	state.screen_max_edr = max_edr;
	state.screen_max_potential_edr = max_potential_edr;

	PackedByteArray data;
	state.serialize(data);
	script_debugger->send_message("embed:ds_state", { data });
}

void EmbeddedProcessMacOS::_try_embed_process() {
	if (current_process_id == 0 || script_debugger == nullptr || context_id == 0) {
		return;
	}

	DisplaySystemEnums::WindowID wid = window->get_window_id();
	Error err = ds->embed_process_update(wid, this);
	if (err == OK) {
		layer_host->set_rect(get_adjusted_embedded_window_rect(get_rect()));

		// Replicate important DisplaySystem state.
		display_state_changed();

		Rect2i rect = get_screen_embedded_window_rect();
		script_debugger->send_message("embed:window_size", { rect.size });
		embedding_state = EmbeddingState::COMPLETED;
		queue_redraw();
		emit_signal(SNAME("embedding_completed"));

		layer_host->grab_focus();
	} else {
		// Another unknown error.
		reset();
		emit_signal(SNAME("embedding_failed"));
	}
}

Rect2i EmbeddedProcessMacOS::get_adjusted_embedded_window_rect(const Rect2i &p_rect) const {
	Rect2i control_rect = Rect2i(p_rect.position + margin_top_left, (p_rect.size - get_margins_size()).maxi(1));
	if (window_size != Size2i()) {
		Rect2i desired_rect;
		if (!keep_aspect && control_rect.size.x >= window_size.x && control_rect.size.y >= window_size.y) {
			// Fixed at the desired size.
			desired_rect.size = window_size;
		} else {
			float ratio = MIN((float)control_rect.size.x / window_size.x, (float)control_rect.size.y / window_size.y);
			desired_rect.size = Size2i(window_size.x * ratio, window_size.y * ratio).maxi(1);
		}
		desired_rect.position = Size2i(control_rect.position.x + ((control_rect.size.x - desired_rect.size.x) / 2), control_rect.position.y + ((control_rect.size.y - desired_rect.size.y) / 2));
		return desired_rect;
	} else {
		// Stretch, use all the control area.
		return control_rect;
	}
}

void EmbeddedProcessMacOS::mouse_set_mode(DisplaySystemEnums::MouseMode p_mode) {
	mouse_mode = p_mode;
	// If the mouse is anything other than visible, we must ensure the Game view is active and the layer focused.
	if (mouse_mode != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
		GameView::get_dock()->make_visible();
		layer_host->grab_focus();
	}
	DisplaySystem::get_singleton()->mouse_set_mode(p_mode);
}

EmbeddedProcessMacOS::EmbeddedProcessMacOS() :
		EmbeddedProcessBase() {
	layer_host = memnew(LayerHost(this));
	add_child(layer_host);
	set_focus_mode(FOCUS_NONE);
	layer_host->set_focus_mode(FOCUS_ALL);
	layer_host->set_anchors_and_offsets_preset(PRESET_FULL_RECT);
	layer_host->set_custom_minimum_size(Size2(100, 100));

	// This shortcut allows a user to forcibly release a captured mouse from within the editor, regardless of whether
	// the embedded process has implemented support to release the cursor.
	ED_SHORTCUT("game_view/release_mouse", TTRC("Release Mouse"), KeyModifierMask::ALT | Key::ESCAPE);
}

EmbeddedProcessMacOS::~EmbeddedProcessMacOS() {
	if (current_process_id != 0) {
		// Stop embedding the last process.
		OS::get_singleton()->kill(current_process_id);
		reset();
	}
}

void LayerHost::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_MOUSE_ENTER: {
			DisplaySystem *ds = DisplaySystem::get_singleton();
			for (const KeyValue<DisplaySystemEnums::CursorShape, CustomCursor> &E : custom_cursors) {
				ds->cursor_set_custom_image(E.value.image, E.key, E.value.hotspot);
			}
			if (script_debugger) {
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_MOUSE_ENTER });
			}
			if (get_window()->has_focus()) {
				grab_focus();
			}
		} break;
		case NOTIFICATION_FOCUS_ENTER: {
			// Restore mouse capture, if necessary.
			if (!window_focused && script_debugger) {
				DisplaySystem *ds = DisplaySystem::get_singleton();
				if (process->get_mouse_mode() != ds->mouse_get_mode()) {
					// Restore embedded process mouse mode.
					ds->mouse_set_mode(process->get_mouse_mode());
				}
				script_debugger->send_message("embed:notification", { NOTIFICATION_APPLICATION_FOCUS_IN });
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_FOCUS_IN });
				window_focused = true;
			}
			process->queue_redraw();
		} break;
		case NOTIFICATION_MOUSE_EXIT: {
			DisplaySystem *ds = DisplaySystem::get_singleton();
			for (int i = 0; i < DisplaySystemEnums::CURSOR_MAX; i++) {
				ds->cursor_set_custom_image(Ref<Resource>(), (DisplaySystemEnums::CursorShape)i, Vector2());
			}
			if (script_debugger) {
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_MOUSE_EXIT });
			}
		} break;
		case NOTIFICATION_FOCUS_EXIT: {
			// Temporarily set mouse state back to visible, so the user can interact with the editor.
			if (window_focused && script_debugger) {
				DisplaySystem *ds = DisplaySystem::get_singleton();
				if (ds->mouse_get_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
					ds->mouse_set_mode(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
				}
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_FOCUS_OUT });
				script_debugger->send_message("embed:notification", { NOTIFICATION_APPLICATION_FOCUS_OUT });
				window_focused = false;
			}
			process->queue_redraw();
		} break;
		case MainLoop::NOTIFICATION_OS_IME_UPDATE: {
			if (script_debugger && has_focus()) {
				const String ime_text = DisplaySystem::get_singleton()->ime_get_text();
				const Vector2i ime_selection = DisplaySystem::get_singleton()->ime_get_selection();
				script_debugger->send_message("embed:ime_update", { ime_text, ime_selection });
			}
		} break;
		case NOTIFICATION_EXIT_STAGE:
		case NOTIFICATION_VISIBILITY_CHANGED: {
			if (!is_visible_in_stage()) {
				DisplaySystem *ds = DisplaySystem::get_singleton();
				for (int i = 0; i < DisplaySystemEnums::CURSOR_MAX; i++) {
					ds->cursor_set_custom_image(Ref<Resource>(), (DisplaySystemEnums::CursorShape)i, Vector2());
				}
				if (ds->mouse_get_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
					ds->mouse_set_mode(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
				}
			}
		} break;
		case NOTIFICATION_APPLICATION_FOCUS_IN:
		case NOTIFICATION_WM_WINDOW_FOCUS_IN: {
			if (has_focus() && !window_focused && script_debugger) {
				DisplaySystem *ds = DisplaySystem::get_singleton();
				if (process->get_mouse_mode() != ds->mouse_get_mode()) {
					// Restore embedded process mouse mode.
					ds->mouse_set_mode(process->get_mouse_mode());
					if (process->get_mouse_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
						get_window()->grab_focus();
					}
				}
				script_debugger->send_message("embed:notification", { NOTIFICATION_APPLICATION_FOCUS_IN });
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_FOCUS_IN });
				window_focused = true;
			}
		} break;
		case NOTIFICATION_APPLICATION_FOCUS_OUT:
		case NOTIFICATION_WM_WINDOW_FOCUS_OUT: {
			if (has_focus() && window_focused && script_debugger) {
				DisplaySystem *ds = DisplaySystem::get_singleton();
				if (ds->mouse_get_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
					ds->mouse_set_mode(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
				}
				script_debugger->send_message("embed:win_event", { DisplaySystemEnums::WINDOW_EVENT_FOCUS_OUT });
				script_debugger->send_message("embed:notification", { NOTIFICATION_APPLICATION_FOCUS_OUT });
				window_focused = false;
			}
		} break;
	}
}

void LayerHost::cursor_set_custom_image(const Ref<Image> &p_image, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot) {
	custom_cursors[p_shape] = CustomCursor(p_image, p_hotspot);
}

void LayerHost::gui_input(const Ref<InputEvent> &p_event) {
	if (!process->is_embedding_completed()) {
		return;
	}

	if (p_event->is_pressed()) {
		if (ED_IS_SHORTCUT("game_view/release_mouse", p_event)) {
			DisplaySystem *ds = DisplaySystem::get_singleton();
			if (ds->mouse_get_mode() != DisplaySystemEnums::MOUSE_MODE_VISIBLE) {
				ds->mouse_set_mode(DisplaySystemEnums::MOUSE_MODE_VISIBLE);
				if (script_debugger != nullptr) {
					script_debugger->send_message("embed:mouse_set_mode", { DisplaySystemEnums::MOUSE_MODE_VISIBLE });
				}
			}
			accept_event();
			return;
		}
	}

	Ref<InputEventJoypadMotion> jm = p_event;
	Ref<InputEventJoypadButton> jb = p_event;
	if (jm.is_valid() || jb.is_valid()) {
		accept_event();
		return;
	}

	// Convert from editor pixels to points for the game.
	float inv_scale = 1.0f / DisplaySystem::get_singleton()->screen_get_max_scale();
	Ref<InputEvent> event = p_event;

	{
		Ref<InputEventMouse> e = p_event;
		if (e.is_valid()) {
			e = e->duplicate();
			e->set_position(e->get_position() * inv_scale);
			e->set_global_position(e->get_global_position() * inv_scale);
			Ref<InputEventMouseMotion> mm = e;
			if (mm.is_valid()) {
				mm->set_relative(mm->get_relative() * inv_scale);
			}
			event = e;
		}
	}

	{
		Ref<InputEventGesture> e = p_event;
		if (e.is_valid()) {
			e = e->duplicate();
			e->set_position(e->get_position() * inv_scale);
			event = e;
		}
	}

	PackedByteArray data;
	if (encode_input_event(event, data)) {
		if (script_debugger != nullptr) {
			script_debugger->send_message("embed:event", { data });
		}
		accept_event();
	}
}

LayerHost::LayerHost(EmbeddedProcessMacOS *p_process) :
		process(p_process) {}
