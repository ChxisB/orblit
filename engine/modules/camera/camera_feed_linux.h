/**************************************************************************/
/*  camera_feed_linux.h                                                   */

#pragma once

#include "buffer_decoder.h"

#include "core/os/thread.h"
#include "systems/camera/camera_feed.h"

#include <linux/videodev2.h>

struct StreamingBuffer;

class CameraFeedLinux : public CameraFeed {
	ORBLIT_SOFTCLASS(CameraFeedLinux, CameraFeed);

private:
	SafeFlag exit_flag;
	Thread *thread = nullptr;
	String device_name;
	int file_descriptor = -1;
	StreamingBuffer *buffers = nullptr;
	unsigned int buffer_count = 0;
	BufferDecoder *buffer_decoder = nullptr;

	static void update_buffer_thread_func(void *p_func);

	void _update_buffer();
	void _query_device(const String &p_device_name);
	void _add_format(v4l2_fmtdesc description, v4l2_frmsize_discrete size, int frame_numerator, int frame_denominator);
	bool _request_buffers();
	bool _start_capturing();
	void _read_frame();
	void _stop_capturing();
	void _unmap_buffers(unsigned int p_count);
	BufferDecoder *_create_buffer_decoder();
	void _start_thread();

public:
	String get_device_name() const;
	bool activate_feed() override;
	void deactivate_feed() override;
	bool set_format(int p_index, const Dictionary &p_parameters) override;
	Array get_formats() const override;
	FeedFormat get_format() const override;

	CameraFeedLinux(const String &p_device_name);
	~CameraFeedLinux() override;
};
