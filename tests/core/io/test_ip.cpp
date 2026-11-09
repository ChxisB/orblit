/**************************************************************************/
/*  test_ip.cpp                                                           */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_ip)

#include "core/io/ip.h"

namespace TestIP {

TEST_CASE("[IP] resolve_hostname") {
	for (int x = 0; x < 1000; x++) {
		IPAddress IPV4 = IP::get_singleton()->resolve_hostname("localhost", IP::TYPE_IPV4);
		CHECK("127.0.0.1" == String(IPV4));
		IPAddress IPV6 = IP::get_singleton()->resolve_hostname("localhost", IP::TYPE_IPV6);
		CHECK("0:0:0:0:0:0:0:1" == String(IPV6));
	}
}

} // namespace TestIP
