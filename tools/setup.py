#!/usr/bin/env python3
"""Download and prepare everything needed to build and work on the mod.

Everything this script downloads or generates goes into deps/, and the path of the local Valheim install goes into
Environment.props. Both are git-ignored. Running it again only redoes what is missing or out of date: a different
pinned version, or a game update for the decompiled code.

    ./setup.sh                      find Valheim through Steam, download BepInEx and ilspycmd, decompile the game code
    ./setup.sh --game <path>        use this Valheim install (the folder that contains valheim_Data)
    ./setup.sh --no-decompile       skip ilspycmd and the decompiled game code
    ./setup.sh --install-bepinex    also copy BepInEx into the game folder, which is needed to run the mod

The game path can also be given in the VALHEIM_INSTALL environment variable. Python 3, standard library only.
ilspycmd needs the .NET 10 SDK (the same one that builds the mod).
"""
import argparse
import hashlib
import os
import re
import shutil
import stat
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from xml.sax.saxutils import escape

# Pinned versions. To update BepInEx, change both lines: the hash is the sha256 of the zip Thunderstore serves.
BEPINEX_VERSION = "5.4.2351"
BEPINEX_SHA256 = "bce631497976a93977ceb08e166712e6c31d15244956f89f17df092a9b62e29f"
BEPINEX_URL = "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/{version}/"
ILSPYCMD_VERSION = "11.1.0.9782"
# Game assemblies that are decompiled into deps/decompiled/ to read the game's code
DECOMPILE = ["assembly_valheim", "assembly_utils", "assembly_guiutils"]
VALHEIM_APP_ID = 892970
# Where Steam keeps libraryfolders.vdf: native Linux, the Debian/Ubuntu package, Flatpak, Windows
STEAM_ROOTS = [
    Path.home() / ".steam" / "steam",
    Path.home() / ".local" / "share" / "Steam",
    Path.home() / ".var" / "app" / "com.valvesoftware.Steam" / ".local" / "share" / "Steam",
    Path("C:/Program Files (x86)/Steam"),
]

ROOT = Path(__file__).resolve().parent.parent
DEPS = ROOT / "deps"
ENV_PROPS = ROOT / "Environment.props"
BEPINEX_DIR = DEPS / "BepInExPack_Valheim"
BEPINEX_STAMP = DEPS / "BepInExPack_Valheim.version"
TOOLS_DIR = DEPS / "tools"
ILSPYCMD = TOOLS_DIR / ("ilspycmd.exe" if os.name == "nt" else "ilspycmd")


def log(msg):
    print(f"==> {msg}", flush=True)


def die(msg):
    sys.exit(f"error: {msg}")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def managed_dir(game):
    return game / "valheim_Data" / "Managed"


def is_game_dir(path):
    return (managed_dir(path) / "assembly_valheim.dll").is_file()


# ---------------------------------------------------------------------------------------------------------------
# The game install
# ---------------------------------------------------------------------------------------------------------------

def find_in_steam():
    """The Valheim folder in the first Steam library that has the game installed, or None."""
    for root in STEAM_ROOTS:
        vdf = root / "steamapps" / "libraryfolders.vdf"
        if not vdf.is_file():
            continue
        libraries = [root] + [Path(p.replace("\\\\", "\\"))
                              for p in re.findall(r'"path"\s+"([^"]*)"', vdf.read_text(errors="replace"))]
        for lib in libraries:
            manifest = lib / "steamapps" / f"appmanifest_{VALHEIM_APP_ID}.acf"
            if not manifest.is_file():
                continue
            m = re.search(r'"installdir"\s+"([^"]*)"', manifest.read_text(errors="replace"))
            game = lib / "steamapps" / "common" / (m.group(1) if m else "Valheim")
            if is_game_dir(game):
                return game
    return None


def read_env_props():
    """The game path saved by an earlier run, or None."""
    if not ENV_PROPS.is_file():
        return None
    try:
        node = ET.parse(ENV_PROPS).getroot().find("./PropertyGroup/ValheimDir")
    except ET.ParseError:
        return None
    return Path(node.text) if node is not None and node.text else None


def write_env_props(game):
    text = ("<Project>\n"
            "  <!-- Written by tools/setup.py: this machine's Valheim install. Not committed. -->\n"
            "  <PropertyGroup>\n"
            f"    <ValheimDir>{escape(str(game))}</ValheimDir>\n"
            "  </PropertyGroup>\n"
            "</Project>\n")
    # Only rewrite when it changes, so MSBuild does not see a newer file and rebuild for nothing
    if not ENV_PROPS.is_file() or ENV_PROPS.read_text() != text:
        ENV_PROPS.write_text(text)


def resolve_game(given):
    if given:
        game = Path(given).expanduser().resolve()
        if not is_game_dir(game):
            die(f"{game} is not a Valheim install (valheim_Data/Managed/assembly_valheim.dll is missing)")
        return game
    saved = read_env_props()
    if saved:
        if is_game_dir(saved):
            return saved
        log(f"The Valheim path in Environment.props ({saved}) is not a Valheim install any more, looking again")
    game = find_in_steam()
    if not game:
        die("Valheim was not found in any Steam library. "
            "Pass --game <path to the Valheim folder> or set VALHEIM_INSTALL.")
    return game


# ---------------------------------------------------------------------------------------------------------------
# BepInEx
# ---------------------------------------------------------------------------------------------------------------

def download_bepinex():
    if (BEPINEX_STAMP.is_file() and BEPINEX_STAMP.read_text().strip() == BEPINEX_VERSION
            and (BEPINEX_DIR / "BepInEx" / "core" / "BepInEx.dll").is_file()):
        log(f"BepInExPack_Valheim {BEPINEX_VERSION}: already downloaded")
        return
    downloads = DEPS / "downloads"
    downloads.mkdir(parents=True, exist_ok=True)
    zip_path = downloads / f"BepInExPack_Valheim-{BEPINEX_VERSION}.zip"
    if not zip_path.is_file() or sha256(zip_path) != BEPINEX_SHA256:
        log(f"Downloading BepInExPack_Valheim {BEPINEX_VERSION}")
        part = zip_path.with_name(zip_path.name + ".part")
        request = urllib.request.Request(BEPINEX_URL.format(version=BEPINEX_VERSION),
                                         headers={"User-Agent": "ValheimFOPReworked-setup"})
        with urllib.request.urlopen(request) as response, open(part, "wb") as f:
            shutil.copyfileobj(response, f)
        actual = sha256(part)
        if actual != BEPINEX_SHA256:
            part.unlink()
            die(f"the BepInExPack_Valheim {BEPINEX_VERSION} download has sha256 {actual}, "
                f"expected {BEPINEX_SHA256}")
        part.replace(zip_path)
    log(f"Unpacking BepInExPack_Valheim {BEPINEX_VERSION} into deps/BepInExPack_Valheim")
    tmp = DEPS / "BepInExPack_Valheim.tmp"
    shutil.rmtree(tmp, ignore_errors=True)
    shutil.rmtree(BEPINEX_DIR, ignore_errors=True)
    with zipfile.ZipFile(zip_path) as z:
        z.extractall(tmp)
    # The zip holds the Thunderstore manifest next to BepInExPack_Valheim/, which is what goes into the game folder
    (tmp / "BepInExPack_Valheim").replace(BEPINEX_DIR)
    shutil.rmtree(tmp)
    BEPINEX_STAMP.write_text(BEPINEX_VERSION + "\n")


def install_bepinex(game):
    """Copy the pack into the game folder, keeping the player's own BepInEx.cfg if there is one."""
    log(f"Installing BepInEx {BEPINEX_VERSION} into {game}")
    keep = Path("BepInEx") / "config" / "BepInEx.cfg"
    for src in sorted(BEPINEX_DIR.rglob("*")):
        rel = src.relative_to(BEPINEX_DIR)
        dst = game / rel
        if src.is_dir() or (rel == keep and dst.exists()):
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
    for script in ("start_game_bepinex.sh", "start_server_bepinex.sh"):
        path = game / script
        path.chmod(path.stat().st_mode | stat.S_IXUSR)  # zip files do not keep the executable bit
    (game / "BepInEx" / "plugins").mkdir(exist_ok=True)


# ---------------------------------------------------------------------------------------------------------------
# The decompiled game code
# ---------------------------------------------------------------------------------------------------------------

def ilspycmd_version():
    if not ILSPYCMD.is_file():
        return None
    try:
        out = subprocess.run([str(ILSPYCMD), "--version"], capture_output=True, text=True).stdout
    except OSError:
        return None
    m = re.search(r"^ilspycmd: (\S+)", out, re.M)
    return m.group(1) if m else None


def install_ilspycmd():
    if ilspycmd_version() == ILSPYCMD_VERSION:
        log(f"ilspycmd {ILSPYCMD_VERSION}: already installed")
        return
    if not shutil.which("dotnet"):
        die("ilspycmd needs dotnet (the .NET 10 SDK). Install it, or pass --no-decompile.")
    log(f"Installing ilspycmd {ILSPYCMD_VERSION} into deps/tools")
    subprocess.run(["dotnet", "tool", "uninstall", "ilspycmd", "--tool-path", str(TOOLS_DIR)],
                   capture_output=True)
    subprocess.run(["dotnet", "tool", "install", "ilspycmd", "--version", ILSPYCMD_VERSION,
                    "--tool-path", str(TOOLS_DIR)], check=True, stdout=subprocess.DEVNULL)


def decompile(game):
    managed = managed_dir(game)
    for name in DECOMPILE:
        dll = managed / f"{name}.dll"
        out = DEPS / "decompiled" / name
        stamp = out / ".source-sha256"
        digest = sha256(dll)
        if stamp.is_file() and stamp.read_text().strip() == digest:
            log(f"{name}.dll: already decompiled")
            continue
        log(f"Decompiling {name}.dll into deps/decompiled/{name}")
        shutil.rmtree(out, ignore_errors=True)
        subprocess.run([str(ILSPYCMD), "--project", "--outputdir", str(out), "--referencepath", str(managed),
                        str(dll)], check=True, stdout=subprocess.DEVNULL)
        # The code is only for reading; a stray project file would only confuse IDEs
        for project in out.glob("*.csproj"):
            project.unlink()
        stamp.write_text(digest + "\n")


# ---------------------------------------------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser(prog="setup.sh", description=__doc__.split("\n\n")[0],
                                 epilog="Everything goes into deps/ and Environment.props, which git ignores.")
    ap.add_argument("--game", default=os.environ.get("VALHEIM_INSTALL"),
                    help="the Valheim install, the folder that contains valheim_Data "
                         "(default: $VALHEIM_INSTALL, then the last path used, then the Steam libraries)")
    ap.add_argument("--no-decompile", action="store_true", help="skip ilspycmd and the decompiled game code")
    ap.add_argument("--install-bepinex", action="store_true",
                    help="also copy BepInEx into the game folder (needed to run the mod; keeps an existing BepInEx.cfg)")
    args = ap.parse_args()

    game = resolve_game(args.game)
    log(f"Valheim: {game}")
    write_env_props(game)
    download_bepinex()
    if not args.no_decompile:
        install_ilspycmd()
        decompile(game)
    if args.install_bepinex:
        install_bepinex(game)

    print()
    print("Ready. Build with:  dotnet build")
    if not (game / "BepInEx" / "core" / "BepInEx.dll").is_file():
        print("BepInEx is not installed in the game yet. To run the mod: ./setup.sh --install-bepinex")
    else:
        print("Deploy the build into the game with:  dotnet build -p:Deploy=true")
        if os.name != "nt":
            print("Steam launch options for the game (Properties > General):  ./start_game_bepinex.sh %command%")


if __name__ == "__main__":
    main()
