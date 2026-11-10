/**************************************************************************/
/*  visible_on_screen_notifier_spatial.h                                       */

#pragma once

#include "scene/spatial/visual_instance_spatial.h"

class VisibleOnScreenNotifier3D : public VisualInstance3D {
	ORBLIT_CLASS(VisibleOnScreenNotifier3D, VisualInstance3D);

	AABB aabb = AABB(Vector3(-1, -1, -1), Vector3(2, 2, 2));

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
	void set_aabb(const AABB &p_aabb);
	virtual AABB get_aabb() const override;
	bool is_on_screen() const;

	VisibleOnScreenNotifier3D();
	~VisibleOnScreenNotifier3D();
};

class VisibleOnScreenEnabler3D : public VisibleOnScreenNotifier3D {
	ORBLIT_CLASS(VisibleOnScreenEnabler3D, VisibleOnScreenNotifier3D);

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

	VisibleOnScreenEnabler3D();
};

VARIANT_ENUM_CAST(VisibleOnScreenEnabler3D::EnableMode);
