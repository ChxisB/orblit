/**************************************************************************/
/*  render_buffer_custom_data_rd.h                                        */

#pragma once

#include "core/object/ref_counted.h"

class RenderSceneBuffersRD;

class RenderBufferCustomDataRD : public RefCounted {
	ORBLIT_CLASS(RenderBufferCustomDataRD, RefCounted);

public:
	virtual void configure(RenderSceneBuffersRD *p_render_buffers) = 0;
	virtual void free_data() = 0; // called on cleanup

private:
};
