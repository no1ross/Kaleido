"""Boundary tests for the merged shipped-source coverage gate."""

import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


CHECKER = Path(__file__).with_name("check-coverage.py")


class CoverageGateTests(unittest.TestCase):
    def run_gate(self, classes):
        root = ET.Element("coverage", {
            "lines-valid": str(sum(total for _, _, _, total in classes)),
            "lines-covered": str(sum(covered for _, _, covered, _ in classes)),
        })
        packages = ET.SubElement(root, "packages")
        for name, filename, covered, total in classes:
            package = ET.SubElement(packages, "package", name="Kaleido.Test")
            cls = ET.SubElement(ET.SubElement(package, "classes"), "class", name=name, filename=filename)
            lines = ET.SubElement(cls, "lines")
            for index in range(total):
                ET.SubElement(lines, "line", number=str(index + 1), hits=str(int(index < covered)))
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "Cobertura.xml"
            ET.ElementTree(root).write(report, encoding="utf-8")
            return subprocess.run([sys.executable, str(CHECKER), str(report)], capture_output=True, text=True)

    def test_exact_aggregate_and_class_boundary_passes(self):
        result = self.run_gate([("Kaleido.Test.Work", "/repo/src/Test/Work.cs", 39, 40)])
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_aggregate_just_below_boundary_fails_even_when_class_passes(self):
        result = self.run_gate([("Kaleido.Test.Work", "/repo/src/Test/Work.cs", 38, 39)])
        self.assertEqual(1, result.returncode)
        self.assertIn("below 97.5%", result.stdout)
        self.assertNotIn("Class Kaleido.Test.Work:", result.stdout)

    def test_class_below_goal_warns_when_aggregate_passes(self):
        result = self.run_gate([
            ("Kaleido.Test.Work", "/repo/src/Test/Work.cs", 96, 100),
            ("Kaleido.Test.Other", "/repo/src/Test/Other.cs", 99, 100),
        ])
        self.assertEqual(0, result.returncode, result.stdout)
        self.assertIn("::warning file=/repo/src/Test/Work.cs,line=97::Class Kaleido.Test.Work: 96/100", result.stdout)
        self.assertIn("below 97% goal: 1", result.stdout)
        self.assertNotIn("below 97.5%", result.stdout)

    def test_generated_code_is_in_aggregate_but_not_the_authored_class_floor(self):
        result = self.run_gate([
            ("Kaleido.Test.Generated", "/repo/src/Test/obj/Release/Generated.g.cs", 0, 1),
            ("Kaleido.Test.Work", "/repo/src/Test/Work.cs", 40, 40),
        ])
        self.assertEqual(0, result.returncode, result.stdout)
        self.assertIn("generated classes included only in aggregate: 1", result.stdout)
        self.assertIn("40/41", result.stdout)

    def test_zero_line_class_is_reported_without_failing_other_measured_classes(self):
        result = self.run_gate([
            ("Kaleido.Test.Data", "/repo/src/Test/Data.cs", 0, 0),
            ("Kaleido.Test.Work", "/repo/src/Test/Work.cs", 39, 40),
        ])
        self.assertEqual(0, result.returncode, result.stdout)
        self.assertIn("Non-measurable class: Kaleido.Test.Data", result.stdout)

    def test_zero_coverable_lines_fails(self):
        result = self.run_gate([("Kaleido.Test.Data", "/repo/src/Test/Data.cs", 0, 0)])
        self.assertEqual(1, result.returncode)
        self.assertIn("Invalid or empty merged coverage", result.stdout)

    def test_out_of_scope_class_fails_instead_of_being_skipped(self):
        result = self.run_gate([("Kaleido.Guide.Work", "/repo/docs/guide/Work.cs", 40, 40)])
        self.assertEqual(1, result.returncode)
        self.assertIn("Out-of-scope coverage class", result.stdout)

    def test_windows_shipped_path_is_accepted(self):
        result = self.run_gate([("Kaleido.Test.Work", "C:\\Repos\\Kaleido\\src\\Test\\Work.cs", 39, 40)])
        self.assertEqual(0, result.returncode, result.stdout)

    def test_missing_report_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run([sys.executable, str(CHECKER), str(Path(directory) / "missing.xml")], capture_output=True, text=True)
        self.assertEqual(1, result.returncode)
        self.assertIn("Cannot read merged Cobertura report", result.stdout)

    def test_malformed_report_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "Cobertura.xml"
            report.write_text("<coverage>", encoding="utf-8")
            result = subprocess.run([sys.executable, str(CHECKER), str(report)], capture_output=True, text=True)
        self.assertEqual(1, result.returncode)
        self.assertIn("Cannot read merged Cobertura report", result.stdout)


if __name__ == "__main__":
    unittest.main()
