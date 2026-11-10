/**************************************************************************/
/*  feed_effects.h                                                        */

#pragma once

#ifdef GLES3_ENABLED

#include "backends/opengl/shaders/feed.glsl.gen.h"

namespace GLES3 {

class FeedEffects {
private:
	struct Feed {
		FeedShaderGLES3 shader;
		RID shader_version;
	} feed;

	static FeedEffects *singleton;

	GLuint screen_triangle = 0;
	GLuint screen_triangle_array = 0;

public:
	static FeedEffects *get_singleton();

	FeedEffects();
	~FeedEffects();

	void draw();

private:
	void draw_screen_triangle();
};

} // namespace GLES3

#endif // GLES3_ENABLED
