/**************************************************************************/
/*  subviewport_container.h                                               */

#pragma once

#include "scene/ui/container.h"

class SubViewportContainer : public Container {
	ORBLIT_CLASS(SubViewportContainer, Container);

	bool stretch = false;
	int shrink = 1;
	bool mouse_target = false;

	void _notify_viewports(int p_notification);
	bool _is_propagated_in_gui_input(const Ref<InputEvent> &p_event);
	void _send_event_to_viewports(const Ref<InputEvent> &p_event);
	void _propagate_nonpositional_event(const Ref<InputEvent> &p_event);

protected:
	void _notification(int p_what);
	static void register_members();

	virtual void add_child_notify(Entity *p_child) override;
	virtual void remove_child_notify(Entity *p_child) override;

	ORBLIT_VIRTUAL1RC(bool, _propagate_input_event, RequiredParam<InputEvent>);

public:
	void set_stretch(bool p_enable);
	bool is_stretch_enabled() const;

	virtual void input(const Ref<InputEvent> &p_event) override;
	virtual void unhandled_input(const Ref<InputEvent> &p_event) override;
	virtual void gui_input(const Ref<InputEvent> &p_event) override;
	void set_stretch_shrink(int p_shrink);
	int get_stretch_shrink() const;
	void recalc_force_viewport_sizes();

	void set_mouse_target(bool p_enable);
	bool is_mouse_target_enabled();

	virtual Size2 get_minimum_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;

	PackedStringArray get_configuration_warnings() const override;

	SubViewportContainer();
};
