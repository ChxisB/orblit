import 'dart:convert';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

/// What orblit_import makes of the ASCII FBX named kRig in
/// orblit_filament/native/headless/orblit_import_check.cpp: a triangle, a
/// camera at 0, 2, 5 cm, a spot light of 200 W and a sun of 3 W/m2 turned a
/// quarter about Z. It is the converter's output, so the test reads what a
/// cooked FBX gives and the check in C++ reads how it was made.
final _cookedRig = base64Decode(
  'Z2xURgIAAADcBQAAcAUAAEpTT057ImFzc2V0Ijp7ImdlbmVyYXRvciI6Ik9yYmxpdCBpbXBv'
  'cnQiLCJ2ZXJzaW9uIjoiMi4wIn0sImV4dGVuc2lvbnNVc2VkIjpbIktIUl9saWdodHNfcHVu'
  'Y3R1YWwiXSwic2NlbmUiOjAsInNjZW5lcyI6W3sibm9kZXMiOlswLDEsMiwzXX1dLCJub2Rl'
  'cyI6W3sibmFtZSI6IlRyaSIsIm1lc2giOjB9LHsibmFtZSI6IkNhbSIsInRyYW5zbGF0aW9u'
  'IjpbMCwwLjAxOTk5OTk5OTYsMC4wNTAwMDAwMDA3XSwicm90YXRpb24iOlswLC0wLjcwNzEw'
  'Njc2OSwwLDAuNzA3MTA2NzY5XSwiY2FtZXJhIjowfSx7Im5hbWUiOiJMYW1wIiwicm90YXRp'
  'b24iOlstMC43MDcxMDY3NjksMCwwLDAuNzA3MTA2NzY5XSwiZXh0ZW5zaW9ucyI6eyJLSFJf'
  'bGlnaHRzX3B1bmN0dWFsIjp7ImxpZ2h0IjowfX19LHsibmFtZSI6IlN1biIsInJvdGF0aW9u'
  'IjpbLTAuNSwtMC41LDAuNSwwLjVdLCJleHRlbnNpb25zIjp7IktIUl9saWdodHNfcHVuY3R1'
  'YWwiOnsibGlnaHQiOjF9fX1dLCJtZXNoZXMiOlt7Im5hbWUiOiJUcmkiLCJwcmltaXRpdmVz'
  'IjpbeyJhdHRyaWJ1dGVzIjp7IlBPU0lUSU9OIjowLCJOT1JNQUwiOjF9LCJpbmRpY2VzIjoy'
  'fV19XSwiY2FtZXJhcyI6W3sidHlwZSI6InBlcnNwZWN0aXZlIiwicGVyc3BlY3RpdmUiOnsi'
  'eWZvdiI6MS4wNDcxOTc1OCwiem5lYXIiOjAuMDA0OTk5OTk5ODksInpmYXIiOjJ9fV0sImFj'
  'Y2Vzc29ycyI6W3siYnVmZmVyVmlldyI6MCwiY29tcG9uZW50VHlwZSI6NTEyNiwiY291bnQi'
  'OjMsInR5cGUiOiJWRUMzIiwibWluIjpbMCwwLDBdLCJtYXgiOlswLjAwOTk5OTk5OTc4LDAu'
  'MDA5OTk5OTk5NzgsMF19LHsiYnVmZmVyVmlldyI6MSwiY29tcG9uZW50VHlwZSI6NTEyNiwi'
  'Y291bnQiOjMsInR5cGUiOiJWRUMzIn0seyJidWZmZXJWaWV3IjoyLCJjb21wb25lbnRUeXBl'
  'Ijo1MTIzLCJjb3VudCI6MywidHlwZSI6IlNDQUxBUiJ9XSwiYnVmZmVyVmlld3MiOlt7ImJ1'
  'ZmZlciI6MCwiYnl0ZU9mZnNldCI6MCwiYnl0ZUxlbmd0aCI6MzYsInRhcmdldCI6MzQ5NjJ9'
  'LHsiYnVmZmVyIjowLCJieXRlT2Zmc2V0IjozNiwiYnl0ZUxlbmd0aCI6MzYsInRhcmdldCI6'
  'MzQ5NjJ9LHsiYnVmZmVyIjowLCJieXRlT2Zmc2V0Ijo3MiwiYnl0ZUxlbmd0aCI6NiwidGFy'
  'Z2V0IjozNDk2M31dLCJleHRlbnNpb25zIjp7IktIUl9saWdodHNfcHVuY3R1YWwiOnsibGln'
  'aHRzIjpbeyJ0eXBlIjoic3BvdCIsImNvbG9yIjpbMSwwLjUsMC4yNV0sImludGVuc2l0eSI6'
  'MTA4NzAuMjgyMiwic3BvdCI6eyJpbm5lckNvbmVBbmdsZSI6MC4yNjE3OTkzOTUsIm91dGVy'
  'Q29uZUFuZ2xlIjowLjUyMzU5ODc5fX0seyJ0eXBlIjoiZGlyZWN0aW9uYWwiLCJjb2xvciI6'
  'WzEsMSwxXSwiaW50ZW5zaXR5IjoyMDQ5fV19fSwiYnVmZmVycyI6W3siYnl0ZUxlbmd0aCI6'
  'Nzh9XX0gICBQAAAAQklOAAAAAAAAAAAAAAAAAArXIzwAAAAAAAAAAAAAAAAK1yM8AAAAAAAA'
  'AAAAAAAAAACAPwAAAAAAAAAAAACAPwAAAAAAAAAAAACAPwAAAQACAAAA',
);

void main() {
  group('a cooked FBX with a camera and lights', () {
    final read = readSceneFrom(_cookedRig, name: 'Rig');
    SceneEntity named(String name) =>
        read.document.entities.singleWhere((e) => e.name == name);

    test('arrives with an entity for each of its nodes', () {
      expect(read.document.entities.map((e) => e.name), [
        'Tri',
        'Cam',
        'Lamp',
        'Sun',
      ]);
    });

    test('the camera keeps its angle and planes, in metres', () {
      final camera =
          named('Cam').components[SceneComponents.camera]! as CameraComponent;
      expect(camera.fieldOfView, closeTo(60, 1e-4));
      expect(camera.near, closeTo(0.005, 1e-6));
      expect(camera.far, closeTo(2, 1e-6));
    });

    test('the lights come back at the watts the file stated', () {
      final spot =
          named('Lamp').components[SceneComponents.light]! as LightComponent;
      expect(spot.kind, LightType.spot);
      expect(spot.power, closeTo(200, 1e-3));
      expect(spot.spotSize, closeTo(60, 1e-3));
      expect(spot.spotBlend, closeTo(0.5, 1e-4));
      final sun =
          named('Sun').components[SceneComponents.light]! as LightComponent;
      expect(sun.kind, LightType.sun);
      expect(sun.power, closeTo(3, 1e-3));
    });
  });
}
