import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_layer.dart';

void main(List<String> args) => writeFixture(
  root: args[0],
  package: 'orblit_motion',
  name: 'layer',
  cases: [
    ...libraryCases(),
    ...restCases(),
    ...completeCases(),
    ...maskCases(),
    ...layerCases(),
    ...addCases(),
    ...weightCases(),
    ...advanceCases(),
    ...sampleCases(),
    ...placeCases(),
  ],
);
