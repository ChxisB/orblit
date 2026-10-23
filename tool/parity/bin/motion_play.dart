import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_play.dart';

void main(List<String> args) {
  writeFixture(
    root: args[0],
    package: 'orblit_motion',
    name: 'play',
    cases: [
      ...playCases(),
      ...passCases(),
      ...syncCases(),
      ...reduceCases(),
      ...restCases(),
      ...sceneCases(),
    ],
  );
}
