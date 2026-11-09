/**************************************************************************/
/*  editor_scale.h                                                        */

#pragma once

class EditorScale {
	static float _scale;

public:
	static void set_scale(float p_scale);
	static float get_scale();
};

#define EDSCALE (EditorScale::get_scale())

#define EDSCALE_RND(m_value) (Math::round((m_value) * EDSCALE))
