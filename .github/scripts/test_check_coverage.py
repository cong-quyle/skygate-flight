"""Test cho check_coverage.py bằng OpenCover XML giả. Chạy: python -I -m unittest discover -s .github/scripts"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_coverage as cc  # noqa: E402


def module_xml(name, points, file_uid="1", path="C:/p/A.cs"):
    """points: list of (start_line, end_line, visit_count)."""
    sp = "".join(
        '<SequencePoint vc="%d" sl="%d" el="%d" sc="1" ec="2" fileid="%s"/>' % (vc, sl, el, file_uid)
        for sl, el, vc in points
    )
    return (
        "<Module><ModuleName>%s</ModuleName><Files><File uid=\"%s\" fullPath=\"%s\"/></Files>"
        "<Classes><Class><Methods><Method><FileRef uid=\"%s\"/><SequencePoints>%s</SequencePoints></Method></Methods></Class></Classes>"
        "</Module>" % (name, file_uid, path, file_uid, sp)
    )


def session(*modules):
    return '<?xml version="1.0"?><CoverageSession><Modules>%s</Modules></CoverageSession>' % "".join(modules)


class CheckCoverageTests(unittest.TestCase):
    def run_main(self, files, *args):
        with tempfile.TemporaryDirectory() as d:
            for name, text in files.items():
                os.makedirs(os.path.dirname(os.path.join(d, name)) or d, exist_ok=True)
                with open(os.path.join(d, name), "w", encoding="utf-8") as fh:
                    fh.write(text)
            out, err = io.StringIO(), io.StringIO()
            with redirect_stdout(out), redirect_stderr(err):
                code = cc.main([d, *args])
            return code, out.getvalue(), err.getvalue()

    def test_passes_when_at_or_above_threshold(self):
        pts = [(i, i, 1) for i in range(1, 20)] + [(20, 20, 0)]       # 19/20 = 95%
        code, out, _ = self.run_main({"r.xml": session(module_xml("SkyGate.Core", pts))}, "--assembly", "SkyGate.Core", "--min", "95")
        self.assertEqual(code, 0, out)
        self.assertIn("95.00%", out)

    def test_fails_below_threshold_and_lists_uncovered_lines(self):
        pts = [(1, 1, 1), (2, 2, 0), (3, 3, 0)]
        code, out, err = self.run_main({"r.xml": session(module_xml("SkyGate.Core", pts))}, "--assembly", "SkyGate.Core", "--min", "95")
        self.assertEqual(code, 1)
        self.assertIn("A.cs:2-3", out)
        self.assertIn("KHÔNG ĐẠT", out + err)

    def test_missing_assembly_fails_instead_of_passing_silently(self):
        code, out, _ = self.run_main({"r.xml": session(module_xml("Other", [(1, 1, 1)]))}, "--assembly", "SkyGate.Core")
        self.assertEqual(code, 1)
        self.assertIn("KHÔNG CÓ DỮ LIỆU", out)

    def test_no_opencover_files_is_an_error(self):
        code, _out, err = self.run_main({"notes.xml": "<a/>"}, "--assembly", "SkyGate.Core")
        self.assertEqual(code, 2)
        self.assertIn("không tìm thấy", err)

    def test_merges_runs_a_line_counts_if_any_run_covered_it(self):
        edit = session(module_xml("SkyGate.Core", [(1, 1, 1), (2, 2, 0)]))
        play = session(module_xml("SkyGate.Core", [(1, 1, 0), (2, 2, 3)]))
        code, out, _ = self.run_main({"EditMode/a.xml": edit, "PlayMode/b.xml": play}, "--assembly", "SkyGate.Core", "--min", "100")
        self.assertEqual(code, 0, out)
        self.assertIn("100.00%", out)

    def test_report_only_assembly_never_fails(self):
        xml = session(module_xml("SkyGate.Core", [(1, 1, 1)]), module_xml("SkyGate.Rendering", [(1, 1, 0)], path="C:/p/B.cs"))
        code, out, _ = self.run_main({"r.xml": xml}, "--assembly", "SkyGate.Core", "--report", "SkyGate.Rendering")
        self.assertEqual(code, 0, out)
        self.assertIn("SkyGate.Rendering", out)
        self.assertIn("0.00%", out)

    def test_multiline_sequence_points_count_every_line(self):
        code, out, _ = self.run_main({"r.xml": session(module_xml("SkyGate.Core", [(1, 4, 1)]))}, "--assembly", "SkyGate.Core")
        self.assertEqual(code, 0)
        self.assertIn("100.00%", out)   # 4/4 dòng

    def test_assembly_name_match_ignores_case(self):
        code, _out, _ = self.run_main({"r.xml": session(module_xml("SkyGate.Core", [(1, 1, 1)]))}, "--assembly", "skygate.core")
        self.assertEqual(code, 0)


if __name__ == "__main__":
    unittest.main()
