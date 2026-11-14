#!/usr/bin/env python3
"""Cổng coverage cho Unity Code Coverage (định dạng OpenCover).

Đọc mọi file OpenCover XML trong một thư mục (vd. CodeCoverage/**/TestCoverageResults_*.xml), gộp kết quả
(một dòng được coi là phủ nếu BẤT KỲ file nào ghi nhận nó được chạy) rồi tính % dòng phủ theo assembly.
Thoát với mã 1 nếu một assembly bắt buộc thấp hơn ngưỡng, hoặc không có dữ liệu cho nó (tránh "xanh giả"
khi cấu hình assemblyFilters sai).

Ví dụ:
    python check_coverage.py CodeCoverage --assembly SkyGate.Core --min 95
    python check_coverage.py CodeCoverage --assembly SkyGate.Core --min 95 --report SkyGate.Rendering
"""
import argparse
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

MAX_RANGE = 2000  # chặn dải dòng bất thường


def find_opencover_files(root):
    found = []
    for dirpath, _dirs, files in os.walk(root):
        for name in files:
            if not name.lower().endswith(".xml"):
                continue
            path = os.path.join(dirpath, name)
            try:
                with open(path, "rb") as fh:
                    head = fh.read(4096)
            except OSError:
                continue
            if b"<CoverageSession" in head:
                found.append(path)
    return sorted(found)


def parse_file(path, lines_by_module):
    """Ghi vào lines_by_module[module][(file, line)] = visited(bool)."""
    ctx = ET.iterparse(path, events=("end",))
    for _event, elem in ctx:
        if elem.tag != "Module":
            continue
        name_el = elem.find("ModuleName")
        module = (name_el.text or "").strip() if name_el is not None else ""
        files = {}
        files_el = elem.find("Files")
        if files_el is not None:
            for f in files_el.findall("File"):
                files[f.get("uid")] = (f.get("fullPath") or f.get("uid") or "?").replace("\\", "/")
        bucket = lines_by_module[module]
        for method in elem.iter("Method"):
            ref = method.find("FileRef")
            default_uid = ref.get("uid") if ref is not None else None
            for sp in method.iter("SequencePoint"):
                uid = sp.get("fileid") or default_uid
                fname = files.get(uid, uid or "?")
                try:
                    sl = int(sp.get("sl"))
                    el = int(sp.get("el", sl))
                    vc = int(sp.get("vc", "0"))
                except (TypeError, ValueError):
                    continue
                if el < sl or el - sl > MAX_RANGE:
                    el = sl
                for line in range(sl, el + 1):
                    key = (fname, line)
                    bucket[key] = bucket.get(key, False) or vc > 0
        elem.clear()


def collect(paths):
    lines_by_module = defaultdict(dict)
    for p in paths:
        parse_file(p, lines_by_module)
    return lines_by_module


def percent(covered, total):
    return 100.0 * covered / total if total else 0.0


def module_stats(lines):
    total = len(lines)
    covered = sum(1 for v in lines.values() if v)
    return covered, total


def uncovered_ranges(lines, limit=25):
    by_file = defaultdict(list)
    for (f, line), visited in lines.items():
        if not visited:
            by_file[f].append(line)
    out = []
    for f, ls in sorted(by_file.items()):
        ls.sort()
        start = prev = ls[0]
        for n in ls[1:] + [None]:
            if n is not None and n == prev + 1:
                prev = n
                continue
            out.append("%s:%s" % (os.path.basename(f), start if start == prev else "%d-%d" % (start, prev)))
            if n is not None:
                start = prev = n
    return out[:limit], max(0, len(out) - limit)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("results_dir", help="thư mục chứa kết quả coverage (tìm đệ quy)")
    ap.add_argument("--assembly", action="append", default=[], help="assembly bị chặn theo ngưỡng (lặp lại được)")
    ap.add_argument("--min", type=float, default=95.0, help="ngưỡng %% dòng phủ cho các --assembly (mặc định 95)")
    ap.add_argument("--report", action="append", default=[], help="assembly chỉ báo cáo, không chặn")
    args = ap.parse_args(argv)

    files = find_opencover_files(args.results_dir)
    if not files:
        print("LỖI: không tìm thấy file OpenCover (*.xml có <CoverageSession>) trong %r" % args.results_dir, file=sys.stderr)
        return 2
    lines_by_module = collect(files)
    known = {m.lower(): m for m in lines_by_module}

    rows, failed = [], False
    for kind, names in (("gate", args.assembly), ("report", args.report)):
        for name in names:
            real = known.get(name.lower())
            if real is None:
                rows.append((name, kind, None, None, None, "KHÔNG CÓ DỮ LIỆU"))
                if kind == "gate":
                    failed = True
                continue
            covered, total = module_stats(lines_by_module[real])
            pct = percent(covered, total)
            if total == 0:
                status = "KHÔNG CÓ DÒNG NÀO"
                failed = failed or kind == "gate"
            elif kind == "gate":
                status = "ĐẠT" if pct >= args.min else "KHÔNG ĐẠT (< %.1f%%)" % args.min
                failed = failed or pct < args.min
            else:
                status = "chỉ báo cáo"
            rows.append((real, kind, covered, total, pct, status))

    print("Coverage từ %d file: %s" % (len(files), ", ".join(os.path.basename(f) for f in files)))
    print("%-28s %-7s %9s %9s %8s  %s" % ("Assembly", "Loại", "Phủ", "Tổng", "%", "Kết quả"))
    for name, kind, cov, tot, pct, status in rows:
        if pct is None:
            print("%-28s %-7s %9s %9s %8s  %s" % (name, kind, "-", "-", "-", status))
        else:
            print("%-28s %-7s %9d %9d %7.2f%%  %s" % (name, kind, cov, tot, pct, status))
    for name, kind, _c, _t, pct, _s in rows:
        if kind == "gate" and pct is not None and pct < args.min:
            shown, more = uncovered_ranges(lines_by_module[known[name.lower()]])
            print("\nDòng chưa được test trong %s:" % name)
            print("  " + ", ".join(shown) + (" … và %d dải khác" % more if more else ""))

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as fh:
            fh.write("### Code coverage (ngưỡng %.0f%% cho %s)\n\n" % (args.min, ", ".join(args.assembly) or "—"))
            fh.write("| Assembly | Loại | Phủ / Tổng | % | Kết quả |\n|---|---|---|---|---|\n")
            for name, kind, cov, tot, pct, status in rows:
                cells = ("-", "-") if pct is None else ("%d / %d" % (cov, tot), "%.2f" % pct)
                fh.write("| %s | %s | %s | %s | %s |\n" % (name, kind, cells[0], cells[1], status))
            fh.write("\n")

    if failed:
        print("\nCOVERAGE KHÔNG ĐẠT.", file=sys.stderr)
        return 1
    print("\nCoverage đạt ngưỡng." if args.assembly else "\nChỉ báo cáo.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
