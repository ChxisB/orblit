import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_cutscene.dart';

void main(List<String> args) => writeFixture(
  root: args[0],
  package: 'orblit_motion',
  name: 'cutscene',
  cases: [
    ...decodeCases(),
    ...frameCases(),
    ...markCases(),
    ...playCases(),
    ...spanCases(),
  ],
);
