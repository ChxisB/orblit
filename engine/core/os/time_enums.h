/**************************************************************************/
/*  time_enums.h                                                          */

#pragma once

#include <cstdint>

enum Month {
	/// Start at 1 to follow Windows SYSTEMTIME structure
	/// https://msdn.microsoft.com/en-us/library/windows/desktop/ms724950(v=vs.85).aspx
	MONTH_JANUARY = 1,
	MONTH_FEBRUARY,
	MONTH_MARCH,
	MONTH_APRIL,
	MONTH_MAY,
	MONTH_JUNE,
	MONTH_JULY,
	MONTH_AUGUST,
	MONTH_SEPTEMBER,
	MONTH_OCTOBER,
	MONTH_NOVEMBER,
	MONTH_DECEMBER,
};

enum Weekday : uint8_t {
	WEEKDAY_SUNDAY,
	WEEKDAY_MONDAY,
	WEEKDAY_TUESDAY,
	WEEKDAY_WEDNESDAY,
	WEEKDAY_THURSDAY,
	WEEKDAY_FRIDAY,
	WEEKDAY_SATURDAY,
};
