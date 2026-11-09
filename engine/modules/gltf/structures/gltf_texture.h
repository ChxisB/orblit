/**************************************************************************/
/*  gltf_texture.h                                                        */

#pragma once

#include "../gltf_defines.h"

#include "core/io/resource.h"

class GLTFTexture : public Resource {
	ORBLIT_CLASS(GLTFTexture, Resource);

private:
	GLTFImageIndex src_image = -1;
	GLTFTextureSamplerIndex sampler = -1;

protected:
	static void register_members();

public:
	GLTFImageIndex get_src_image() const;
	void set_src_image(GLTFImageIndex val);
	GLTFTextureSamplerIndex get_sampler() const;
	void set_sampler(GLTFTextureSamplerIndex val);
};
