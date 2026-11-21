"""Self-test the repository encoding checker without changing repository files."""

import importlib.util
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("check-repository-encoding.py")
spec = importlib.util.spec_from_file_location("check_repository_encoding", SCRIPT)
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)


class RepositoryEncodingTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def add(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content if isinstance(content, bytes) else content.encode("utf-8"))
        return Path(name)

    def test_valid_unicode_and_escaped_sequences_are_allowed(self):
        path = self.add("src/example.cs", r'var text = "\uFFFD"; // Café')

        checked, findings = checker.check_files(self.root, [path])

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_all_legacy_corruption_patterns_report_their_lines(self):
        examples = ("\ufffd", "\u00e2\u20ac", "\u00c3\u00a9", "\u00c2\u00b0", "\u0393\u00c7")
        for example in examples:
            with self.subTest(example=example.encode("unicode_escape")):
                path = self.add("docs/example.md", "valid\n" + example + "\n")

                checked, findings = checker.check_files(self.root, [path])

                self.assertEqual(1, checked)
                self.assertEqual([(path, 2)], [(finding.path, finding.line) for finding in findings])

    def test_invalid_utf8_is_reported_without_decoding_or_echoing_contents(self):
        path = self.add("tests/example.cs", b"valid\ninvalid\xff\n")

        checked, findings = checker.check_files(self.root, [path])

        self.assertEqual(1, checked)
        self.assertEqual((path, 2, "Not valid UTF-8."), tuple(findings[0]))

    def test_untracked_source_files_and_root_text_are_checked_but_generated_dirs_are_skipped(self):
        root = self.add("README.md", "valid")
        source = self.add("src/example.cs", "valid")
        self.add("src/bin/generated.cs", "\ufffd")
        self.add("docs/COVERAGE/generated.md", "\ufffd")
        self.add("src/image.png", "\ufffd")

        paths = checker.candidate_files(self.root, [])
        checked, findings = checker.check_files(self.root, paths)

        self.assertEqual([root, source], paths)
        self.assertEqual(2, checked)
        self.assertEqual([], findings)

    def test_tracked_workflow_files_outside_legacy_roots_are_checked(self):
        workflow = self.add(".github/workflows/build.yml", "\ufffd")

        checked, findings = checker.check_files(self.root, checker.candidate_files(self.root, [workflow.as_posix()]))

        self.assertEqual(1, checked)
        self.assertEqual([workflow], [finding.path for finding in findings])


if __name__ == "__main__":
    unittest.main()
