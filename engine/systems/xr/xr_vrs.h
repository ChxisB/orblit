/**************************************************************************/
/*  xr_vrs.h                                                              */

#pragma once

#include "core/containers/rid.h"
#include "core/containers/vector.h"
#include "core/math/vector2i.h"
#include "core/object/object.h"

/* This is a helper class for generating stereoscopic VRS images */

class XRVRS : public Object {
	ORBLIT_CLASS(XRVRS, Object);

private:
	float vrs_min_radius = 20.0;
	float vrs_strength = 1.0;
	Rect2i vrs_render_region;
	bool vrs_dirty = true;

	RID vrs_texture;
	Size2i target_size;
	Vector<Vector2> eye_foci;

protected:
	static void register_members();

public:
	~XRVRS();

	float get_vrs_min_radius() const;
	void set_vrs_min_radius(float p_vrs_min_radius);
	float get_vrs_strength() const;
	void set_vrs_strength(float p_vrs_strength);
	Rect2i get_vrs_render_region() const;
	void set_vrs_render_region(const Rect2i &p_vrs_render_region);

	RID make_vrs_texture(const Size2 &p_target_size, const Vector<Vector2> &p_eye_foci);
};
