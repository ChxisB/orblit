/**************************************************************************/
/*  lipo.h                                                                */

#pragma once

// Universal / Universal 2 fat binary file creator and extractor.

#include "core/io/file_access.h"
#include "core/object/ref_counted.h"

class LipO : public RefCounted {
	ORBLIT_SOFTCLASS(LipO, RefCounted);

	struct FatArch {
		uint32_t cputype;
		uint32_t cpusubtype;
		uint64_t offset;
		uint64_t size;
		uint32_t align;
	};

	Ref<FileAccess> fa;
	Vector<FatArch> archs;

	static inline size_t PAD(size_t s, size_t a) {
		return (a - s % a);
	}

	static bool _create_file(const String &p_output_path, const Vector<Ref<FileAccess>> &p_files, const Vector<Vector2i> &p_cputypes);

public:
	static bool is_lipo(const String &p_path);
	static bool is_lipo(const PackedByteArray &p_buffer);
	static bool is_lipo(const Ref<FileAccess> &p_fa);

	static bool create_file(const String &p_output_path, const Vector<String> &p_files, const Vector<Vector2i> &p_cputypes = Vector<Vector2i>());
	static bool create_file(const String &p_output_path, const Vector<PackedByteArray> &p_file_buffers, const Vector<Vector2i> &p_cputypes = Vector<Vector2i>());

	bool open_file(const String &p_path);
	bool open_buffer(const PackedByteArray &p_buffer);
	bool open_file(const Ref<FileAccess> &p_fa);

	int get_arch_count() const;
	uint32_t get_arch_cputype(int p_index) const;
	uint32_t get_arch_cpusubtype(int p_index) const;
	bool extract_arch(int p_index, const String &p_path);

	void close();

	~LipO();
};
