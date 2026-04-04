from __future__ import annotations

import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parent.parent
REFERENCE_SKIN = PROJECT_ROOT / "dist" / "Skins" / "25"
PANEL_FILES = [
    "TrafficView.panel.90.png",
    "TrafficView.panel.png",
    "TrafficView.panel.110.png",
    "TrafficView.panel.125.png",
    "TrafficView.panel.150.png",
]


def main() -> None:
    if not REFERENCE_SKIN.exists():
        raise FileNotFoundError(f"Reference skin folder not found: {REFERENCE_SKIN}")

    for name in PANEL_FILES:
        source = REFERENCE_SKIN / name
        target = ROOT / name
        if not source.exists():
            raise FileNotFoundError(f"Reference panel asset missing: {source}")
        shutil.copy2(source, target)
        print(target)


if __name__ == "__main__":
    main()
