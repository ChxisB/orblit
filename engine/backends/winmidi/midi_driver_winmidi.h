/**************************************************************************/
/*  midi_driver_winmidi.h                                                 */

#pragma once

#ifdef WINMIDI_ENABLED

#include "core/os/midi_driver.h"
#include "core/containers/vector.h"

#include <windows.h>

#include <mmsystem.h>

class MIDIDriverWinMidi : public MIDIDriver {
	Vector<HMIDIIN> connected_sources;

	static void CALLBACK read(HMIDIIN hMidiIn, UINT wMsg, DWORD_PTR dwInstance, DWORD_PTR dwParam1, DWORD_PTR dwParam2);

public:
	virtual Error open() override;
	virtual void close() override;

	MIDIDriverWinMidi() = default;
	virtual ~MIDIDriverWinMidi();
};

#endif // WINMIDI_ENABLED
