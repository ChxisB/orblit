/**************************************************************************/
/*  type_info.cpp                                                         */

#include "core/variant/type_info.h"

namespace OrblitTypeInfo {
namespace Internal {
String enum_qualified_name_to_class_info_name(const char *p_qualified_name) {
	Vector<String> parts = String(p_qualified_name).split("::", false);
	if (parts.size() <= 2) {
		return String(".").join(parts);
	}
	// Contains namespace. We only want the class and enum names.
	return parts[parts.size() - 2] + "." + parts[parts.size() - 1];
}
} // namespace Internal
} // namespace OrblitTypeInfo
