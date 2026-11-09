"""Validate paths and heading fragments in tracked Markdown without fetching external URLs.

Use relative paths: /path links to the GitHub site root, not this repository.
Run from the repository root: python3 tools/docs/check-local-links.py
"""

import html
import posixpath
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from urllib.parse import unquote, urlsplit


FENCE = re.compile(r"^( *)(`{3,}|~{3,})(.*)$")
LIST_ITEM = re.compile(r"^( *)(?:[-+*]|\d+[.)]) +")
HEADING = re.compile(r"^ {0,3}#{1,6}(?:\s+|$)(.*)$")
SETEXT = re.compile(r"^ {0,3}(?:=+|-+)\s*$")
REFERENCE = re.compile(r"^ *\[([^]\n]+)\]:\s*(<[^>]+>|\S+)")
ATTRIBUTE = re.compile(r"\b(?:href|src)\s*=\s*(['\"])(.*?)\1", re.I)
EXPLICIT_ID = re.compile(r"<[a-z][^>]*?\b(?:id|name)\s*=\s*(?:['\"]([^'\"]+)['\"]|([^\s>]+))", re.I)
AUTOLINK = re.compile(r"<((?:\.{1,2}/|/)[^>\s]+)>")
CODE = re.compile(r"(`+)(.*?)\1")
SCHEME = re.compile(r"^[a-z][a-z0-9+.-]*:", re.I)
GITHUB_LINE = re.compile(r"^L\d+(?:-L\d+)?$")


@dataclass(frozen=True)
class Finding:
    path: str
    line: int
    message: str


def visible_lines(text):
    fence = None
    comment = False
    list_indents = []
    for raw in text.splitlines():
        original = re.sub(r"^ {0,3}(?:> ?)+", "", raw)
        if fence is not None:
            match = FENCE.match(original)
            if match and match.group(2)[0] == fence[0] and len(match.group(2)) >= len(fence) and not match.group(3).strip():
                fence = None
            yield ""
            continue
        line = original
        while line:
            if comment:
                end = line.find("-->")
                if end < 0:
                    line = ""
                    break
                line, comment = line[end + 3 :], False
            start = line.find("<!--")
            if start < 0:
                break
            end = line.find("-->", start + 4)
            if end < 0:
                line, comment = line[:start], True
                break
            line = line[:start] + line[end + 3 :]
        indent = len(original) - len(original.lstrip(" "))
        item = LIST_ITEM.match(line)
        if line.strip():
            while list_indents and indent < list_indents[-1]:
                list_indents.pop()
        if item and (indent <= 3 or list_indents and indent >= list_indents[-1]):
            list_indents.append(item.end())
        in_list = bool(list_indents and indent >= list_indents[-1])
        indented_code = original.startswith("\t") or indent >= 4 and not item and not (in_list and indent < list_indents[-1] + 4)
        match = FENCE.match(line)
        if match and not indented_code and (indent <= 3 or in_list and indent - list_indents[-1] <= 3):
            fence = match.group(2)
            yield ""
        elif indented_code:
            yield ""
        else:
            yield line


def slug(text):
    text = html.unescape(re.sub(r"<[^>]*>", "", text))
    text = re.sub(r"!?\[([^]]+)\](?:\([^)]*\)|\[[^]]*\])", r"\1", text)
    text = text.replace("`", "").replace("*", "").replace("~", "")
    text = re.sub(r"[^\w\s-]", "", text.lower())
    return re.sub(r"\s", "-", text.strip())


def anchors(lines):
    found = set()
    headings = set()
    previous = ""
    for line in lines:
        without_code = CODE.sub(lambda match: " " * len(match.group()), line)
        for match in EXPLICIT_ID.finditer(without_code):
            found.add(html.unescape(match.group(1) or match.group(2)))
        match = HEADING.match(line)
        paragraph = previous and not (HEADING.match(previous) or LIST_ITEM.match(previous) or REFERENCE.match(previous)
                                      or previous.lstrip().startswith(("<", "|")) or re.fullmatch(r"[-*_ ]{3,}", previous.strip()))
        title = re.sub(r"\s+#+\s*$", "", match.group(1)) if match else previous if SETEXT.match(line) and paragraph else None
        if title is not None:
            base = slug(title)
            candidate = base
            number = 0
            while candidate in headings:
                number += 1
                candidate = f"{base}-{number}"
            headings.add(candidate)
            found.add(candidate)
        previous = line if line.strip() else ""
    return found


def closing_bracket(text, start):
    depth = 1
    for index in range(start + 1, len(text)):
        if text[index - 1] == "\\":
            continue
        if text[index] == "[":
            depth += 1
        elif text[index] == "]":
            depth -= 1
            if depth == 0:
                return index
    return None


def inline_destination(text, start):
    index = start + 1
    while index < len(text) and text[index].isspace():
        index += 1
    if index < len(text) and text[index] == "<":
        end = text.find(">", index + 1)
        if end >= 0 and ")" in text[end + 1 :]:
            return text[index + 1 : end], text.find(")", end + 1) + 1
        return None
    begin = index
    depth = 0
    while index < len(text):
        char = text[index]
        if char == "\\":
            index += 2
            continue
        if char == "(":
            depth += 1
        elif char == ")":
            if depth == 0:
                return text[begin:index], index + 1
            depth -= 1
        elif char.isspace() and depth == 0:
            end = text.find(")", index)
            return (text[begin:index], end + 1) if end >= 0 else None
        index += 1
    return None


def reference_name(label):
    return " ".join(label.split()).casefold()


def inline_links(text, references):
    index = 0
    while index < len(text):
        if text[index] != "[" or (index > 0 and text[index - 1] == "\\"):
            index += 1
            continue
        end = closing_bracket(text, index)
        if end is None:
            index += 1
            continue
        label = text[index + 1 : end]
        yield from inline_links(label, references)
        after = end + 1
        if after < len(text) and text[after] == "(":
            parsed = inline_destination(text, after)
            if parsed:
                yield parsed[0]
                index = parsed[1]
                continue
        elif after < len(text) and text[after] == "[":
            close = closing_bracket(text, after)
            if close is not None:
                key = reference_name(text[after + 1 : close] or label)
                if key in references:
                    yield references[key]
                index = close + 1
                continue
        elif reference_name(label) in references:
            yield references[reference_name(label)]
        index = end + 1


def markdown_links(lines):
    references = {}
    for line in lines:
        if match := REFERENCE.match(line):
            references[reference_name(match.group(1))] = match.group(2).strip("<>")
    for number, original in enumerate(lines, 1):
        if REFERENCE.match(original):
            continue
        line = CODE.sub(lambda match: " " * len(match.group()), original)
        yield from ((number, target) for target in inline_links(line, references))
        yield from ((number, match.group(2)) for match in ATTRIBUTE.finditer(line))
        yield from ((number, match.group(1)) for match in AUTOLINK.finditer(line)
                    if not re.fullmatch(r"/[a-z][\w-]*", match.group(1), re.I))


def check_links(root, files):
    tracked = set(files)
    checked = 0
    findings = []
    headings = {}
    for source in sorted(path for path in tracked if path.lower().endswith(".md")):
        lines = list(visible_lines((root / source).read_text(encoding="utf-8-sig")))
        for number, destination in markdown_links(lines):
            destination = html.unescape(destination).strip()
            if destination.startswith("//") or SCHEME.match(destination):
                continue
            checked += 1
            parsed = urlsplit(destination)
            target_path = unquote(parsed.path)
            if target_path.startswith("/"):
                findings.append(Finding(source, number, f"Root-relative URL is not a repository link on GitHub: {destination}"))
                continue
            relative = source if not target_path else posixpath.join(posixpath.dirname(source), target_path)
            target = posixpath.normpath(relative)
            if target == ".." or target.startswith("../"):
                findings.append(Finding(source, number, f"Local link leaves the repository: {destination}"))
                continue
            if target in tracked and (root / target).is_file():
                markdown = target if target.lower().endswith(".md") else None
            elif (root / target).is_dir() and (target == "." or any(path.startswith(target.rstrip("/") + "/") for path in tracked)):
                markdown = "README.md" if target == "." else target.rstrip("/") + "/README.md"
            else:
                findings.append(Finding(source, number, f"Local link target is missing or untracked: {destination}"))
                continue
            fragment = unquote(parsed.fragment)
            if fragment and markdown in tracked:
                if markdown not in headings:
                    headings[markdown] = anchors(list(visible_lines((root / markdown).read_text(encoding="utf-8-sig"))))
                if fragment not in headings[markdown] and fragment.removeprefix("user-content-") not in headings[markdown]:
                    findings.append(Finding(source, number, f"Markdown heading fragment does not exist: {destination}"))
            elif fragment and markdown is None and not GITHUB_LINE.match(fragment):
                findings.append(Finding(source, number, f"A non-Markdown target has no heading fragment: {destination}"))
            elif fragment and markdown not in tracked:
                findings.append(Finding(source, number, f"Directory has no tracked README heading: {destination}"))
    return checked, findings


def main():
    root = Path.cwd()
    output = subprocess.run(["git", "ls-files", "-z"], cwd=root, capture_output=True, check=True).stdout
    files = [path for path in output.decode("utf-8").split("\0") if path]
    checked, findings = check_links(root, files)
    for finding in findings:
        print(f"::error file={finding.path},line={finding.line}::{finding.message}")
    print(f"Markdown local links: {checked} checked, {len(findings)} problem(s).")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
