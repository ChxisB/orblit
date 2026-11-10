/**************************************************************************/
/*  openxr_visibility_mask.h                                              */

#pragma once

#include "scene/spatial/visual_instance_spatial.h"

class OpenXRVisibilityMask : public VisualInstance3D {
	ORBLIT_CLASS(OpenXRVisibilityMask, VisualInstance3D);

protected:
	void _notification(int p_what);

	void _on_openxr_session_begun();
	void _on_openxr_session_stopping();

public:
	virtual PackedStringArray get_configuration_warnings() const override;

	virtual AABB get_aabb() const override;

	OpenXRVisibilityMask();
	~OpenXRVisibilityMask();
};
