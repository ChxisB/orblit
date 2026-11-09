/**************************************************************************/
/*  file_info.h                                                           */

#pragma once

#include "core/containers/list.h"
#include "core/string/string_name.h"

enum class FileSortOption {
	FILE_SORT_NAME = 0,
	FILE_SORT_NAME_REVERSE = 1,
	FILE_SORT_TYPE = 2,
	FILE_SORT_TYPE_REVERSE = 3,
	FILE_SORT_MODIFIED_TIME = 4,
	FILE_SORT_MODIFIED_TIME_REVERSE = 5,
	FILE_SORT_MAX = 6,
};

struct FileInfo {
	String name;
	String path;
	String icon_path;
	StringName type;
	Vector<String> sources;
	bool import_broken = false;
	uint64_t modified_time = 0;

	bool operator<(const FileInfo &p_fi) const {
		return FileNoCaseComparator()(name, p_fi.name);
	}
};

struct FileInfoTypeComparator {
	bool operator()(const FileInfo &p_a, const FileInfo &p_b) const {
		return FileNoCaseComparator()(p_a.name.get_extension() + p_a.type + p_a.name.get_basename(), p_b.name.get_extension() + p_b.type + p_b.name.get_basename());
	}
};

struct FileInfoModifiedTimeComparator {
	bool operator()(const FileInfo &p_a, const FileInfo &p_b) const {
		return p_a.modified_time > p_b.modified_time;
	}
};

void sort_file_info_list(List<FileInfo> &r_file_list, FileSortOption p_file_sort_option);
