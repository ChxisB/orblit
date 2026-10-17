import 'package:orblit_input/orblit_input.dart';

Duration ms(int milliseconds) => Duration(milliseconds: milliseconds);

/// One frame of the devices, with every part optional.
FrameControls frame({
  Set<String> keys = const {},
  Set<String> touch = const {},
  Set<PadButton> down = const {},
  Map<PadAxis, double> axes = const {},
}) => FrameControls(
  keys: keys,
  touch: touch,
  pad: PadState(
    slot: 0,
    identity: const PadIdentity(name: 'test'),
    down: down,
    pressed: const {},
    released: const {},
    axes: axes,
    raw: axes,
  ),
);

const actions = [
  InputAction.button('jump'),
  InputAction.button('fire'),
  InputAction.button('confirm'),
  InputAction.axis('steer'),
  InputAction.axis2('move'),
];

/// Gameplay on the keyboard and a pad, a vehicle that rebinds the jump key
/// and leaves the rest, and a menu that blocks.
List<InputContext> contexts({String jumpKey = 'Space'}) => [
  InputContext(
    'play',
    bindings: {
      'jump': [
        Binding(KeyControl(jumpKey)),
        const Binding(PadButtonControl(PadButton.south)),
      ],
      'fire': const [Binding(KeyControl('KeyF'))],
      'steer': const [
        Binding(KeyControl('KeyA'), scale: -1),
        Binding(KeyControl('KeyD')),
      ],
      'move': const [
        Binding(KeyControl('KeyA'), scale: -1),
        Binding(KeyControl('KeyD')),
        Binding(KeyControl('KeyS'), scale: -1, part: AxisPart.y),
        Binding(KeyControl('KeyW'), part: AxisPart.y),
        Binding(PadStickControl(PadStick.left), shaping: Shaping.none),
      ],
    },
  ),
  InputContext(
    'vehicle',
    bindings: const {
      'jump': [Binding(KeyControl('KeyE'))],
    },
  ),
  InputContext(
    'menu',
    blocks: true,
    bindings: const {
      'confirm': [Binding(KeyControl('Enter')), Binding(KeyControl('Space'))],
    },
  ),
];

ActionMap actionMap({
  List<String> active = const ['play'],
  String jumpKey = 'Space',
}) => ActionMap(
  actions: actions,
  contexts: contexts(jumpKey: jumpKey),
  active: active,
);
