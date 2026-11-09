/**************************************************************************/
/*  file_info.cpp                                                         */

#include "file_info.h"

void sort_file_info_list(List<FileInfo> &r_file_list, FileSortOption p_file_sort_option) {
	// Sort the file list if needed.
	switch (p_file_sort_option) {
		case FileSortOption::FILE_SORT_TYPE:
			r_file_list.sort_custom<FileInfoTypeComparator>();
			break;
		case FileSortOption::FILE_SORT_TYPE_REVERSE:
			r_file_list.sort_custom<FileInfoTypeComparator>();
			r_file_list.reverse();
			break;
		case FileSortOption::FILE_SORT_MODIFIED_TIME:
			r_file_list.sort_custom<FileInfoModifiedTimeComparator>();
			break;
		case FileSortOption::FILE_SORT_MODIFIED_TIME_REVERSE:
			r_file_list.sort_custom<FileInfoModifiedTimeComparator>();
			r_file_list.reverse();
			break;
		case FileSortOption::FILE_SORT_NAME_REVERSE:
			r_file_list.sort();
			r_file_list.reverse();
			break;
		case FileSortOption::FILE_SORT_NAME:
			r_file_list.sort();
			break;
		default:
			ERR_FAIL_MSG("Invalid file sort option.");
			break;
	}
}
