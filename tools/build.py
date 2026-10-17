#!/usr/bin/env python3
"""Build the mod and check its patches against the installed game.

    ./build.sh                build the plugin (Release), then run tools/PatchCheck
    ./build.sh --debug        build the Debug configuration instead
    ./build.sh --deploy       also copy the plugin into the game's BepInEx/plugins/ValheimFOPReworked/
    ./build.sh --no-check     skip the patch check
    ./build.sh --package      also make dist/<name>-<version>.zip, the Thunderstore package mod managers install

./setup.sh has to have run once (it finds the game and downloads BepInEx). --deploy needs BepInEx in the game too:
./setup.sh --install-bepinex. On Windows, build.bat and setup.bat take the same options. Python 3, standard library
only; the build itself is dotnet build.
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

# How the scripts are run on this system, for the messages
SETUP = "setup.bat" if os.name == "nt" else "./setup.sh"
BUILD = "build.bat" if os.name == "nt" else "./build.sh"

ROOT = Path(__file__).resolve().parent.parent
SOLUTION = ROOT / "ValheimFOPReworked.slnx"
ENV_PROPS = ROOT / "Environment.props"
BEPINEX_CORE = ROOT / "deps" / "BepInExPack_Valheim" / "BepInEx" / "core"
PATCH_CHECK = ROOT / "tools" / "PatchCheck"
PLUGIN_SOURCE = ROOT / "src" / "ValheimFOPReworked" / "Plugin.cs"
README = ROOT / "README.md"
PACKAGE_FILES = ROOT / "package"
DIST = ROOT / "dist"

# The Thunderstore package. Its version is Plugin.Version; the team (namespace) is chosen when uploading.
PACKAGE_NAME = "ForgeOfPotentialReworked"  # letters, digits and underscores only
PACKAGE_WEBSITE = "https://github.com/Melichor/ValheimFOPReworked"
PACKAGE_DESCRIPTION = ("Items gain experience as you use them; a full bar lets you refine them at the Forge of "
                       "Potential, with idols. No gamble and nothing breaks, up to level 25. Idols can be crafted.")


def die(msg):
    sys.exit(f"error: {msg}")


def game_dir():
    """The Valheim install that setup.sh found."""
    if not ENV_PROPS.is_file():
        die(f"Environment.props is missing, run {SETUP} first")
    node = ET.parse(ENV_PROPS).getroot().find("./PropertyGroup/ValheimDir")
    if node is None or not node.text:
        die(f"Environment.props has no ValheimDir, run {SETUP} again")
    return Path(node.text)


def run(cmd):
    print("==> " + " ".join(str(c) for c in cmd), flush=True)
    if subprocess.run([str(c) for c in cmd], cwd=ROOT).returncode != 0:
        sys.exit(1)


def plugin_version():
    """Plugin.Version in Plugin.cs, the one version of the mod."""
    match = re.search(r'public const string Version = "([^"]+)"', PLUGIN_SOURCE.read_text(encoding="utf-8"))
    if not match:
        die(f"no Version constant in {PLUGIN_SOURCE.relative_to(ROOT)}")
    if not re.fullmatch(r"\d+\.\d+\.\d+", match.group(1)):
        die(f"Plugin.Version is {match.group(1)}; Thunderstore needs three numbers, like 1.2.3")
    return match.group(1)


def bepinex_dependency():
    """The BepInExPack_Valheim version setup.py pins, as a Thunderstore dependency."""
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(ROOT / "tools"))
    import setup
    return f"denikson-BepInExPack_Valheim-{setup.BEPINEX_VERSION}"


def package_readme():
    """The players' part of README.md: from the top to the start of the build instructions (## Requirements)."""
    text = README.read_text(encoding="utf-8")
    end = text.find("\n## Requirements")
    if end < 0:
        die("README.md has no '## Requirements' section to end the package README at")
    return text[:end].rstrip() + f"\n\nSource code and build instructions: {PACKAGE_WEBSITE}\n"


def package(plugin):
    """dist/<name>-<version>.zip in Thunderstore's layout: manifest, README, icon, and the plugin in plugins/."""
    icon = PACKAGE_FILES / "icon.png"
    if not icon.is_file():
        die(f"{icon.relative_to(ROOT)} is missing; Thunderstore needs a 256x256 PNG icon (run package/export-icon.sh)")
    version = plugin_version()
    manifest = {
        "name": PACKAGE_NAME,
        "version_number": version,
        "website_url": PACKAGE_WEBSITE,
        "description": PACKAGE_DESCRIPTION,
        "dependencies": [bepinex_dependency()],
    }
    if len(PACKAGE_DESCRIPTION) > 250:
        die(f"the package description has {len(PACKAGE_DESCRIPTION)} characters, Thunderstore allows 250")
    DIST.mkdir(exist_ok=True)
    path = DIST / f"{PACKAGE_NAME}-{version}.zip"
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("manifest.json", json.dumps(manifest, indent=2) + "\n")
        z.writestr("README.md", package_readme())
        z.write(icon, "icon.png")
        changelog = PACKAGE_FILES / "CHANGELOG.md"
        if changelog.is_file():
            z.write(changelog, "CHANGELOG.md")
        z.write(plugin, f"plugins/{plugin.name}")
    return path


def main():
    ap = argparse.ArgumentParser(prog=BUILD.lstrip("./"), description=__doc__.split("\n\n")[0])
    ap.add_argument("--debug", action="store_true", help="build the Debug configuration instead of Release")
    ap.add_argument("--deploy", action="store_true",
                    help="also copy the plugin into the game's BepInEx/plugins/ValheimFOPReworked/")
    ap.add_argument("--no-check", action="store_true", help="skip the patch check (tools/PatchCheck)")
    ap.add_argument("--package", action="store_true",
                    help="also make dist/<name>-<version>.zip, the Thunderstore package mod managers install")
    args = ap.parse_args()
    if args.package and args.debug:
        die("--package packs the Release build, leave out --debug")

    if not shutil.which("dotnet"):
        die("dotnet (the .NET 10 SDK) is needed to build")
    game = game_dir()
    if not (BEPINEX_CORE / "BepInEx.dll").is_file():
        die(f"BepInEx is missing from deps/, run {SETUP} first")
    if args.deploy and not (game / "BepInEx" / "core" / "BepInEx.dll").is_file():
        die(f"BepInEx is not installed in {game}, run {SETUP} --install-bepinex first")

    configuration = "Debug" if args.debug else "Release"
    build = ["dotnet", "build", SOLUTION, "-c", configuration, "-nologo"]
    if args.deploy:
        build.append("-p:Deploy=true")
    run(build)

    plugin = ROOT / "src" / "ValheimFOPReworked" / "bin" / configuration / "netstandard2.1" / "ValheimFOPReworked.dll"
    if not args.no_check:
        # The solution build above built the checker too
        run(["dotnet", "run", "--project", PATCH_CHECK, "-c", configuration, "--no-build", "--", game, plugin])

    print()
    print(f"Built {plugin.relative_to(ROOT)}")
    if args.deploy:
        print(f"Deployed to {game / 'BepInEx' / 'plugins' / 'ValheimFOPReworked'}")
    if args.package:
        print(f"Packaged {package(plugin).relative_to(ROOT)}")


if __name__ == "__main__":
    main()
