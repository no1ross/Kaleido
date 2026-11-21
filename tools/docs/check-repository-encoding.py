"""Check repository text for invalid UTF-8, U+FFFD, and common mojibake.

Run from the repository root: python3 tools/docs/check-repository-encoding.py
"""

import os
import re
import subprocess
import sys
from pathlib import Path
from typing import NamedTuple


ROOTS = ("src", "tools", "tests", "docs", "samples")
SKIPPED = frozenset({"bin", "obj", "node_modules", ".git", ".vs", ".angular", "testresults", "artifacts", "coverage"})
EXTENSIONS = frozenset({
    ".cs", ".md", ".txt", ".json", ".yml", ".yaml", ".xml", ".props", ".targets", ".csproj", ".slnx",
    ".editorconfig", ".http", ".ts", ".html", ".scss", ".css", ".razor", ".ps1", ".cmd", ".sh",
})
CORRUPTION = re.compile("\ufffd|\u00e2\u20ac|\u00c3[\u0080-\u00bf]|\u00c2[\u0080-\u00bf]|\u0393\u00c7")


class Finding(NamedTuple):
    path: Path
    line: int
    message: str


def is_text(path):
    return path.suffix.casefold() in EXTENSIONS or path.name.casefold() in EXTENSIONS


def tracked_files(root):
    listed = subprocess.run(["git", "-C", str(root), "ls-files", "-z"], check=True, capture_output=True).stdout
    return [os.fsdecode(path) for path in listed.split(b"\0") if path]


def candidate_files(root, tracked):
    paths = {Path(name) for name in tracked if is_text(Path(name))}
    paths.update(path.relative_to(root) for path in root.iterdir() if path.is_file() and is_text(path))
    for name in ROOTS:
        folder = root / name
        if not folder.is_dir():
            continue
        for directory, subdirectories, files in os.walk(folder):
            subdirectories[:] = [child for child in subdirectories
                                 if child.casefold() not in SKIPPED and not (Path(directory) / child).is_symlink()]
            paths.update((Path(directory) / file).relative_to(root) for file in files if is_text(Path(file)))
    return sorted(paths)


def check_files(root, paths):
    selected = sorted(set(paths))
    findings = []
    for relative in selected:
        path = root / relative
        try:
            raw = path.read_bytes()
        except OSError as error:
            findings.append(Finding(relative, 1, f"Could not read text file: {error.strerror or type(error).__name__}."))
            continue
        try:
            text = raw.decode("utf-8")
        except UnicodeDecodeError as error:
            findings.append(Finding(relative, raw[:error.start].count(b"\n") + 1, "Not valid UTF-8."))
            continue
        for number, line in enumerate(text.splitlines(), 1):
            if CORRUPTION.search(line):
                findings.append(Finding(relative, number, "U+FFFD or mojibake (UTF-8 decoded as another code page)."))
    return len(selected), findings


def main():
    root = Path.cwd()
    if not (root / "Kaleido.slnx").is_file():
        print("::error::Run from the Kaleido repository root.")
        return 1
    checked, findings = check_files(root, candidate_files(root, tracked_files(root)))
    for finding in findings:
        print(f"::error file={finding.path.as_posix()},line={finding.line}::{finding.message}")
    print(f"Repository encoding: {checked} checked, {len(findings)} problem(s).")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
