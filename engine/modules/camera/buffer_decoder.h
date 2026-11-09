/**************************************************************************/
/*  buffer_decoder.h                                                      */

#pragma once

#include "core/containers/vector.h"
#include "core/io/image.h"

class CameraFeed;

struct StreamingBuffer {
	void *start = nullptr;
	size_t length = 0;
};

class BufferDecoder {
protected:
	CameraFeed *camera_feed = nullptr;
	Ref<Image> image;
	int width = 0;
	int height = 0;

public:
	virtual void decode(StreamingBuffer p_buffer) = 0;

	BufferDecoder(CameraFeed *p_camera_feed);
	virtual ~BufferDecoder() {}
};

class AbstractYuyvBufferDecoder : public BufferDecoder {
protected:
	int *component_indexes = nullptr;

public:
	AbstractYuyvBufferDecoder(CameraFeed *p_camera_feed);
	~AbstractYuyvBufferDecoder();
};

class SeparateYuyvBufferDecoder : public AbstractYuyvBufferDecoder {
private:
	Vector<uint8_t> y_image_data;
	Vector<uint8_t> cbcr_image_data;
	Ref<Image> y_image;
	Ref<Image> cbcr_image;

public:
	SeparateYuyvBufferDecoder(CameraFeed *p_camera_feed);
	virtual void decode(StreamingBuffer p_buffer) override;
};

class YuyvToGrayscaleBufferDecoder : public AbstractYuyvBufferDecoder {
private:
	Vector<uint8_t> image_data;

public:
	YuyvToGrayscaleBufferDecoder(CameraFeed *p_camera_feed);
	virtual void decode(StreamingBuffer p_buffer) override;
};

class YuyvToRgbBufferDecoder : public AbstractYuyvBufferDecoder {
private:
	Vector<uint8_t> image_data;

public:
	YuyvToRgbBufferDecoder(CameraFeed *p_camera_feed);
	virtual void decode(StreamingBuffer p_buffer) override;
};

class CopyBufferDecoder : public BufferDecoder {
private:
	Vector<uint8_t> image_data;
	bool rgba = false;

public:
	CopyBufferDecoder(CameraFeed *p_camera_feed, bool p_rgba);
	virtual void decode(StreamingBuffer p_buffer) override;
};

class JpegBufferDecoder : public BufferDecoder {
private:
	Vector<uint8_t> image_data;

public:
	JpegBufferDecoder(CameraFeed *p_camera_feed);
	virtual void decode(StreamingBuffer p_buffer) override;
};
