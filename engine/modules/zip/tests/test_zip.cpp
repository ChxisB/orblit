/**************************************************************************/
/*  test_zip.cpp                                                          */

#include "test_zip.h"

namespace TestZip {

void check_file_size(const String &p_path, int p_expected_size) {
	Ref<FileAccess> f = FileAccess::open(p_path, FileAccess::READ);
	CHECK(f.is_valid());
	CHECK(f->get_length() == p_expected_size);
}

} // namespace TestZip
