/**************************************************************************/
/*  ip_address.h                                                          */

#pragma once

#include "core/string/ustring.h"

struct [[nodiscard]] _WARN_UNUSED_ IPAddress {
private:
	union {
		uint8_t field8[16];
		uint16_t field16[8];
		uint32_t field32[4];
	};

	enum {
		IPV4_MAX_STRING_LENGTH = 16,
		IPV6_MAX_STRING_LENGTH = 40,
	};

	bool valid;
	bool wildcard;

protected:
	static bool _parse_ipv6(const String &p_string, IPAddress &r_ip);
	static bool _parse_ipv4(const String &p_string, int p_start, uint8_t *r_dest);

public:
	//operator Variant() const;
	bool operator==(const IPAddress &p_ip) const {
		if (p_ip.valid != valid) {
			return false;
		}
		if (!valid) {
			return false;
		}
		for (int i = 0; i < 4; i++) {
			if (field32[i] != p_ip.field32[i]) {
				return false;
			}
		}
		return true;
	}

	bool operator!=(const IPAddress &p_ip) const {
		if (p_ip.valid != valid) {
			return true;
		}
		if (!valid) {
			return true;
		}
		for (int i = 0; i < 4; i++) {
			if (field32[i] != p_ip.field32[i]) {
				return true;
			}
		}
		return false;
	}

	static bool is_valid_ip_address(const String &p_string);

	void clear();
	bool is_wildcard() const { return wildcard; }
	bool is_valid() const { return valid; }
	bool is_ipv4() const;
	const uint8_t *get_ipv4() const;
	void set_ipv4(const uint8_t *p_ip);

	const uint8_t *get_ipv6() const;
	void set_ipv6(const uint8_t *p_buf);

	explicit operator String() const;
	IPAddress(const String &p_string);
	IPAddress(uint32_t p_a, uint32_t p_b, uint32_t p_c, uint32_t p_d, bool p_is_v6 = false);
	IPAddress() { clear(); }
};

// Zero-constructing IPAddress initializes field, valid, and wildcard to 0 (and thus empty).
template <>
struct is_zero_constructible<IPAddress> : std::true_type {};
