# Changelog

## 0.1.0

- A port of `orblit_collide` 0.1.0.
- `Box.box` is gone. A member cannot share its class's name in C#, and
  `Bounds` returned the same value. `Rectangle.Rect` stays.
- `World2` pairs two ids as `a * 1000003 + b`, as the Dart does. An id below
  0 or above 1000002 can pair with the wrong body, and `Ended` unpairs a
  negative key wrongly. Keep ids in 0 to 1000002.
- `RaycastAll` is a stable sort. Dart's `List.sort` is stable up to 32 items
  and not above that, so two hits at the same distance can order differently
  in a list of more than 32.
