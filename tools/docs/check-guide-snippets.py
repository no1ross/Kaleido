"""Fails when a C# example in the consumer guide (docs/guide) is not compiled code.

Every ```csharp block in docs/guide/*.md must be preceded by a marker naming its compiled region:

    <!-- snippet: getting-started-source -->
    ```csharp
    ...
    ```

and the block must equal that region of docs/guide/examples (the project
Kaleido.Guide.Examples, built by CI), between `// snippet: <name>` and `// end snippet`.
Indentation common to the region's lines is ignored, as is trailing whitespace. So the guide
cannot show code that does not compile or that drifted from the API. A block that is not C# code
to compile (a fragment of configuration, a signature excerpt) is marked
`<!-- not-compiled: <reason> -->` instead.

Run from the repository root: python3 tools/docs/check-guide-snippets.py
"""

import pathlib
import re
import sys

GUIDE = pathlib.Path("docs/guide")
EXAMPLES = GUIDE / "examples"

# The marker's value is stripped in code: no two quantifiers here compete for the same characters,
# so a line that is not a marker fails fast (no backtracking).
MARKER = re.compile(r"<!--\s*(snippet|not-compiled):([^>]*)-->$")
REGION_START = re.compile(r"^\s*// snippet: (\S+)\s*$")
REGION_END = re.compile(r"^\s*// end snippet\s*$")


def normalize(lines):
    lines = [line.rstrip() for line in lines]
    while lines and not lines[0]:
        lines.pop(0)
    while lines and not lines[-1]:
        lines.pop()
    indents = [len(line) - len(line.lstrip()) for line in lines if line]
    common = min(indents, default=0)
    return [line[common:] for line in lines]


def regions():
    found = {}
    for path in sorted(EXAMPLES.rglob("*.cs")):
        if any(part in ("bin", "obj") for part in path.parts):
            continue
        lines = path.read_text(encoding="utf-8-sig").splitlines()
        name, start = None, 0
        for number, line in enumerate(lines):
            if name is None and (match := REGION_START.match(line)):
                name, start = match.group(1), number + 1
            elif name is not None and REGION_END.match(line):
                if name in found:
                    sys.exit(f"{path}: snippet '{name}' is defined twice")
                found[name] = (normalize(lines[start:number]), path)
                name = None
        if name is not None:
            sys.exit(f"{path}: snippet '{name}' has no '// end snippet'")
    return found


def blocks(path):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    marker = None
    number = 0
    while number < len(lines):
        line = lines[number]
        if match := MARKER.match(line.strip()):
            marker = (match.group(1), match.group(2).strip())
        elif line.strip().startswith("```"):
            fence = line.strip()
            end = number + 1
            while end < len(lines) and lines[end].strip() != "```":
                end += 1
            yield number + 1, fence[3:].strip(), marker, normalize(lines[number + 1:end])
            marker = None
            number = end
        elif line.strip():
            marker = None if marker and not line.strip().startswith("<!--") else marker
        number += 1


def main():
    compiled = regions()
    used = set()
    failures = []

    for page in sorted(GUIDE.glob("*.md")):
        for line, language, marker, code in blocks(page):
            if language != "csharp":
                continue
            if marker is None:
                failures.append(f"{page}:{line}: a csharp block without '<!-- snippet: name -->' (or '<!-- not-compiled: reason -->')")
            elif marker[0] == "snippet":
                name = marker[1]
                used.add(name)
                if name not in compiled:
                    failures.append(f"{page}:{line}: snippet '{name}' is not a region of {EXAMPLES}")
                elif code != compiled[name][0]:
                    failures.append(f"{page}:{line}: snippet '{name}' differs from {compiled[name][1]}")
            elif not marker[1]:
                failures.append(f"{page}:{line}: 'not-compiled' needs a reason")

    for name in sorted(set(compiled) - used):
        failures.append(f"{compiled[name][1]}: snippet '{name}' is not shown in the guide")

    for failure in failures:
        print(failure)
    print(f"Guide snippets: {len(used)} checked, {len(failures)} problem(s).")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
