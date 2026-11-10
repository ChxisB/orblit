/**************************************************************************/
/*  tts_macos.h                                                           */

#pragma once

#include "core/containers/hash_map.h"
#include "core/containers/list.h"
#include "core/string/ustring.h"
#include "core/variant/array.h"

#import <AppKit/AppKit.h>

#if __has_include(<AVFAudio/AVSpeechSynthesis.h>)
#import <AVFAudio/AVSpeechSynthesis.h>
#else
#import <AVFoundation/AVFoundation.h>
#endif

struct TTSUtterance;

@interface TTS_MacOS : NSObject <AVSpeechSynthesizerDelegate> {
	bool speaking;
	HashMap<id, int64_t> ids;

	id synth;
	List<TTSUtterance> queue;
}

- (void)pauseSpeaking;
- (void)resumeSpeaking;
- (void)stopSpeaking;
- (bool)isSpeaking;
- (bool)isPaused;
- (void)speak:(const String &)text voice:(const String &)voice volume:(int)volume pitch:(float)pitch rate:(float)rate utterance_id:(int64_t)utterance_id interrupt:(bool)interrupt;
- (Array)getVoices;
@end
