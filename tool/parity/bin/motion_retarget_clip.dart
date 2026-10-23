import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_retarget_clip.dart';

// The library case goes last: it holds the clips the other cases made.
void main(List<String> args) => writeFixture(
  root: args[0],
  package: 'orblit_motion',
  name: 'retarget_clip',
  cases: [
    ...retargetCases(),
    ...boneMapDecodeCases(),
    ...boneMapEditCases(),
    ...boneMapFitCases(),
    ...boneMapPairingCases(),
    ...boneMapApplyCases(),
    ...clipLibraryCases(),
  ],
);
