/**************************************************************************/
/*  ip_windows.cpp                                                        */

#if defined(WINDOWS_ENABLED)

#include "ip_windows.h"

#include <winsock2.h>
#include <ws2tcpip.h>
// Include winsock2 before iphlpapi.
#include <iphlpapi.h>

static IPAddress _sockaddr2ip(struct sockaddr *p_addr) {
	IPAddress ip;

	if (p_addr->sa_family == AF_INET) {
		struct sockaddr_in *addr = (struct sockaddr_in *)p_addr;
		ip.set_ipv4((uint8_t *)&(addr->sin_addr));
	} else if (p_addr->sa_family == AF_INET6) {
		struct sockaddr_in6 *addr6 = (struct sockaddr_in6 *)p_addr;
		ip.set_ipv6(addr6->sin6_addr.s6_addr);
	}

	return ip;
}

void IPWindows::_resolve_hostname(List<IPAddress> &r_addresses, const String &p_hostname, Type p_type) const {
	struct addrinfo hints;
	struct addrinfo *result = nullptr;

	memset(&hints, 0, sizeof(struct addrinfo));
	if (p_type == TYPE_IPV4) {
		hints.ai_family = AF_INET;
	} else if (p_type == TYPE_IPV6) {
		hints.ai_family = AF_INET6;
		hints.ai_flags = 0;
	} else {
		hints.ai_family = AF_UNSPEC;
		hints.ai_flags = AI_ADDRCONFIG;
	}
	hints.ai_flags &= ~AI_NUMERICHOST;

	int s = getaddrinfo(p_hostname.utf8().get_data(), nullptr, &hints, &result);
	if (s != 0) {
		print_verbose("getaddrinfo failed! Cannot resolve hostname.");
		return;
	}

	if (result == nullptr || result->ai_addr == nullptr) {
		print_verbose("Invalid response from getaddrinfo.");
		if (result) {
			freeaddrinfo(result);
		}
		return;
	}

	struct addrinfo *next = result;

	do {
		if (next->ai_addr == nullptr) {
			next = next->ai_next;
			continue;
		}
		IPAddress ip = _sockaddr2ip(next->ai_addr);
		if (ip.is_valid() && !r_addresses.find(ip)) {
			r_addresses.push_back(ip);
		}
		next = next->ai_next;
	} while (next);

	freeaddrinfo(result);
}

void IPWindows::get_local_interfaces(HashMap<String, Interface_Info> *r_interfaces) const {
	ULONG buf_size = 1024;
	IP_ADAPTER_ADDRESSES *addrs;

	while (true) {
		addrs = (IP_ADAPTER_ADDRESSES *)memalloc(buf_size);
		int err = GetAdaptersAddresses(AF_UNSPEC, GAA_FLAG_SKIP_ANYCAST | GAA_FLAG_SKIP_MULTICAST | GAA_FLAG_SKIP_DNS_SERVER | GAA_FLAG_SKIP_FRIENDLY_NAME,
				nullptr, addrs, &buf_size);
		if (err == NO_ERROR) {
			break;
		}
		memfree(addrs);
		if (err == ERROR_BUFFER_OVERFLOW) {
			continue; // Will go back and alloc the right size.
		}

		ERR_FAIL_MSG("Call to GetAdaptersAddresses failed with error " + itos(err) + ".");
	}

	IP_ADAPTER_ADDRESSES *adapter = addrs;

	while (adapter != nullptr) {
		Interface_Info info;
		info.name = adapter->AdapterName;
		info.name_friendly = adapter->FriendlyName;
		info.index = String::num_uint64(adapter->IfIndex);

		IP_ADAPTER_UNICAST_ADDRESS *address = adapter->FirstUnicastAddress;
		while (address != nullptr) {
			int family = address->Address.lpSockaddr->sa_family;
			if (family != AF_INET && family != AF_INET6) {
				continue;
			}
			info.ip_addresses.push_front(_sockaddr2ip(address->Address.lpSockaddr));
			address = address->Next;
		}
		adapter = adapter->Next;
		// Only add interface if it has at least one IP.
		if (info.ip_addresses.size() > 0) {
			r_interfaces->insert(info.name, info);
		}
	}

	memfree(addrs);
}

void IPWindows::make_default() {
	_create = _create_unix;
}

IP *IPWindows::_create_unix() {
	return memnew(IPWindows);
}

IPWindows::IPWindows() {
}

#endif // WINDOWS_ENABLED
