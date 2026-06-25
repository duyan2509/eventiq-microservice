"""Download Spider benchmark dataset to eval/data/spider/.

Downloads from HuggingFace Hub (spider dataset by Yale/taoyds).
Includes dev.json, tables.json, and all 200 SQLite databases.

Usage (from AnalyticsService/ root):
  python -m eval.download_spider
"""
from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

EVAL_DIR = Path(__file__).parent
TARGET_DIR = EVAL_DIR / "data" / "spider"

HF_REPO = "spider"


def install_if_missing(package: str) -> None:
    try:
        __import__(package.replace("-", "_"))
    except ImportError:
        print(f"Installing {package}...")
        subprocess.run([sys.executable, "-m", "pip", "install", package], check=True)


def download() -> None:
    install_if_missing("huggingface_hub")

    from huggingface_hub import snapshot_download

    TARGET_DIR.mkdir(parents=True, exist_ok=True)

    # Check if already downloaded
    if (TARGET_DIR / "dev.json").exists() and (TARGET_DIR / "tables.json").exists():
        db_count = len(list((TARGET_DIR / "database").glob("*/*.sqlite"))) if (TARGET_DIR / "database").exists() else 0
        if db_count > 0:
            print(f"Spider already downloaded: {db_count} databases at {TARGET_DIR}")
            return

    print("Downloading Spider dataset from HuggingFace Hub...")
    print("(~1.4 GB including all 200 SQLite databases — this may take a few minutes)\n")

    tmp_dir = EVAL_DIR / "data" / "_spider_hf_tmp"
    tmp_dir.mkdir(parents=True, exist_ok=True)

    snapshot_download(
        repo_id=HF_REPO,
        repo_type="dataset",
        local_dir=str(tmp_dir),
        ignore_patterns=["*.parquet", "*.arrow", ".git*"],
    )

    print("\nOrganizing files...")
    _organize(tmp_dir, TARGET_DIR)

    shutil.rmtree(tmp_dir, ignore_errors=True)

    db_count = len(list((TARGET_DIR / "database").glob("*/*.sqlite")))
    print(f"\nDone. {db_count} databases extracted to {TARGET_DIR}")
    print("Run `python -m eval.spider_eval` to start evaluation.")


def _organize(src: Path, dst: Path) -> None:
    """Copy dev.json, tables.json, database/ from the HF snapshot to dst."""
    # HuggingFace Spider layout varies — search for the files
    for json_name in ("dev.json", "tables.json"):
        candidates = list(src.rglob(json_name))
        if not candidates:
            print(f"WARNING: {json_name} not found in download")
            continue
        # Pick the shortest path (avoid nested copies)
        candidates.sort(key=lambda p: len(p.parts))
        shutil.copy2(candidates[0], dst / json_name)
        print(f"  {json_name} -> {dst / json_name}")

    # Copy database/ directory
    db_dirs = list(src.rglob("database"))
    db_dirs = [d for d in db_dirs if d.is_dir()]
    if not db_dirs:
        print("WARNING: database/ directory not found in download")
        return

    db_dirs.sort(key=lambda p: len(p.parts))
    src_db = db_dirs[0]
    dst_db = dst / "database"

    if dst_db.exists():
        shutil.rmtree(dst_db)
    shutil.copytree(src_db, dst_db)
    db_count = len(list(dst_db.glob("*/*.sqlite")))
    print(f"  database/ ({db_count} SQLite files) -> {dst_db}")


if __name__ == "__main__":
    download()
