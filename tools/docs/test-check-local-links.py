"""Tests the tracked Markdown link and heading-fragment check without network access."""

import importlib.util
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("check-local-links.py")
spec = importlib.util.spec_from_file_location("check_local_links", SCRIPT)
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)


class LocalLinksTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / "docs" / "images").mkdir(parents=True)
        self.files = []

    def add(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        self.files.append(name)

    def check(self):
        return checker.check_links(self.root, self.files)

    def test_repository_root_directory_and_its_readme_heading(self):
        self.add("README.md", "# Home\n")
        self.add("docs/guide/index.md", "[Root](../../) [Heading](../../#home)\n")

        checked, findings = self.check()

        self.assertEqual(2, checked)
        self.assertEqual([], findings)

    def test_relative_links_images_references_and_duplicate_headings(self):
        self.add("docs/guide.md", "# Getting Started\n## A call: `foo()`\n## Repeat\n## Repeat\n<a id=\"custom-anchor\"></a>\n")
        self.add("docs/images/flow.png", "image")
        self.add("docs/index.md", """[Guide](guide.md#getting-started) [Code](guide.md#a-call-foo)
[Second](guide.md#repeat-1) [Anchor](guide.md#custom-anchor)
![Flow](images/flow.png) [Encoded](%67uide.md#getting-started)
[Ref][page]
[page]: guide.md#getting-started
[![nested](images/flow.png)](guide.md#repeat)
[External](https://example.com/missing.md#missing) <https://example.com/missing.md>
""")

        checked, findings = self.check()

        self.assertEqual([], findings)
        self.assertEqual(9, checked)

    def test_nested_list_links_and_directory_readme_fragments(self):
        self.add("docs/guide/README.md", "# Start Here\n")
        self.add("docs/index.md", """1. Parent
   - Child
     [Nested](guide/#start-here)
     ```markdown
     [example](missing.md)
     ```
""")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_missing_paths_untracked_targets_and_fragments_are_reported_at_the_link(self):
        self.add("docs/guide.md", "# Present\n")
        (self.root / "docs" / "untracked.md").write_text("# Present\n", encoding="utf-8")
        self.add("docs/index.md", """[Missing](gone.md)
[Fragment](guide.md#absent)
![Diagram](images/not-there.png)
[Untracked](untracked.md)
[Escape](../../outside.md)
""")

        checked, findings = self.check()

        self.assertEqual(5, checked)
        self.assertEqual([1, 2, 3, 4, 5], [finding.line for finding in findings])
        self.assertTrue(all(finding.path == "docs/index.md" for finding in findings))
        self.assertIn("absent", findings[1].message)
        self.assertIn("outside", findings[-1].message)

    def test_case_sensitive_tracked_paths_and_encoded_fragments(self):
        self.add("docs/Guide.md", "# Café & Tea\n")
        self.add("docs/index.md", "[Valid](Guide.md#caf%C3%A9--tea)\n[Wrong case](guide.md#caf%C3%A9--tea)\n")

        checked, findings = self.check()

        self.assertEqual(2, checked)
        self.assertEqual([2], [finding.line for finding in findings])
        self.assertIn("missing or untracked", findings[0].message)

    def test_github_heading_punctuation_and_task_checkboxes(self):
        self.add("docs/guide.md", "## Consumer rules — `KAL2xxx`\n")
        self.add("docs/index.md", "[Rules](guide.md#consumer-rules--kal2xxx)\n- [x] `Done` (`one`, `two`)\n")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_fences_inline_code_and_comments_do_not_contain_links(self):
        self.add("docs/guide.md", """# Alpha
[valid](#alpha) `[not a link](missing.md)`
<!-- [hidden](missing.md) -->
```markdown
[example](missing.md)
```
    [indented](missing.md)
""")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_explicit_ids_do_not_change_duplicate_heading_suffixes(self):
        self.add("docs/guide.md", "<a id=\"repeat\"></a>\n## Repeat\n## Repeat\n")
        self.add("docs/index.md", "[Second](guide.md#repeat-1)\n[Not a third](guide.md#repeat-2)\n")

        checked, findings = self.check()

        self.assertEqual(2, checked)
        self.assertEqual([2], [finding.line for finding in findings])

    def test_inline_code_is_not_an_explicit_html_anchor(self):
        self.add("docs/guide.md", "`<a id=\"example\"></a>`\n# Real\n")
        self.add("docs/index.md", "[Missing](guide.md#example)\n[Present](guide.md#real)\n")

        checked, findings = self.check()

        self.assertEqual(2, checked)
        self.assertEqual([1], [finding.line for finding in findings])

    def test_unterminated_html_comment_inside_fence_cannot_hide_later_links(self):
        self.add("docs/guide.md", "# Visible\n")
        self.add("docs/index.md", "```html\n<!-- example only\n```\n[Real](guide.md#visible)\n")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_setext_separator_after_atx_heading_is_not_a_second_heading(self):
        self.add("docs/guide.md", "# Title\n---\n")
        self.add("docs/index.md", "[Phantom](guide.md#title-1)\n")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([1], [finding.line for finding in findings])

    def test_repo_root_absolute_urls_are_not_repository_links(self):
        self.add("docs/guide.md", "# Guide\n")
        self.add("docs/index.md", "[Wrong on GitHub](/docs/guide.md#guide)\n")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([1], [finding.line for finding in findings])
        self.assertIn("Root-relative", findings[0].message)

    def test_blockquoted_headings_fences_and_reference_definitions(self):
        self.add("docs/guide.md", "> ### In a quote\n")
        self.add("docs/index.md", """[Quoted][page]
> [page]: guide.md#in-a-quote
> ```markdown
> [example](missing.md)
> ```
""")

        checked, findings = self.check()

        self.assertEqual(1, checked)
        self.assertEqual([], findings)

    def test_reference_and_html_links_and_setext_headings(self):
        self.add("docs/guide.md", "Title with punctuation!\n=======================\n")
        self.add("docs/index.md", """[short][] [short] <a href=\"guide.md#title-with-punctuation\">read</a>
[short]: guide.md#title-with-punctuation
<img src=\"images/absent.png\">
""")

        checked, findings = self.check()

        self.assertEqual(4, checked)
        self.assertEqual([3], [finding.line for finding in findings])
        self.assertIn("absent.png", findings[0].message)


if __name__ == "__main__":
    unittest.main()
