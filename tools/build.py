#!/usr/bin/env python3
"""Build the mod and check its patches against the installed game.

    ./build.sh                build the plugin (Release), then run tools/PatchCheck
    ./build.sh --debug        build the Debug configuration instead
    ./build.sh --deploy       also copy the plugin into the game's BepInEx/plugins/ValheimFOPReworked/
    ./build.sh --no-check     skip the patch check

./setup.sh has to have run once (it finds the game and downloads BepInEx). --deploy needs BepInEx in the game too:
./setup.sh --install-bepinex. On Windows, build.bat and setup.bat take the same options. Python 3, standard library
only; the build itself is dotnet build.
"""
import argparse
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# How the scripts are run on this system, for the messages
SETUP = "setup.bat" if os.name == "nt" else "./setup.sh"
BUILD = "build.bat" if os.name == "nt" else "./build.sh"

ROOT = Path(__file__).resolve().parent.parent
SOLUTION = ROOT / "ValheimFOPReworked.slnx"
ENV_PROPS = ROOT / "Environment.props"
BEPINEX_CORE = ROOT / "deps" / "BepInExPack_Valheim" / "BepInEx" / "core"
PATCH_CHECK = ROOT / "tools" / "PatchCheck"


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


def main():
    ap = argparse.ArgumentParser(prog=BUILD.lstrip("./"), description=__doc__.split("\n\n")[0])
    ap.add_argument("--debug", action="store_true", help="build the Debug configuration instead of Release")
    ap.add_argument("--deploy", action="store_true",
                    help="also copy the plugin into the game's BepInEx/plugins/ValheimFOPReworked/")
    ap.add_argument("--no-check", action="store_true", help="skip the patch check (tools/PatchCheck)")
    args = ap.parse_args()

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


if __name__ == "__main__":
    main()
