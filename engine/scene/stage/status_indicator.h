/**************************************************************************/
/*  status_indicator.h                                                    */

#pragma once

#include "scene/stage/entity.h"
#include "scene/resources/texture.h"
#include "systems/display/display_system_enums.h"

class StatusIndicator : public Entity {
	ORBLIT_CLASS(StatusIndicator, Entity);

	Ref<Texture2D> icon;
	String tooltip;
	bool visible = true;
	DisplaySystemEnums::IndicatorID iid = DisplaySystemEnums::INVALID_INDICATOR_ID;
	EntityPath menu;

protected:
	void _notification(int p_what);
	static void register_members();

	void _callback(MouseButton p_index, const Point2i &p_pos);

public:
	void set_icon(const Ref<Texture2D> &p_icon);
	Ref<Texture2D> get_icon() const;

	void set_tooltip(const String &p_tooltip);
	String get_tooltip() const;

	void set_menu(const EntityPath &p_menu);
	EntityPath get_menu() const;

	void set_visible(bool p_visible);
	bool is_visible() const;

	Rect2 get_rect() const;
};
