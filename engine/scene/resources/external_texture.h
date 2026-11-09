/**************************************************************************/
/*  external_texture.h                                                    */

#pragma once

#include "scene/resources/texture.h"

// External textures as defined by OES_EGL_image_external (GLES) or VK_ANDROID_external_memory_android_hardware_buffer (Vulkan).
class ExternalTexture : public Texture2D {
	ORBLIT_CLASS(ExternalTexture, Texture2D);

private:
	mutable RID texture;
	mutable bool using_placeholder = false;
	Size2 size = Size2(256, 256);
	uint64_t external_buffer = 0;

	void _ensure_created() const;

protected:
	static void register_members();

public:
	uint64_t get_external_texture_id() const;

	virtual Size2 get_size() const override;
	void set_size(const Size2 &p_size);

	void set_external_buffer_id(uint64_t p_external_buffer);

	virtual int get_width() const override;
	virtual int get_height() const override;

	virtual RID get_rid() const override;
	virtual bool has_alpha() const override;

	ExternalTexture();
	~ExternalTexture();
};
