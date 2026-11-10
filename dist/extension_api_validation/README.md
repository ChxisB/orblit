This folder contains files with the expected output of `--validate-extension-api` when run against the `extension_api.json` of a release.

The folder has no entries yet. Orblit 1.0.0 has no earlier release to break.

`dist/scripts/validate_extension_api.sh` downloads `extension_api.json` from the GitHub release named by the folder, at
`https://github.com/ChxisB/orblit/releases/download/{TAG}/extension_api.json`. Each release must attach that file.

Only lines that start with "Validate extension JSON:" matter, everything else is considered a comment and ignored. They
should instead be used to justify these changes and describe how users should work around these changes.

Add new entries to the folder named after the latest release tag, for example `v1.0.0`. When the next release ships,
rename that folder to `v1.0.0_v1.1.0` and add a new `v1.1.0` folder.
