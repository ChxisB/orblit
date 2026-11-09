/**************************************************************************/
/*  marker_planar.h                                                           */

#pragma once

#include "scene/planar/entity_planar.h"

class Marker2D : public Entity2D {
	ORBLIT_CLASS(Marker2D, Entity2D);

	real_t gizmo_extents = 10.0;

	void _draw_cross();

protected:
	void _notification(int p_what);
	static void register_members();

public:
#ifdef DEBUG_ENABLED
	virtual Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
#endif // DEBUG_ENABLED

	void set_gizmo_extents(real_t p_extents);
	real_t get_gizmo_extents() const;

	Marker2D();
};
