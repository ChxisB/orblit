/**************************************************************************/
/*  semver.h                                                              */

#pragma once

#include "core/string/regex.h"
#include "core/string/ustring.h"

// <sys/sysmacros.h> is included somewhere, which defines major(dev) to gnu_dev_major(dev)
#if defined(major)
#undef major
#endif
#if defined(minor)
#undef minor
#endif

namespace orblitsharp {

struct SemVer {
private:
	static bool parse_digit_only_field(const String &p_field, uint64_t &r_result);

	static int cmp(const SemVer &p_a, const SemVer &p_b);

public:
	int major = 0;
	int minor = 0;
	int patch = 0;
	String prerelease;
	String build_metadata;

	bool operator==(const SemVer &b) const {
		return cmp(*this, b) == 0;
	}

	bool operator!=(const SemVer &b) const {
		return !operator==(b);
	}

	bool operator<(const SemVer &b) const {
		return cmp(*this, b) < 0;
	}

	bool operator>(const SemVer &b) const {
		return cmp(*this, b) > 0;
	}

	bool operator<=(const SemVer &b) const {
		return cmp(*this, b) <= 0;
	}

	bool operator>=(const SemVer &b) const {
		return cmp(*this, b) >= 0;
	}

	SemVer() {}

	SemVer(int p_major, int p_minor, int p_patch,
			const String &p_prerelease, const String &p_build_metadata) :
			major(p_major),
			minor(p_minor),
			patch(p_patch),
			prerelease(p_prerelease),
			build_metadata(p_build_metadata) {
	}
};

struct SemVerParser {
private:
	RegEx regex;

public:
	bool parse(const String &p_ver_text, SemVer &r_semver);
};

} //namespace orblitsharp
