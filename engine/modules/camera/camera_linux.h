/**************************************************************************/
/*  camera_linux.h                                                        */

#pragma once

#include "core/containers/safe_refcount.h"
#include "core/os/mutex.h"
#include "core/os/thread.h"
#include "systems/camera/camera_system.h"

class CameraLinux : public CameraSystem {
private:
	SafeFlag exit_flag;
	Thread camera_thread;
	Mutex camera_mutex;

	static void camera_thread_func(void *p_camera_linux);

	void _update_devices();
	bool _has_device(const String &p_device_name);
	void _add_device(const String &p_device_name);
	void _remove_device(const String &p_device_name);
	int _open_device(const String &p_device_name);
	bool _is_active(const String &p_device_name);
	bool _is_video_capture_device(int p_file_descriptor);
	bool _can_query_format(int p_file_descriptor, int p_type);

public:
	CameraLinux() = default;
	~CameraLinux();

	void set_monitoring_feeds(bool p_monitoring_feeds) override;
};
