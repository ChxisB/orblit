/**************************************************************************/
/*  navigation_region_planar_editor_plugin.h                                  */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "editor/scene/planar/abstract_polygon_planar_editor.h"

class AcceptDialog;
class HBoxContainer;
class NavigationPolygon;
class NavigationRegion2D;
class Timer;

class NavigationRegion2DEditor : public AbstractPolygon2DEditor {
	friend class NavigationRegion2DEditorPlugin;

	ORBLIT_CLASS(NavigationRegion2DEditor, AbstractPolygon2DEditor);

	NavigationRegion2D *node = nullptr;

	Ref<NavigationPolygon> _ensure_navpoly() const;

	AcceptDialog *err_dialog = nullptr;

	HBoxContainer *bake_hbox = nullptr;
	Button *button_bake = nullptr;
	Button *button_reset = nullptr;
	Label *bake_info = nullptr;

	Timer *rebake_timer = nullptr;
	float _rebake_timer_delay = 1.5;
	void _rebake_timer_timeout();

	void _bake_pressed();
	void _clear_pressed();

	void _update_polygon_editing_state();

protected:
	void _notification(int p_what);

	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_polygon) override;

	virtual int _get_polygon_count() const override;
	virtual Variant _get_polygon(int p_idx) const override;
	virtual void _set_polygon(int p_idx, const Variant &p_polygon) const override;

	virtual void _action_add_polygon(const Variant &p_polygon) override;
	virtual void _action_remove_polygon(int p_idx) override;
	virtual void _action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) override;

	virtual bool _has_resource() const override;
	virtual void _create_resource() override;

public:
	NavigationRegion2DEditor();
};

class NavigationRegion2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(NavigationRegion2DEditorPlugin, AbstractPolygon2DEditorPlugin);

	NavigationRegion2DEditor *navigation_polygon_editor = nullptr;

public:
	NavigationRegion2DEditorPlugin();
};
