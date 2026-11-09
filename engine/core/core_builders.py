"""Functions used to generate source files during build time"""

import methods


# Generate disabled classes
def disabled_class_builder(target, source, env):
    with methods.generated_wrapper(str(target[0])) as file:
        for c in source[0].read():
            if cs := c.strip():
                file.write(f"class {cs}; template <> struct is_class_enabled<{cs}> : std::false_type {{}};\n")


# Generate version info
def version_info_builder(target, source, env):
    with methods.generated_wrapper(str(target[0])) as file:
        file.write(
            """\
#define ORBLIT_VERSION_SHORT_NAME "{short_name}"
#define ORBLIT_VERSION_NAME "{name}"
#define ORBLIT_VERSION_MAJOR {major}
#define ORBLIT_VERSION_MINOR {minor}
#define ORBLIT_VERSION_PATCH {patch}
#define ORBLIT_VERSION_STATUS "{status}"
#define ORBLIT_VERSION_BUILD "{build}"
#define ORBLIT_VERSION_MODULE_CONFIG "{module_config}"
#define ORBLIT_VERSION_WEBSITE "{website}"
#define ORBLIT_VERSION_DOCS_URL ORBLIT_VERSION_WEBSITE "/docs"
""".format(**source[0].read())
        )


def version_hash_builder(target, source, env):
    with methods.generated_wrapper(str(target[0])) as file:
        file.write(
            """\
#include "core/version.h"

const char *const ORBLIT_VERSION_HASH = "{git_hash}";
const unsigned long long ORBLIT_VERSION_TIMESTAMP = {git_timestamp};
""".format(**source[0].read())
        )


def encryption_key_builder(target, source, env):
    src = source[0].read() or "0" * 64
    try:
        buffer = bytes.fromhex(src)
        if len(buffer) != 32:
            raise ValueError
    except ValueError:
        methods.print_error(
            f'Invalid AES256 encryption key, not 64 hexadecimal characters: "{src}".\n'
            "Unset `SCRIPT_AES256_ENCRYPTION_KEY` in your environment "
            "or make sure that it contains exactly 64 hexadecimal characters."
        )
        raise

    with methods.generated_wrapper(str(target[0])) as file:
        file.write(
            f"""\
#include <cstdint>

uint8_t script_encryption_key[32] = {{
	{methods.format_buffer(buffer, 1)}
}};"""
        )


def make_certs_header(target, source, env):
    buffer = methods.get_buffer(str(source[0]))
    decomp_size = len(buffer)
    buffer = methods.compress_buffer_zstd(buffer, env)

    with methods.generated_wrapper(str(target[0])) as file:
        # System certs path. Editor will use them if defined. (for package maintainers)
        file.write('#define _SYSTEM_CERTS_PATH "{}"\n'.format(source[2].read() or ""))
        if source[1].read():
            # Defined here and not in env so changing it does not trigger a full rebuild.
            file.write(f"""\
#define BUILTIN_CERTS_ENABLED

inline constexpr int _certs_compressed_size = {len(buffer)};
inline constexpr int _certs_uncompressed_size = {decomp_size};
inline constexpr unsigned char _certs_compressed[] = {{
	{methods.format_buffer(buffer, 1)}
}};
""")


def make_license_header(target, source, env):
    with open(str(source[0]), "r", encoding="utf-8") as file:
        license_text = file.read()

    with methods.generated_wrapper(str(target[0])) as file:
        file.write(f"""\
inline constexpr const char *ORBLIT_LICENSE_TEXT = {{
{methods.to_raw_cstring(license_text)}
}};
""")
