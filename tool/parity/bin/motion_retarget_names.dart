import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_retarget_names.dart';

void main(List<String> args) => writeFixture(
  root: args[0],
  package: 'orblit_motion',
  name: 'retarget_names',
  cases: [
    ...libraryCases(),
    ...cleanCases(),
    ...matchCases(),
    ...humanoidCases(),
    ...matchHumanoidCases(),
    ...reachCases(),
  ],
);
