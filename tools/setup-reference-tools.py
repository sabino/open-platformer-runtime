#!/usr/bin/env python3
"""Install local development references and the macOS ARM64 original-ROM oracle."""
from pathlib import Path
import hashlib
import json
import platform
import shutil
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
REFERENCES = {
    "snesrev-smw": ("https://github.com/snesrev/smw.git", "eae20c65c58930c8b62c76188d259579ad4130f1"),
    "smw-src": ("https://github.com/galaxyhaxz/smw-src.git", "30e66e1ae3e8e0e518885dbcd5f85f2909a4969e"),
    "SuperMarioWorldRecomp": ("https://github.com/mstan/SuperMarioWorldRecomp.git", "8dedb2869414f20d1d86d34081be26594560cc15"),
    "mesen": ("https://github.com/SourMesen/Mesen2.git", "137ae7ce3bf3f539d007e2c4ef3cb3b6c97672a1"),
}
ARCHIVE_SHA256 = "c16068b3d934f7b993b782a0652b8f0e7fc0cde7464862fea91d5e76b68a813f"


def run(*command: str, **kwargs):
    return subprocess.run(command, check=True, **kwargs)


def setup() -> None:
    reference_root = ROOT / ".godot_local/references"
    reference_root.mkdir(parents=True, exist_ok=True)
    for name, (url, revision) in REFERENCES.items():
        destination = reference_root / name
        if not destination.exists():
            run("git", "clone", "--no-checkout", url, str(destination))
            run("git", "-C", str(destination), "checkout", "--detach", revision)
        actual = subprocess.check_output(["git", "-C", str(destination), "rev-parse", "HEAD"], text=True).strip()
        if actual != revision:
            raise RuntimeError(f"Existing reference {name} differs from the pin; left unchanged")
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        print("Sources prepared. Configure SMW_MESEN_BIN for this host's external oracle.")
        return

    folder = ROOT / ".godot_local/oracles/mesen-2.1.1"
    folder.mkdir(parents=True, exist_ok=True)
    archive = folder / "Mesen_2.1.1_macOS_ARM64_AppleSilicon.zip"
    if not archive.exists():
        url = "https://github.com/SourMesen/Mesen2/releases/download/2.1.1/" + archive.name
        with urllib.request.urlopen(url, timeout=60) as response, archive.open("wb") as output:
            shutil.copyfileobj(response, output)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != ARCHIVE_SHA256:
        raise RuntimeError("Mesen release checksum mismatch")
    if not (folder / "Mesen.app").exists():
        run("/usr/bin/ditto", "-x", "-k", str(archive), str(folder))
        run("/usr/bin/ditto", "-x", "-k", str(folder / "Mesen.app.zip"), str(folder))
    application = folder / "Mesen.app/Contents/MacOS"
    (application / "settings.json").write_text(json.dumps({
        "Debug": {"ScriptWindow": {"AllowIoOsAccess": True}},
        "Snes": {"RamPowerOnState": 1},
    }))

    core = application / "MesenCore.dylib"
    if not core.exists():
        # With a local profile and no ROM, this headless branch extracts the
        # release dependencies and exits before creating a normal application UI.
        extraction = subprocess.run([str(application / "Mesen"), "--testRunner", "--doNotSaveSettings"])
        if extraction.returncode not in (0, 255):
            raise RuntimeError("Mesen dependency extraction failed")
    sdk_tool = shutil.which("pkg-config")
    if not sdk_tool:
        raise RuntimeError("SDL2-compatible development library required (Malt: sdl2-compat)")
    library = Path(sdk_tool).parent.parent / "lib/libSDL2-2.0.0.dylib"
    if not library.exists():
        library = Path(subprocess.check_output([sdk_tool, "--variable=libdir", "sdl2"], text=True).strip()) / "libSDL2-2.0.0.dylib"
    if not library.exists():
        raise RuntimeError("Installed SDL2-compatible dylib not found")
    links = subprocess.check_output(["otool", "-L", str(core)], text=True)
    original_path = "/opt/homebrew/opt/sdl2/lib/libSDL2-2.0.0.dylib"
    if original_path in links and str(library) != original_path:
        metadata = core.stat()
        original = application / "MesenCore.original.dylib"
        if not original.exists():
            shutil.copy2(core, original)
        run("codesign", "--remove-signature", str(core))
        run("install_name_tool", "-change", original_path, str(library), str(core))
        unsigned = application / "MesenCore.unsigned.dylib"
        shutil.copy2(core, unsigned)
        run("codesign", "--force", "--sign", "-", str(core))
        padding = metadata.st_size - core.stat().st_size
        if padding < 0:
            raise RuntimeError("Relocated core cannot fit release extraction metadata")
        shutil.copyfile(unsigned, core)
        run("codesign", "--force", "--sign", "-", "--signature-size", str(padding), str(core))
        run("codesign", "--verify", str(core))
        if core.stat().st_size != metadata.st_size:
            raise RuntimeError("Relocated core size differs from the release extraction metadata")
        # Mesen re-extracts a dependency when its length or timestamp changes.
        import os
        os.utime(core, ns=(metadata.st_atime_ns, metadata.st_mtime_ns))
    run("codesign", "--verify", str(core))
    (folder / "tool.json").write_text(json.dumps({
        "version": "2.1.1", "archive_sha256": ARCHIVE_SHA256,
        "core_sha256": hashlib.sha256(core.read_bytes()).hexdigest(),
        "sdl_library": str(library),
        "sdl_sha256": hashlib.sha256(library.read_bytes()).hexdigest(),
        "role": "external development oracle; never a runtime dependency",
    }, indent=2) + "\n")
    print("Read-only reference pins and local Mesen oracle prepared.")


if __name__ == "__main__":
    setup()
