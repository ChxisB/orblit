part of 'runner.dart';

// What is drawn over the game: the score, the cards that stand in
// front of it, and the numbers that pop up as coins are taken.

const _figures = [FontFeature.tabularFigures()];

extension _Hud on RunnerExample {
  Widget _hud() {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xD91A1D26),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 7),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              '$score',
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w800,
                fontFeatures: _figures,
              ),
            ),
            const SizedBox(width: 16),
            const _CoinIcon(),
            const SizedBox(width: 6),
            Text(
              '$_coins',
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: Color(0xFFFFD34D),
                fontFeatures: _figures,
              ),
            ),
            const SizedBox(width: 16),
            Text(
              '${math.max(_speed, 0).round()} m/s',
              style: const TextStyle(
                fontSize: 14,
                color: Color(0xFFB4BCCB),
                fontFeatures: _figures,
              ),
            ),
            if (autopilot) ...[
              const SizedBox(width: 12),
              const Text(
                'AUTO',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF7ED9B7),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  /// The "+N" for the coins just picked up, over the runner's head.
  Widget? _popupAt(Size size) {
    final since = _clock - _lastCoinAt;
    if (_popup == 0 || since > 1.1 || _phase == RunnerPhase.ready) return null;
    if (size.width <= 0 || size.height <= 0 || !size.isFinite) return null;

    final view = makeViewMatrix(_eye, _look, Vector3(0, 1, 0));
    final projection = makePerspectiveMatrix(
      _fieldOfView * math.pi / 180,
      size.width / size.height,
      0.1,
      1000,
    );
    final clip = projection
        .multiplied(view)
        .transform(Vector4(_x, _y + 1.75 + since * 0.4, _z(_along), 1));
    if (clip.w <= 0) return null;
    final sx = (clip.x / clip.w + 1) / 2 * size.width;
    final sy = (1 - clip.y / clip.w) / 2 * size.height;
    final fade = since < 0.8 ? 1.0 : 1 - (since - 0.8) / 0.3;

    return Positioned(
      left: sx - 60,
      top: sy - 20,
      width: 120,
      child: Opacity(
        opacity: fade.clamp(0.0, 1.0),
        child: Text(
          '+$_popup',
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 26,
            fontWeight: FontWeight.w900,
            color: Color(0xFFFFD34D),
            shadows: [Shadow(color: Color(0xCC3A2500), blurRadius: 5)],
          ),
        ),
      ),
    );
  }

  Widget _card(List<Widget> children) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xE61A1D26),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 16),
        child: Column(mainAxisSize: MainAxisSize.min, children: children),
      ),
    );
  }

  Widget _readyCard() {
    Widget line(String keys, String does) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 1.5),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 118,
            child: Text(
              keys,
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: Color(0xFFFFD34D),
                fontWeight: FontWeight.w700,
                fontSize: 13,
              ),
            ),
          ),
          const SizedBox(width: 10),
          SizedBox(
            width: 110,
            child: Text(
              does,
              style: const TextStyle(color: Color(0xFFD5DAE3), fontSize: 13),
            ),
          ),
        ],
      ),
    );

    return _card([
      const Text(
        'Tap or press Space to run',
        style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 10),
      line('← →  or  A D', 'change lane'),
      line('↑  W  or  Space', 'jump'),
      line('↓  or  S', 'slide'),
      line('Esc', 'pause'),
      const SizedBox(height: 6),
      const Text(
        'On a touch screen, swipe.',
        style: TextStyle(color: Color(0xFF8E97A8), fontSize: 12),
      ),
      if (_best > 0) ...[
        const SizedBox(height: 6),
        Text(
          'Best $_best',
          style: const TextStyle(color: Color(0xFF8E97A8), fontSize: 12),
        ),
      ],
    ]);
  }

  Widget _overCard() {
    Widget figure(String label, String value, {Color? colour}) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 76,
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFFB4BCCB)),
            ),
          ),
          SizedBox(
            width: 76,
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w800,
                color: colour,
                fontFeatures: _figures,
              ),
            ),
          ),
        ],
      ),
    );

    return _card([
      const Text(
        'Crashed!',
        style: TextStyle(fontSize: 26, fontWeight: FontWeight.w900),
      ),
      if (_newBest)
        const Text(
          'A new best',
          style: TextStyle(
            color: Color(0xFFFFD34D),
            fontWeight: FontWeight.w700,
          ),
        ),
      const SizedBox(height: 10),
      figure('Score', '$score'),
      figure('Coins', '$_coins', colour: const Color(0xFFFFD34D)),
      figure('Distance', '${_ran.floor()} m'),
      figure('Best', '$_best'),
      const SizedBox(height: 12),
      const Text(
        'Tap or press Space to run again',
        style: TextStyle(color: Color(0xFFD5DAE3)),
      ),
    ]);
  }

  /// Tap, Space or Enter: whatever "go" means just now.
  void _confirm() {
    switch (_phase) {
      case RunnerPhase.ready:
        start();
      case RunnerPhase.paused:
        resume();
      case RunnerPhase.over:
        // Not in the moment of the crash, when a tap was meant for a jump.
        if (_clock - _crashedAt > 0.7) restart();
      case RunnerPhase.running:
        break;
    }
  }
}

/// A coin, drawn: a gold disc with a paler rim.
class _CoinIcon extends StatelessWidget {
  const _CoinIcon();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 18,
      height: 18,
      child: DecoratedBox(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            center: Alignment(-0.3, -0.35),
            colors: [Color(0xFFFFF0A0), Color(0xFFFFC21A), Color(0xFFC98A00)],
            stops: [0, 0.55, 1],
          ),
          border: Border.fromBorderSide(
            BorderSide(color: Color(0xFFFFE27A), width: 1.2),
          ),
        ),
      ),
    );
  }
}
