/**************************************************************************/
/*  engine_profiler.h                                                     */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"

class EngineProfiler : public RefCounted {
	ORBLIT_CLASS(EngineProfiler, RefCounted);

private:
	String registration;

protected:
	static void register_members();

public:
	virtual void toggle(bool p_enable, const Array &p_opts);
	virtual void add(const Array &p_data);
	virtual void tick(double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time);

	Error bind(const String &p_name);
	Error unbind();
	bool is_bound() const { return registration.length() > 0; }

	ORBLIT_VIRTUAL2(_toggle, bool, Array);
	ORBLIT_VIRTUAL1(_add_frame, Array);
	ORBLIT_VIRTUAL4(_tick, double, double, double, double);

	virtual ~EngineProfiler();
};
