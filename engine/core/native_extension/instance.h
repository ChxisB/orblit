/**************************************************************************/
/*  instance.h                                                            */

#pragma once

#include "core/native_extension/native_extension_interface.gen.h"
#include "core/object/object.h"

class OrblitInstance : public Object {
	ORBLIT_CLASS(OrblitInstance, Object);

	bool started = false;

protected:
	static void register_members();

public:
	OrblitInstance();
	~OrblitInstance();

	bool initialize(NativeExtensionInitializationFunction p_init_func);

	bool start();
	bool is_started();
	bool iteration();
	void stop();

	void focus_out();
	void focus_in();
	void pause();
	void resume();
};
