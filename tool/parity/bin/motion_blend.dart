import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_blend.dart';

void main(List<String> args) {
  writeFixture(
    root: args[0],
    package: 'orblit_motion',
    name: 'blend',
    cases: [
      ...libraryCases(),
      ...decodeBlendCases(),
      ...placeCases(),
      ...numbersCases(),
      ...weightsCases(),
      ...changeCases(),
      ...sampleBlendCases(),
      ...restedCases(),
      ...lengthCases(),
      ...routeCases(),
      ...playerCases(),
    ],
  );
}
