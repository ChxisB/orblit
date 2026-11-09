/**************************************************************************/
/*  mesh_texture.h                                                        */

#pragma once

#include "scene/resources/texture.h"

class Mesh;

class MeshTexture : public Texture2D {
	ORBLIT_CLASS(MeshTexture, Texture2D);
	RES_BASE_EXTENSION("meshtex");

	Ref<Texture2D> base_texture;
	Ref<Mesh> mesh;
	Size2i size;

protected:
	static void register_members();

public:
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual RID get_rid() const override;

	virtual bool has_alpha() const override;

	void set_mesh(const Ref<Mesh> &p_mesh);
	Ref<Mesh> get_mesh() const;

	void set_image_size(const Size2 &p_size);
	Size2 get_image_size() const;

	void set_base_texture(const Ref<Texture2D> &p_texture);
	Ref<Texture2D> get_base_texture() const;

	virtual void draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile = false, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false, bool p_clip_uv = true) const override;
	virtual bool get_rect_region(const Rect2 &p_rect, const Rect2 &p_src_rect, Rect2 &r_rect, Rect2 &r_src_rect) const override;

	bool is_pixel_opaque(int p_x, int p_y) const override;

	MeshTexture();
};
