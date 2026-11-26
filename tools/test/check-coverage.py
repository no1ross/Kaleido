"""Enforce merged shipped-source coverage and report authored-class goals."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def check(report: Path) -> int:
    try:
        root = ET.parse(report).getroot()
        valid = int(root.attrib["lines-valid"])
        covered = int(root.attrib["lines-covered"])
    except (OSError, ET.ParseError, KeyError, ValueError) as error:
        print(f"::error::Cannot read merged Cobertura report {report}: {error}")
        return 1

    if root.tag != "coverage" or valid <= 0 or covered < 0 or covered > valid:
        print(f"::error::Invalid or empty merged coverage in {report}.")
        return 1

    failures = 0
    advisories = 0
    measured = 0
    generated = 0
    for package in root.findall("./packages/package"):
        for cls in package.findall("./classes/class"):
            name = cls.get("name", "<unnamed>")
            filename = cls.get("filename", "")
            source = filename.replace("\\", "/").casefold()
            if not source.startswith("src/") and "/src/" not in source:
                print(f"::error::Out-of-scope coverage class {name}: {filename}")
                failures += 1
                continue
            if "/obj/" in source:
                generated += 1
                continue

            lines = cls.find("lines")
            if lines is None or len(lines) == 0:
                print(f"Non-measurable class: {name} ({filename})")
                continue
            try:
                hits_by_line = [(int(line.attrib["number"]), int(line.attrib["hits"])) for line in lines]
                if any(hits < 0 for _, hits in hits_by_line):
                    raise ValueError("negative hit count")
                missed = [number for number, hits in hits_by_line if hits == 0]
            except (KeyError, ValueError) as error:
                print(f"::error::Invalid lines for {name} ({filename}): {error}")
                failures += 1
                continue

            measured += 1
            count = len(lines)
            hits = count - len(missed)
            if hits * 100 < 97 * count:
                print(f"::warning file={filename},line={min(missed)}::Class {name}: {hits}/{count} lines covered ({hits / count:.2%}); goal: 97%")
                advisories += 1

    if measured == 0:
        print("::error::No measurable shipped source classes in the merged report.")
        failures += 1
    print(f"Merged shipped-source line coverage: {covered}/{valid} ({covered / valid:.2%}); floor: 97.5%. Measurable authored classes: {measured}; below 97% goal: {advisories}; generated classes included only in aggregate: {generated}.")
    if covered * 200 < 195 * valid:
        print("::error::Merged shipped-source line coverage is below 97.5%.")
        failures += 1
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(check(Path(sys.argv[1] if len(sys.argv) > 1 else "coverage/Cobertura.xml")))
