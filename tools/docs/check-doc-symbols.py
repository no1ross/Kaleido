"""Fails when documentation names a Kaleido type or member that does not exist.

Every backticked identifier in the tracked Markdown that looks like Kaleido API (a name starting
with Kaleido, Process, Query, Step, Registry, Information, Mcp, Sqlite, ...) must be declared in
the repository's C# or TypeScript sources. Namespaces and file names are skipped. ADRs and the
audit records are historical and may name removed symbols, so they are not checked.

Run from the repository root: python3 tools/docs/check-doc-symbols.py
"""

import re
import subprocess
import sys

# Documents that record history on purpose (they name symbols that no longer exist).
HISTORICAL = ("docs/decisions/", "docs/DOCUMENTATION-AUDIT.md", "tests/FUNCTIONAL-TEST-AUDIT.md")

# Names that are not symbols, each with the reason it is written in code style.
ALLOWED = {
    "KaleidoProcessExecute": "endpoint-name value (docs/ANALYZERS.md, KAL0017)",
    "KaleidoRegistry": "endpoint-name value (docs/ANALYZERS.md, KAL0017)",
    "KaleidoProcessStepREgistry": "the historical endpoint-name typo KAL0017 prevents",
    "QueryContextSource": "type-name suffix the MCP adapter strips from tool names",
    "QueryViewSource": "type-name suffix the MCP adapter strips from tool names",
    "ProcessorMappings": "named in the PriorAuth handoff guide as a setting that does not exist",
    "IQueryable": ".NET type (System.Linq) that query sources return",
}

API_NAME = re.compile(r"^I?(Kaleido|Process|Processor|Query|Queryable|Step|Registry|Information|Mcp|Sqlite"
                      r"|HttpHeader|HttpCorrelation|HttpClientError)[A-Za-z0-9]*$")
SEGMENT = re.compile(r"^(I?[A-Z][A-Za-z0-9]*)(?:<[^>]*>)?(?:\(.*\))?$")
CODE_SPAN = re.compile(r"`([^`\n]+)`")
FILE_NAME = re.compile(r"\.(cs|csproj|md|json|ts|tsx|yml|yaml|props|targets|slnx|ps1|cmd|sh|txt|png|xml)$", re.I)

# Declarations are read line by line with patterns whose quantifiers never compete for the same
# characters, so matching stays linear.
TYPE_DECLARATION = re.compile(r"\b(?:class|record|struct|interface|enum|delegate) +(?:struct +|class +)?([A-Z]\w*)")
MEMBER_LINE = re.compile(r"^\s*(?:public|internal|protected|private)\b")
MEMBER_NAME = re.compile(r"\b([A-Z]\w*)(?= *(?:[({=;]|<[^<>(){};]*> *\())")  # name before ( { = ; or <T>(
ENUM_MEMBER_LINE = re.compile(r"^\s*([A-Z]\w*) *(?:=[^,;{}]*)?,?$")


def cs_declarations(text):
    lines = [line.rstrip() for line in text.splitlines()]
    for index, line in enumerate(lines):
        yield from TYPE_DECLARATION.findall(line)
        if MEMBER_LINE.match(line):
            # A member declaration may continue on the next lines (the name after a long return type).
            declaration, following = line, index + 1
            while not any(c in declaration for c in "({;=") and following < min(index + 4, len(lines)):
                declaration += " " + lines[following].strip()
                following += 1
            yield from MEMBER_NAME.findall(declaration)
        enum_member = ENUM_MEMBER_LINE.match(line)
        if enum_member:
            yield enum_member.group(1)
TS_DECLARATIONS = [re.compile(r"\b(?:class|interface|enum|type|function|const)\s+([A-Z]\w*)")]
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)


def tracked_files():
    output = subprocess.run(["git", "ls-files", "-z"], capture_output=True, check=True).stdout
    return [f for f in output.decode("utf-8").split("\0") if f]


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as handle:
        return handle.read()


def main():
    files = tracked_files()
    declared, namespaces = set(), set()
    for path in files:
        if path.endswith(".cs"):
            text = read(path)
            declared.update(cs_declarations(text))
            for namespace in NAMESPACE.findall(text):
                namespaces.update(namespace.split("."))
        elif path.endswith(".ts") and "node_modules" not in path:
            text = read(path)
            for pattern in TS_DECLARATIONS:
                declared.update(pattern.findall(text))
        elif path.endswith(".csproj"):
            namespaces.update(path.rsplit("/", 1)[-1][: -len(".csproj")].split("."))

    findings = 0
    for path in (f for f in files if f.endswith(".md") and not f.startswith(HISTORICAL)):
        for number, line in enumerate(read(path).splitlines(), 1):
            for span in CODE_SPAN.findall(line):
                if FILE_NAME.search(span.strip()):
                    continue
                for part in re.split(r"[.\s/]", span):
                    match = SEGMENT.match(part.strip())
                    if not match:
                        continue
                    name = match.group(1)
                    if (API_NAME.match(name) and name not in declared and name not in namespaces
                            and name not in ALLOWED):
                        print(f"::error file={path},line={number}::`{name}` is not declared anywhere in the sources.")
                        findings += 1

    print(f"Documentation symbols: {findings} unresolved.")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
