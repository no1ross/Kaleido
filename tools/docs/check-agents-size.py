"""Fails when an AGENTS.md is too large for agent tooling to load it whole (#455).

Agent runtimes inject AGENTS.md files as rules and truncate them silently; one cut the root
guide at 16,384 bytes, dropping its build, test and quality-gate section. Every tracked
AGENTS.md must therefore stay under LIMIT bytes (UTF-8: an em dash counts three), with a small
margin below that observed cut. Move detail into docs/ and leave a link instead of raising the
limit.

Run from the repository root: python3 tools/docs/check-agents-size.py
"""

import pathlib
import subprocess
import sys

LIMIT = 16_000


def tracked_agents_files():
    listed = subprocess.run(
        ["git", "ls-files", "--", "AGENTS.md", "*/AGENTS.md"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    return [pathlib.Path(line) for line in listed.splitlines() if line]


def main():
    files = tracked_agents_files()

    if not files:
        print("::error::No AGENTS.md found; run from the repository root.")
        return 1

    failed = 0

    for path in sorted(files):
        size = len(path.read_bytes())
        print(f"{path.as_posix()}: {size:,} bytes")

        if size > LIMIT:
            failed += 1
            print(
                f"::error file={path.as_posix()}::{size:,} bytes is over the {LIMIT:,}-byte limit: "
                "agent tooling may truncate it. Move detail into docs/ and link to it."
            )

    print(f"AGENTS.md size: {len(files)} checked, {failed} over {LIMIT:,} bytes.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
