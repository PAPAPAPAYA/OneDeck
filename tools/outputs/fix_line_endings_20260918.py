# -*- coding: utf-8 -*-
import io

def report(path):
    data = open(path, "rb").read()
    crlf = data.count(b"\r\n")
    lf_total = data.count(b"\n")
    return crlf, lf_total, lf_total - crlf

for p in ["plans/plan-popup-peak-live-follow-2026-09-18.md",
          "docs/RegressionChecklist.md"]:
    crlf, lf_total, lone = report(p)
    print(p, "-> crlf:", crlf, "total_lf:", lf_total, "lone_lf:", lone)
    if lone > 0:
        data = open(p, "rb").read().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
        with open(p, "wb") as f:
            f.write(data)
        crlf2, lf2, lone2 = report(p)
        print("   normalized -> crlf:", crlf2, "lone_lf:", lone2)
