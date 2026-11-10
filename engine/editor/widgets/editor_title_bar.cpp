/**************************************************************************/
/*  editor_title_bar.cpp                                                  */

#include "editor_title_bar.h"

#include "core/object/callable_mp.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "systems/display/display_system.h"

void EditorTitleBar::gui_input(const Ref<InputEvent> &p_event) {
	if (!can_move) {
		return;
	}

	Ref<InputEventMouseMotion> mm = p_event;
	if (mm.is_valid() && moving) {
		if (mm->get_button_mask().has_flag(MouseButtonMask::LEFT)) {
			Window *w = Object::cast_to<Window>(get_viewport());
			if (w) {
				Point2 mouse = DisplaySystem::get_singleton()->mouse_get_position();
				w->set_position(mouse - click_pos);
			}
		} else {
			moving = false;
		}
	}

	Ref<InputEventMouseButton> mb = p_event;
	if (mb.is_valid() && has_point(mb->get_position())) {
		Window *w = Object::cast_to<Window>(get_viewport());
		if (w) {
			if (mb->get_button_index() == MouseButton::LEFT) {
				if (mb->is_pressed()) {
					if (DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_WINDOW_DRAG)) {
						DisplaySystem::get_singleton()->window_start_drag(w->get_window_id());
					} else {
						click_pos = DisplaySystem::get_singleton()->mouse_get_position() - w->get_position();
						moving = true;
					}
				} else {
					moving = false;
				}
			}
			if (mb->get_button_index() == MouseButton::LEFT && mb->is_double_click() && mb->is_pressed()) {
				if (DisplaySystem::get_singleton()->window_maximize_on_title_dbl_click()) {
					if (w->get_mode() == Window::MODE_WINDOWED) {
						w->set_mode(Window::MODE_MAXIMIZED);
					} else if (w->get_mode() == Window::MODE_MAXIMIZED) {
						w->set_mode(Window::MODE_WINDOWED);
					}
				} else if (DisplaySystem::get_singleton()->window_minimize_on_title_dbl_click()) {
					w->set_mode(Window::MODE_MINIMIZED);
				}
				moving = false;
			}
		}
	}
}

void EditorTitleBar::set_center_control(Control *p_center_control) {
	center_control = p_center_control;

	if (center_control->get_parent() != this) {
		center_control->reparent(this);
	}
}

Control *EditorTitleBar::get_center_control() const {
	return center_control;
}

void EditorTitleBar::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_EXIT_STAGE: {
			Stage::get_singleton()->get_root()->disconnect(SceneStringName(nonclient_window_input), callable_mp(this, &EditorTitleBar::gui_input));
			get_window()->set_nonclient_area(Rect2i());
		} break;
		case NOTIFICATION_ENTER_STAGE: {
			Stage::get_singleton()->get_root()->connect(SceneStringName(nonclient_window_input), callable_mp(this, &EditorTitleBar::gui_input));
			[[fallthrough]];
		}
		case NOTIFICATION_RESIZED: {
			get_window()->set_nonclient_area(get_global_transform().xform(Rect2i(get_position(), get_size())));
		} break;
		case NOTIFICATION_SORT_CHILDREN: {
			if (!center_control) {
				break;
			}
			Control *prev = nullptr;
			Control *base = nullptr;
			Control *next = nullptr;

			bool rtl = is_layout_rtl();

			int start;
			int end;
			int delta;
			if (rtl) {
				start = get_child_count() - 1;
				end = -1;
				delta = -1;
			} else {
				start = 0;
				end = get_child_count();
				delta = +1;
			}

			for (int i = start; i != end; i += delta) {
				Control *c = as_sortable_control(get_child(i));
				if (!c) {
					continue;
				}
				if (base) {
					next = c;
					break;
				}
				if (c != center_control) {
					prev = c;
					continue;
				}
				base = c;
			}
			if (base && prev && next) {
				Size2i title_size = get_size();
				int center = title_size.x / 2;
				int width = MIN(center - prev->get_position().x, next->get_position().x + next->get_size().x - center) * 1.9;
				width = MAX(width, base->get_combined_minimum_size().x);
				base->set_custom_maximum_size(Size2i(width, title_size.y));
				Size2i c_size = base->get_bound_desired_size();

				int min_offset = prev->get_position().x + prev->get_combined_minimum_size().x;
				int max_offset = next->get_position().x + next->get_size().x - next->get_combined_minimum_size().x - c_size.x;

				int offset = (title_size.width - c_size.width) / 2;
				offset = CLAMP(offset, min_offset, max_offset);

				fit_child_in_rect(prev, Rect2i(prev->get_position().x, 0, offset - prev->get_position().x, title_size.height));
				fit_child_in_rect(next, Rect2i(offset + c_size.width, 0, next->get_position().x + next->get_size().x - (offset + c_size.width), title_size.height));
				fit_child_in_rect(base, Rect2i(offset, 0, c_size.width, title_size.height));
			}
		} break;
	}
}

void EditorTitleBar::set_can_move_window(bool p_enabled) {
	can_move = p_enabled;
	set_input_enabled(can_move);
}

bool EditorTitleBar::get_can_move_window() const {
	return can_move;
}
