/**************************************************************************/
/*  visible_on_screen_notifier_planar.h                                       */

#pragma once

#include "scene/planar/entity_planar.h"

class Viewport;
class VisibleOnScreenNotifier2D : public Entity2D {
	ORBLIT_CLASS(VisibleOnScreenNotifier2D, Entity2D);

	Rect2 rect;
	bool show_rect = true;

private:
	bool on_screen = false;
	void _visibility_enter();
	void _visibility_exit();

protected:
	virtual void _screen_enter() {}
	virtual void _screen_exit() {}

	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Dictionary _edit_get_state() const override;
	virtual void _edit_set_state(const Dictionary &p_state) override;

	virtual Vector2 _edit_get_minimum_size() const override { return Vector2(); }

	virtual void _edit_set_rect(const Rect2 &p_edit_rect) override;

	virtual Rect2 _edit_get_rect() const override;

	virtual bool _edit_use_rect() const override;
#endif // DEBUG_ENABLED

	void set_rect(const Rect2 &p_rect);
	Rect2 get_rect() const;

	void set_show_rect(bool p_show_rect);
	bool is_showing_rect() const;

	bool is_on_screen() const;

	VisibleOnScreenNotifier2D();
};

class VisibleOnScreenEnabler2D : public VisibleOnScreenNotifier2D {
	ORBLIT_CLASS(VisibleOnScreenEnabler2D, VisibleOnScreenNotifier2D);

public:
	enum EnableMode {
		ENABLE_MODE_INHERIT,
		ENABLE_MODE_ALWAYS,
		ENABLE_MODE_WHEN_PAUSED,
	};

protected:
	ObjectID node_id;
	virtual void _screen_enter() override;
	virtual void _screen_exit() override;

	EnableMode enable_mode = ENABLE_MODE_INHERIT;
	EntityPath enable_node_path = EntityPath("..");

	void _notification(int p_what);
	static void register_members();

	void _update_enable_mode(bool p_enable);

public:
	void set_enable_mode(EnableMode p_mode);
	EnableMode get_enable_mode();

	void set_enable_node_path(EntityPath p_path);
	EntityPath get_enable_node_path();

	VisibleOnScreenEnabler2D();
};

VARIANT_ENUM_CAST(VisibleOnScreenEnabler2D::EnableMode);
