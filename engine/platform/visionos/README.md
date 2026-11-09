# visionOS platform port

This folder contains the C++, Objective-C and Objective-C++ code for the visionOS
platform port.

This platform derives from the Apple Embedded abstract platform ([`backends/apple_embedded`](/backends/apple_embedded)).

This platform uses shared Apple code ([`backends/apple`](/backends/apple)).

See also [`dist/apple_embedded_xcode`](/dist/apple_embedded_xcode) folder for the Xcode
project template used for packaging the iOS export templates.

## Documentation

The compiling and exporting process is the same as on iOS, but replacing the `ios` parameter by `visionos`.
