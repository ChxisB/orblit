import 'dart:io';

import 'backend.dart';
import 'linux.dart';

/// The backend this platform has, or one with nothing attached.
PadBackend platformBackend() => Platform.isLinux ? LinuxPads() : noPads;
