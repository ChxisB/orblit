/**************************************************************************/
/*  blit_material.h                                                       */

#pragma once

#include "scene/resources/material.h"

class BlitMaterial : public Material {
	ORBLIT_CLASS(BlitMaterial, Material);

public:
	enum BlendMode {
		BLEND_MODE_MIX,
		BLEND_MODE_ADD,
		BLEND_MODE_SUB,
		BLEND_MODE_MUL,
		BLEND_MODE_DISABLED,
		BLEND_MODE_PREMULTIPLIED_ALPHA,
	};

private:
	static Mutex shader_mutex;
	static RID shader_cache[5];
	static void _update_shader(BlendMode p_blend);
	mutable bool shader_set = false;

	BlendMode blend_mode = BLEND_MODE_MIX;

protected:
	static void register_members();

public:
	void set_blend_mode(BlendMode p_blend_mode);
	BlendMode get_blend_mode() const;

	virtual Shader::Mode get_shader_mode() const override;
	virtual RID get_shader_rid() const override;
	virtual RID get_rid() const override;

	static void cleanup_shader();

	BlitMaterial();
	virtual ~BlitMaterial();
};

VARIANT_ENUM_CAST(BlitMaterial::BlendMode);
