/**************************************************************************/
/*  dxil_hash.h                                                           */

#pragma once

#include <windows.h>

void compute_dxil_hash(const BYTE *pData, UINT byteCount, BYTE *pOutHash);
