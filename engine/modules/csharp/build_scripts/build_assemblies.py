#!/usr/bin/env python3

import os
import os.path
import shlex
import subprocess
from dataclasses import dataclass


def find_dotnet_cli():
    if os.name == "nt":
        for hint_dir in os.environ["PATH"].split(os.pathsep):
            hint_dir = hint_dir.strip('"')
            hint_path = os.path.join(hint_dir, "dotnet")
            if os.path.isfile(hint_path) and os.access(hint_path, os.X_OK):
                return hint_path
            if os.path.isfile(hint_path + ".exe") and os.access(hint_path + ".exe", os.X_OK):
                return hint_path + ".exe"
    else:
        for hint_dir in os.environ["PATH"].split(os.pathsep):
            hint_dir = hint_dir.strip('"')
            hint_path = os.path.join(hint_dir, "dotnet")
            if os.path.isfile(hint_path) and os.access(hint_path, os.X_OK):
                return hint_path


def find_msbuild_standalone_windows():
    msbuild_tools_path = find_msbuild_tools_path_reg()

    if msbuild_tools_path:
        return os.path.join(msbuild_tools_path, "MSBuild.exe")

    return None


def find_msbuild_tools_path_reg():
    import subprocess

    program_files = os.getenv("PROGRAMFILES(X86)")
    if not program_files:
        program_files = os.getenv("PROGRAMFILES")
    vswhere = os.path.join(program_files, "Microsoft Visual Studio", "Installer", "vswhere.exe")

    vswhere_args = ["-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild"]

    try:
        lines = subprocess.check_output([vswhere] + vswhere_args).splitlines()

        for line in lines:
            parts = line.decode("utf-8").split(":", 1)

            if len(parts) < 2 or parts[0] != "installationPath":
                continue

            val = parts[1].strip()

            if not val:
                raise ValueError("Value of `installationPath` entry is empty")

            # Since VS2019, the directory is simply named "Current"
            return os.path.join(val, "MSBuild", "Current", "Bin")

        raise ValueError("Cannot find `installationPath` entry")
    except ValueError as e:
        print("Error reading output from vswhere: " + str(e))
    except OSError:
        pass  # Fine, vswhere not found
    except (subprocess.CalledProcessError, OSError):
        pass


@dataclass
class ToolsLocation:
    dotnet_cli: str = ""
    msbuild_standalone: str = ""


def find_any_msbuild_tool():
    # Preference order: dotnet CLI > Standalone MSBuild

    # Find dotnet CLI
    dotnet_cli = find_dotnet_cli()
    if dotnet_cli:
        return ToolsLocation(dotnet_cli=dotnet_cli)

    # Find standalone MSBuild
    if os.name == "nt":
        msbuild_standalone = find_msbuild_standalone_windows()
        if msbuild_standalone:
            return ToolsLocation(msbuild_standalone=msbuild_standalone)

    return None


def run_msbuild(tools: ToolsLocation, sln: str, chdir_to: str, msbuild_args: list[str] | None = None):
    # Preference order: dotnet CLI > Standalone MSBuild
    if tools.dotnet_cli:
        args = [tools.dotnet_cli, "msbuild"]
    elif tools.msbuild_standalone:
        args = [tools.msbuild_standalone]
    else:
        raise RuntimeError("Path to MSBuild or dotnet CLI not provided.")

    args += [sln]

    if msbuild_args:
        args += msbuild_args

    print("Running MSBuild: ", " ".join(shlex.quote(arg) for arg in args), flush=True)

    msbuild_env = os.environ.copy()

    # Needed when running from Developer Command Prompt for VS
    if "PLATFORM" in msbuild_env:
        del msbuild_env["PLATFORM"]

    # We want to control cwd when running msbuild, because that's where the search for global.json begins.
    return subprocess.call(args, env=msbuild_env, cwd=chdir_to)


def build_api(msbuild_tool, module_dir, output_dir, push_nupkgs_local, precision, werror):
    target_filenames = [
        "OrblitSharp.dll",
        "OrblitSharp.pdb",
        "OrblitSharp.xml",
        "OrblitSharpEditor.dll",
        "OrblitSharpEditor.pdb",
        "OrblitSharpEditor.xml",
        "OrblitPlugins.dll",
        "OrblitPlugins.pdb",
        "OrblitPlugins.runtimeconfig.json",
    ]

    for build_config in ["Debug", "Release"]:
        editor_api_dir = os.path.join(output_dir, "OrblitSharp", "Api", build_config)

        targets = [os.path.join(editor_api_dir, filename) for filename in target_filenames]

        args = ["/restore", "/t:Build", "/p:Configuration=" + build_config, "/p:NoWarn=1591"]
        if push_nupkgs_local:
            args += ["/p:ClearNuGetLocalCache=true", "/p:PushNuGetToLocalSource=" + push_nupkgs_local]
        if precision == "double":
            args += ["/p:OrblitFloat64=true"]
        if werror:
            args += ["/p:TreatWarningsAsErrors=true"]

        sln = os.path.join(module_dir, "glue/OrblitSharp/OrblitSharp.sln")
        exit_code = run_msbuild(msbuild_tool, sln=sln, chdir_to=module_dir, msbuild_args=args)
        if exit_code != 0:
            return exit_code

        # Copy targets

        core_src_dir = os.path.abspath(os.path.join(sln, os.pardir, "OrblitSharp", "bin", build_config))
        editor_src_dir = os.path.abspath(os.path.join(sln, os.pardir, "OrblitSharpEditor", "bin", build_config))
        plugins_src_dir = os.path.abspath(os.path.join(sln, os.pardir, "OrblitPlugins", "bin", build_config, "net10.0"))

        if not os.path.isdir(editor_api_dir):
            assert not os.path.isfile(editor_api_dir)
            os.makedirs(editor_api_dir)

        def copy_target(target_path):
            from shutil import copy

            filename = os.path.basename(target_path)

            src_path = os.path.join(core_src_dir, filename)
            if not os.path.isfile(src_path):
                src_path = os.path.join(editor_src_dir, filename)
            if not os.path.isfile(src_path):
                src_path = os.path.join(plugins_src_dir, filename)

            print(f"Copying assembly to {target_path}...")
            copy(src_path, target_path)

        for scons_target in targets:
            copy_target(scons_target)

    return 0


def write_file_if_different(path, content):
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8") as f:
            existing_content = f.read()
        if existing_content == content:
            return
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)


def generate_sdk_package_versions():
    # I can't believe importing files in Python is so convoluted when not
    # following the golden standard for packages/modules.
    import os
    import sys
    from os.path import dirname

    # We want ../../../../methods.py.
    script_path = dirname(os.path.abspath(__file__))
    root_path = dirname(dirname(dirname(dirname(script_path))))

    sys.path.insert(0, root_path)
    from methods import get_version_info

    version_info = get_version_info("")
    sys.path.remove(root_path)

    version_status = version_info["status"]
    orblitsharp_version_str = "{major}.{minor}.{patch}".format(**version_info)
    orblit_dotnet_version_str = "{major}.{minor}.{patch}".format(**version_info)
    if version_status != "stable":
        # Pre-releases and development builds. A stable build keeps the plain version.

        # If version was overridden to be e.g. "beta3", we insert a dot between
        # "beta" and "3" to follow SemVer 2.0.
        import re

        match = re.search(r"[\d]+$", version_status)
        if match:
            pos = match.start()
            orblitsharp_version_status = version_status[:pos]
            orblit_dotnet_version_status = version_status[:pos]

            # "dev" pre-releases always use the "alpha" label in the Orblit .NET packages.
            if orblit_dotnet_version_status == "dev":
                orblit_dotnet_version_status = "alpha"

            orblitsharp_version_status += f".{version_status[pos:]}"
            orblit_dotnet_version_status += f".{version_status[pos:]}"
        else:
            # If the version status is not numbered, it must be a development build.
            # Development builds always use the "dev" label in the Orblit .NET packages.
            orblit_dotnet_version_status = "dev"
            orblitsharp_version_status = version_status

        orblitsharp_version_str += f"-{orblitsharp_version_status}"
        orblit_dotnet_version_str += f"-{orblit_dotnet_version_status}"

    import version

    version_defines = (
        [
            f"ORBLIT{version.major}",
            f"ORBLIT{version.major}_{version.minor}",
            f"ORBLIT{version.major}_{version.minor}_{version.patch}",
        ]
        + [f"ORBLIT{v}_OR_GREATER" for v in range(1, version.major + 1)]
        + [f"ORBLIT{version.major}_{v}_OR_GREATER" for v in range(0, version.minor + 1)]
        + [f"ORBLIT{version.major}_{version.minor}_{v}_OR_GREATER" for v in range(0, version.patch + 1)]
    )

    props = f"""<Project>
  <PropertyGroup>
    <PackageVersion_OrblitSharp>{orblitsharp_version_str}</PackageVersion_OrblitSharp>
    <PackageVersion_Orblit_NET_Sdk>{orblitsharp_version_str}</PackageVersion_Orblit_NET_Sdk>
    <PackageVersion_Orblit_SourceGenerators>{orblitsharp_version_str}</PackageVersion_Orblit_SourceGenerators>
    <PackageVersion_OrblitDotNet>{orblit_dotnet_version_str}</PackageVersion_OrblitDotNet>
    <_OrblitVersionConstants>{";".join(version_defines)}</_OrblitVersionConstants>
  </PropertyGroup>
</Project>
"""

    # We write in ../SdkPackageVersions.props.
    write_file_if_different(os.path.join(dirname(script_path), "SdkPackageVersions.props"), props)

    # Also write the versioned docs URL to a constant for the Source Generators.

    constants = """namespace Orblit.SourceGenerators
{{
// TODO: This is currently disabled because of https://github.com/dotnet/roslyn/issues/52904
#pragma warning disable IDE0040 // Add accessibility modifiers.
    partial class Common
    {{
        public const string VersionDocsUrl = "{website}/docs";
    }}
}}
""".format(**version_info)

    generators_dir = os.path.join(
        dirname(script_path),
        "editor",
        "Orblit.NET.Sdk",
        "Orblit.SourceGenerators",
        "Generated",
    )
    os.makedirs(generators_dir, exist_ok=True)

    write_file_if_different(os.path.join(generators_dir, "Common.Constants.cs"), constants)


def build_all(msbuild_tool, module_dir, output_dir, orblit_platform, dev_debug, push_nupkgs_local, precision, werror):
    # Generate SdkPackageVersions.props and VersionDocsUrl constant
    generate_sdk_package_versions()

    # Orblit API
    exit_code = build_api(msbuild_tool, module_dir, output_dir, push_nupkgs_local, precision, werror)
    if exit_code != 0:
        return exit_code

    # OrblitTools
    sln = os.path.join(module_dir, "editor/OrblitTools/OrblitTools.sln")
    args = ["/restore", "/t:Build", "/p:Configuration=" + ("Debug" if dev_debug else "Release")] + (
        ["/p:OrblitPlatform=" + orblit_platform] if orblit_platform else []
    )
    if push_nupkgs_local:
        args += ["/p:ClearNuGetLocalCache=true", "/p:PushNuGetToLocalSource=" + push_nupkgs_local]
    if precision == "double":
        args += ["/p:OrblitFloat64=true"]
    exit_code = run_msbuild(msbuild_tool, sln=sln, chdir_to=module_dir, msbuild_args=args)
    if exit_code != 0:
        return exit_code

    # Orblit.NET.Sdk
    args = ["/restore", "/t:Build", "/p:Configuration=Release"]
    if push_nupkgs_local:
        args += ["/p:ClearNuGetLocalCache=true", "/p:PushNuGetToLocalSource=" + push_nupkgs_local]
    if precision == "double":
        args += ["/p:OrblitFloat64=true"]
    sln = os.path.join(module_dir, "editor/Orblit.NET.Sdk/Orblit.NET.Sdk.sln")
    exit_code = run_msbuild(msbuild_tool, sln=sln, chdir_to=module_dir, msbuild_args=args)
    if exit_code != 0:
        return exit_code

    return 0


def main():
    import argparse
    import sys

    parser = argparse.ArgumentParser(description="Builds all Orblit .NET solutions")
    parser.add_argument("--orblit-output-dir", type=str, required=True)
    parser.add_argument(
        "--dev-debug",
        action="store_true",
        default=False,
        help="Build OrblitTools and Orblit.NET.Sdk with 'Configuration=Debug'",
    )
    parser.add_argument("--orblit-platform", type=str, default="")
    parser.add_argument("--push-nupkgs-local", type=str, default="")
    parser.add_argument(
        "--precision", type=str, default="single", choices=["single", "double"], help="Floating-point precision level"
    )
    parser.add_argument("--werror", action="store_true", default=False, help="Treat compiler warnings as errors.")

    args = parser.parse_args()

    this_script_dir = os.path.dirname(os.path.realpath(__file__))
    module_dir = os.path.abspath(os.path.join(this_script_dir, os.pardir))

    output_dir = os.path.abspath(args.orblit_output_dir)

    push_nupkgs_local = os.path.abspath(args.push_nupkgs_local) if args.push_nupkgs_local else None

    msbuild_tool = find_any_msbuild_tool()

    if msbuild_tool is None:
        print("Unable to find MSBuild")
        sys.exit(1)

    exit_code = build_all(
        msbuild_tool,
        module_dir,
        output_dir,
        args.orblit_platform,
        args.dev_debug,
        push_nupkgs_local,
        args.precision,
        args.werror,
    )
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
