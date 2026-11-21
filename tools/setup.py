#!/usr/bin/env python3
"""Download and prepare everything needed to build and work on the mod.

Everything this script downloads or generates goes into deps/, and the path of the local Valheim install goes into
Environment.props. Both are git-ignored. Running it again only redoes what is missing or out of date: a different
pinned version, or a game update for the decompiled code.

    ./setup.sh                      find Valheim through Steam, download BepInEx and ilspycmd, decompile the game code
    ./setup.sh --game <path>        use this Valheim install (the Steam Valheim folder)
    ./setup.sh --no-decompile       skip ilspycmd and the decompiled game code
    ./setup.sh --install-bepinex    also copy BepInEx into the game folder, which is needed to run the mod

On Windows, setup.bat takes the same options. The game path can also be given in the VALHEIM_INSTALL environment
variable. Python 3, standard library only.
ilspycmd needs the .NET 10 SDK (the same one that builds the mod).
"""
import argparse
import datetime
import hashlib
import os
import platform
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
# Apple Silicon needs BepInEx and UnityDoorstop builds that have not reached the regular BepInEx release yet.
MACOS_BEPINEX_VERSION = "5.4.23.5-01fd4aa"
MACOS_BEPINEX_SHA256 = "dfd66508691984320e173544036cefe0f80c7f4952fc715b3cb2704f41788824"
MACOS_BEPINEX_URL = "https://thevalheim.wiki/downloads/BepInEx_macos_valheim_{version}.zip"
ILSPYCMD_VERSION = "11.1.0.9782"
# Game assemblies that are decompiled into deps/decompiled/ to read the game's code
DECOMPILE = ["assembly_valheim", "assembly_utils", "assembly_guiutils"]
VALHEIM_APP_ID = 892970
# Where Steam keeps libraryfolders.vdf: macOS, native Linux, the Debian/Ubuntu package, Flatpak, Windows
STEAM_ROOTS = [
    Path.home() / "Library" / "Application Support" / "Steam",
    Path.home() / ".steam" / "steam",
    Path.home() / ".local" / "share" / "Steam",
    Path.home() / ".var" / "app" / "com.valvesoftware.Steam" / ".local" / "share" / "Steam",
    Path("C:/Program Files (x86)/Steam"),
]

# How the scripts are run on this system, for the messages
SETUP = "setup.bat" if os.name == "nt" else "./setup.sh"
BUILD = "build.bat" if os.name == "nt" else "./build.sh"

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


GAME_LAYOUTS = (
    Path("valheim_Data") / "Managed",
    Path("valheim.app") / "Contents" / "Resources" / "Data" / "Managed",
)


def game_layout(game):
    """The path from a Valheim Steam folder to Managed, or None when this is not a game install."""
    for relative in GAME_LAYOUTS:
        if (game / relative / "assembly_valheim.dll").is_file():
            return relative
    return None


def managed_dir(game):
    """The installed game's Managed directory. Call is_game_dir first for untrusted paths."""
    relative = game_layout(game)
    return game / (relative if relative is not None else GAME_LAYOUTS[0])


def normalize_game_dir(path):
    """Return the Steam Valheim folder for a root, valheim.app, or its legacy Resources path."""
    path = Path(path).expanduser().resolve()
    candidates = [path]
    if path.name == "valheim.app":
        candidates.append(path.parent)
    resources_tail = ("valheim.app", "Contents", "Resources")
    if len(path.parts) >= 3 and path.parts[-3:] == resources_tail:
        candidates.append(path.parents[2])
    for candidate in candidates:
        if game_layout(candidate) is not None:
            return candidate
    return path


def is_game_dir(path):
    return game_layout(path) is not None


def is_macos_game(game):
    return game_layout(game) == GAME_LAYOUTS[1]


# ---------------------------------------------------------------------------------------------------------------
# The game install
# ---------------------------------------------------------------------------------------------------------------

def steam_roots():
    """STEAM_ROOTS, after the folder the registry gives on Windows (Steam can be installed anywhere there)."""
    roots = list(STEAM_ROOTS)
    if os.name == "nt":
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
                roots.insert(0, Path(winreg.QueryValueEx(key, "SteamPath")[0]))
        except OSError:
            pass
    return roots


def find_in_steam():
    """The Valheim folder in the first Steam library that has the game installed, or None."""
    for root in steam_roots():
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
    relative_managed = game_layout(game)
    if relative_managed is None:
        die(f"{game} is not a Valheim install")
    text = ("<Project>\n"
            "  <!-- Written by tools/setup.py: this machine's Valheim install. Not committed. -->\n"
            "  <PropertyGroup>\n"
            f"    <ValheimDir>{escape(str(game))}</ValheimDir>\n"
            f"    <ValheimManagedDir>{escape(relative_managed.as_posix())}</ValheimManagedDir>\n"
            "  </PropertyGroup>\n"
            "</Project>\n")
    # Only rewrite when it changes, so MSBuild does not see a newer file and rebuild for nothing
    if not ENV_PROPS.is_file() or ENV_PROPS.read_text() != text:
        ENV_PROPS.write_text(text)


def resolve_game(given):
    if given:
        game = normalize_game_dir(given)
        if not is_game_dir(game):
            die(f"{game} is not a Valheim install (no supported Managed/assembly_valheim.dll was found)")
        return game
    saved = read_env_props()
    if saved:
        saved = normalize_game_dir(saved)
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


def copy_missing(source, destination):
    """Merge source into destination without replacing a player's config or plugins."""
    for src in sorted(source.rglob("*")):
        rel = src.relative_to(source)
        dst = destination / rel
        if src.is_dir():
            dst.mkdir(parents=True, exist_ok=True)
        elif not dst.exists():
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)


def migrate_legacy_macos_install(game):
    """Move the old, incorrectly nested loader out of valheim.app without discarding player data."""
    resources = game / "valheim.app" / "Contents" / "Resources"
    legacy_bepinex = resources / "BepInEx"
    if not legacy_bepinex.exists():
        return

    log("Migrating the old BepInEx installation out of valheim.app")
    for name in ("config", "plugins"):
        source = legacy_bepinex / name
        if source.is_dir():
            copy_missing(source, game / "BepInEx" / name)

    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    backup = game / f"BepInEx-macos-legacy-backup-{stamp}"
    backup.mkdir()
    # These are the files the old Linux-oriented pack put in Resources. Moving, rather than deleting, keeps recovery easy.
    for name in ("BepInEx", "start_game_bepinex.sh", "start_server_bepinex.sh", "run_bepinex.sh",
                 "doorstop_config.ini", "libdoorstop.dylib"):
        source = resources / name
        if source.exists() or source.is_symlink():
            shutil.move(str(source), str(backup / name))
    log(f"Legacy loader backed up to {backup}")


def download_macos_bepinex():
    downloads = DEPS / "downloads"
    downloads.mkdir(parents=True, exist_ok=True)
    zip_path = downloads / f"BepInEx_macos_valheim_{MACOS_BEPINEX_VERSION}.zip"
    if not zip_path.is_file() or sha256(zip_path) != MACOS_BEPINEX_SHA256:
        log(f"Downloading native macOS BepInEx {MACOS_BEPINEX_VERSION}")
        part = zip_path.with_name(zip_path.name + ".part")
        request = urllib.request.Request(MACOS_BEPINEX_URL.format(version=MACOS_BEPINEX_VERSION),
                                         headers={"User-Agent": "ValheimFOPReworked-setup"})
        with urllib.request.urlopen(request) as response, open(part, "wb") as f:
            shutil.copyfileobj(response, f)
        actual = sha256(part)
        if actual != MACOS_BEPINEX_SHA256:
            part.unlink()
            die(f"the macOS BepInEx download has sha256 {actual}, expected {MACOS_BEPINEX_SHA256}")
        part.replace(zip_path)
    return zip_path


def install_macos_bepinex(game):
    if sys.platform != "darwin" or platform.machine().lower() not in ("arm64", "aarch64"):
        die("native macOS BepInEx is supported only on an Apple Silicon Mac (arm64)")
    migrate_legacy_macos_install(game)
    zip_path = download_macos_bepinex()
    temporary = DEPS / "BepInEx_macos_valheim.tmp"
    shutil.rmtree(temporary, ignore_errors=True)
    temporary.mkdir(parents=True)
    with zipfile.ZipFile(zip_path) as z:
        z.extractall(temporary)
    log(f"Installing native macOS BepInEx {MACOS_BEPINEX_VERSION} into {game}")
    # The archive is laid out for the Valheim folder. Preserve all existing config and plugins.
    for src in sorted(temporary.rglob("*")):
        rel = src.relative_to(temporary)
        dst = game / rel
        protected = len(rel.parts) >= 2 and rel.parts[:2] in (("BepInEx", "config"), ("BepInEx", "plugins"))
        if src.is_dir() or (protected and dst.exists()):
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
    shutil.rmtree(temporary)
    launcher = game / "run_bepinex.sh"
    if not launcher.is_file():
        die("the macOS BepInEx archive did not contain run_bepinex.sh")
    launcher.chmod(launcher.stat().st_mode | stat.S_IXUSR)
    (game / "BepInEx" / "plugins").mkdir(parents=True, exist_ok=True)


def install_bepinex(game):
    """Copy the pack into the game folder, keeping the player's own BepInEx.cfg if there is one."""
    if is_macos_game(game):
        install_macos_bepinex(game)
        return
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
    ap = argparse.ArgumentParser(prog=SETUP.lstrip("./"), description=__doc__.split("\n\n")[0],
                                 epilog="Everything goes into deps/ and Environment.props, which git ignores.")
    ap.add_argument("--game", default=os.environ.get("VALHEIM_INSTALL"),
                    help="the Valheim Steam folder (or valheim.app/Contents/Resources on macOS) "
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
    print(f"Ready. Build with:  {BUILD}")
    if not (game / "BepInEx" / "core" / "BepInEx.dll").is_file():
        print(f"BepInEx is not installed in the game yet. To run the mod: {SETUP} --install-bepinex")
    else:
        print(f"Build and copy the plugin into the game with:  {BUILD} --deploy")
        if is_macos_game(game):
            print(f'Steam launch options (Properties > General):  "{game / "run_bepinex.sh"}" %command%')
        elif os.name != "nt":
            print("Steam launch options for the game (Properties > General):  ./start_game_bepinex.sh %command%")


if __name__ == "__main__":
    main()
