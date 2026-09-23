# Changelog

## 0.2.1

- **A rotation turns at an even rate between two keys.** `QuaternionMixer`
  blended the four numbers and made a rotation again, which lags a quarter of
  the way through a wide turn and runs ahead at three quarters. It now goes
  round the arc at a constant speed, as glTF specifies and as the renderer
  plays a file's own clips, so a clip poses a model the same either way. A
  shape that overshoots carries on round the same arc. Only turns more than a
  few degrees apart change. Keys a frame apart were already within a fraction
  of a degree.

## 0.2.0

- **Curves.** `Hold.curve` carries a value along a cubic that leaves a key,
  and arrives at the next, at the slopes they name in `Key.slopeIn` and
  `Key.slopeOut`. A key that names none takes the line from its neighbours,
  so a curve flows through the keys in the middle, and a peak, a trough and
  the two ends are left flat so nothing overshoots a value somebody set.
  Rotations follow glTF's cubic spline: four numbers along four curves, then
  made a rotation again, the short way round. `Channel.slopeAt` is the slope
  a key really has, for a curve editor to draw its handle.
- `CurveMixer` is what a value needs to curve; the number, vector and
  rotation mixers are ones. A flag is not, and travels as a line does.

## 0.1.0

- First cut of `orblit_sequence`. Pre-alpha: everything is subject to change.
