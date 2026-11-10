/**************************************************************************/
/*  xr_tracker.h                                                          */

#pragma once

#include "core/os/thread_safe.h"
#include "systems/xr/xr_system.h"

/**
	The XR tracker object is a common base for all different types of XR trackers.
*/

class XRTracker : public RefCounted {
	ORBLIT_CLASS(XRTracker, RefCounted);
	_THREAD_SAFE_CLASS_

protected:
	XRSystem::TrackerType type = XRSystem::TRACKER_UNKNOWN; // type of tracker
	StringName name = "Unknown"; // (unique) name of the tracker
	String description; // description of the tracker

	static void register_members();

public:
	virtual void set_tracker_type(XRSystem::TrackerType p_type);
	XRSystem::TrackerType get_tracker_type() const;
	void set_tracker_name(const StringName &p_name);
	StringName get_tracker_name() const;
	void set_tracker_desc(const String &p_desc);
	String get_tracker_desc() const;
};
