/**************************************************************************/
/*  openxr_foveated_inset_viewport.h                                      */

#pragma once

#include "scene/spatial/xr/xr_nodes.h"
#include "scene/stage/viewport.h"

class OpenXRFoveatedInsetViewport : public SubViewport {
	ORBLIT_CLASS(OpenXRFoveatedInsetViewport, SubViewport);

public:
	OpenXRFoveatedInsetViewport();
	~OpenXRFoveatedInsetViewport();

	XROrigin3D *get_xr_origin3d() const { return xr_origin; }
	XRCamera3D *get_xr_camera3d() const { return xr_camera; }

protected:
	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;
	void _notification(int p_what);

private:
	void _update_size();

	XROrigin3D *xr_origin = nullptr;
	XRCamera3D *xr_camera = nullptr;
};
