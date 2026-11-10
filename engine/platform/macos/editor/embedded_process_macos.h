/**************************************************************************/
/*  embedded_process_macos.h                                              */

#pragma once

#include "core/object/class_registry.h"
#include "core/os/process_id.h"
#include "editor/run/embedded_process.h"
#include "scene/ui/control.h"
#include "systems/display/display_system_enums.h"

class DisplaySystemMacOS;
class EmbeddedProcessMacOS;

class LayerHost final : public Control {
	ORBLIT_CLASS(LayerHost, Control);

	ScriptEditorDebugger *script_debugger = nullptr;
	EmbeddedProcessMacOS *process = nullptr;
	bool window_focused = true;

	struct CustomCursor {
		Ref<Image> image;
		Vector2 hotspot;
		CustomCursor() {}
		CustomCursor(const Ref<Image> &p_image, const Vector2 &p_hotspot) {
			image = p_image;
			hotspot = p_hotspot;
		}
	};
	HashMap<DisplaySystemEnums::CursorShape, CustomCursor> custom_cursors;

	virtual void gui_input(const Ref<InputEvent> &p_event) override;

protected:
	void _notification(int p_what);

public:
	void cursor_set_custom_image(const Ref<Image> &p_image, DisplaySystemEnums::CursorShape p_shape, const Vector2 &p_hotspot);
	void set_script_debugger(ScriptEditorDebugger *p_debugger) {
		script_debugger = p_debugger;
	}

	LayerHost(EmbeddedProcessMacOS *p_process);
};

class EmbeddedProcessMacOS final : public EmbeddedProcessBase {
	ORBLIT_CLASS(EmbeddedProcessMacOS, EmbeddedProcessBase);

	enum class EmbeddingState {
		IDLE,
		IN_PROGRESS,
		COMPLETED,
		FAILED,
	};

	DisplaySystemMacOS *ds = nullptr;
	EmbeddingState embedding_state = EmbeddingState::IDLE;
	uint32_t context_id = 0;
	ScriptEditorDebugger *script_debugger = nullptr;
	LayerHost *layer_host = nullptr;
	ProcessID current_process_id = 0;

	// Embedded process state.

	// The last mouse mode sent by the embedded process.
	DisplaySystemEnums::MouseMode mouse_mode = DisplaySystemEnums::MOUSE_MODE_VISIBLE;

	// Helper functions.

	void _try_embed_process();
	void update_embedded_process();

protected:
	void _notification(int p_what);

public:
	// MARK: - Message Handlers

	void set_context_id(uint32_t p_context_id);
	void mouse_set_mode(DisplaySystemEnums::MouseMode p_mode);

	uint32_t get_context_id() const { return context_id; }
	void set_script_debugger(ScriptEditorDebugger *p_debugger) override;

	bool is_embedding_in_progress() const override {
		return embedding_state == EmbeddingState::IN_PROGRESS;
	}

	_FORCE_INLINE_ bool is_embedding_completed() const override {
		return embedding_state == EmbeddingState::COMPLETED;
	}

	bool is_process_focused() const override { return layer_host->has_focus(); }
	void embed_process(ProcessID p_pid) override;
	int get_embedded_pid() const override { return current_process_id; }
	void reset() override;
	void reset_timers() override {}
	void request_close() override;
	void queue_update_embedded_process() override { update_embedded_process(); }

	Rect2i get_adjusted_embedded_window_rect(const Rect2i &p_rect) const override;

	_FORCE_INLINE_ LayerHost *get_layer_host() const { return layer_host; }

	void display_state_changed();

	// MARK: - Embedded process state
	_FORCE_INLINE_ DisplaySystemEnums::MouseMode get_mouse_mode() const { return mouse_mode; }

	EmbeddedProcessMacOS();
	~EmbeddedProcessMacOS() override;
};
