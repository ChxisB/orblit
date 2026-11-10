/**************************************************************************/
/*  icudata_stub.cpp                                                      */

#include <unicode/udata.h>
#include <unicode/utypes.h>
#include <unicode/uversion.h>

typedef struct {
	uint16_t header_size;
	uint8_t magic_1, magic_2;
	UDataInfo info;
	char padding[8];
	uint32_t count, reserved;
	int fake_name_and_data[4];
} ICU_data_header;

extern "C" U_EXPORT const ICU_data_header U_ICUDATA_ENTRY_POINT = {
	32,
	0xDA, 0x27,
	{ sizeof(UDataInfo),
			0,
#if U_IS_BIG_ENDIAN
			1,
#else
			0,
#endif
			U_CHARSET_FAMILY,
			sizeof(UChar),
			0,
			{ 0x54, 0x6F, 0x43, 0x50 },
			{ 1, 0, 0, 0 },
			{ 0, 0, 0, 0 } },
	{ 0, 0, 0, 0, 0, 0, 0, 0 },
	0, 0,
	{ 0, 0, 0, 0 }
};
