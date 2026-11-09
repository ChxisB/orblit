/**************************************************************************/
/*  editor_script.h                                                       */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"

class EditorInterface;
class Entity;

class EditorScript : public RefCounted {
	ORBLIT_CLASS(EditorScript, RefCounted);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0_REQUIRED(_run)

public:

	virtual void run();
};
