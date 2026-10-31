"""Fails when a public error code is never raised, or ERROR_CODES.md and the constants disagree.

Every `public const string` of a public `*ErrorCodes` class in `src/` is a compatibility promise from
1.0 on (ADR 0032), so each must be:
- referenced somewhere in `src/` besides its own declaration (by `Class.Name`, or by its value as a
  string literal), so the error can actually occur (#518);
- listed in docs/ERROR_CODES.md, which in turn lists no code that is not declared.

Run from the repository root: python3 tools/docs/check-error-codes.py
"""

import re
import subprocess
import sys

DOC = "docs/ERROR_CODES.md"

CONSTANT = re.compile(r'^\s*public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;')

# A type declaration is read by its leading words, not by a regular expression: a line is split once
# and walked, so the check stays linear whatever the line holds.
MODIFIERS = {"public", "internal", "private", "protected", "static", "sealed", "abstract", "partial", "file"}
TYPE_KEYWORDS = {"class", "record", "struct", "interface", "enum"}
DOC_ROW = re.compile(r"^\|\s*`(\w+)`\s*\|\s*`([a-z0-9_]+)`\s*\|")


def tracked_files():
    output = subprocess.run(["git", "ls-files", "-z"], capture_output=True, check=True).stdout
    return [f for f in output.decode("utf-8").split("\0") if f]


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as handle:
        return handle.read()


def type_declaration(line):
    """(is public, is class, name) when the line declares a type, else None."""
    words = line.split()
    modifiers = []
    for index, word in enumerate(words):
        if word in MODIFIERS:
            modifiers.append(word)
            continue
        if word in TYPE_KEYWORDS and index + 1 < len(words):
            name = re.match(r"\w+", words[index + 1])
            return ("public" in modifiers, word == "class", name.group(0)) if name else None
        return None
    return None


def declared_codes(sources):
    """(class, name, value, path, line number) of each constant of a public *ErrorCodes class."""
    for path, text in sources.items():
        current = None
        for number, line in enumerate(text.splitlines(), start=1):
            declared = type_declaration(line)
            if declared:
                public, is_class, name = declared
                current = name if public and is_class and name.endswith("ErrorCodes") else None
                continue
            constant = CONSTANT.match(line)
            if current and constant:
                yield current, constant.group(1), constant.group(2), path, number


def is_raised(cls, name, value, path, number, sources):
    qualified = re.compile(rf"\b{re.escape(cls)}\.{re.escape(name)}\b")
    bare = re.compile(rf"(?<![\w.]){re.escape(name)}\b")
    literal = f'"{value}"'
    for other_path, text in sources.items():
        for other_number, line in enumerate(text.splitlines(), start=1):
            if other_path == path and other_number == number:
                continue
            if qualified.search(line) or literal in line:
                return True
            # Inside its own class a code may be used without the class name.
            if other_path == path and bare.search(line) and not CONSTANT.match(line):
                return True
    return False


def main():
    files = tracked_files()
    sources = {path: read(path) for path in files if path.startswith("src/") and path.endswith(".cs")}
    codes = list(declared_codes(sources))
    problems = []

    for cls, name, value, path, number in codes:
        if not is_raised(cls, name, value, path, number, sources):
            problems.append((path, number, f"{cls}.{name} ('{value}') is declared but nothing raises it: remove it, or raise it."))

    documented = {}
    for number, line in enumerate(read(DOC).splitlines(), start=1):
        row = DOC_ROW.match(line)
        if row:
            documented.setdefault(row.group(2), number)

    declared_values = {value: (path, number, cls, name) for cls, name, value, path, number in codes}
    for value, (path, number, cls, name) in sorted(declared_values.items()):
        if value not in documented:
            problems.append((path, number, f"{cls}.{name} ('{value}') is not listed in {DOC}."))
    for value, number in sorted(documented.items(), key=lambda item: item[1]):
        if value not in declared_values:
            problems.append((DOC, number, f"'{value}' is listed but no public *ErrorCodes class declares it."))

    for path, number, message in problems:
        print(f"::error file={path},line={number}::{message}")

    print(f"Error codes: {len(codes)} declared, {len(documented)} documented, {len(problems)} problem(s).")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
