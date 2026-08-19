"""Create the worldview naming doc as a Notion page under the OD page.
Reads docs/OneDeck_Worldview_Naming_v1_NewWeirdCult.md, converts markdown to
Notion blocks (headings / quotes / lists / tables), creates the page and
appends children in batches. Token via env NOTION_TOKEN."""

import json
import os
import re
import sys
import urllib.error
import urllib.request

TOKEN = os.environ.get("NOTION_TOKEN", "")
DOC = r"D:/Unity Projects/OneDeck/docs/OneDeck_Worldview_Naming_v1_NewWeirdCult.md"
PARENT = "333827b8-c3c1-80fa-9bae-eb547a15270d"  # OD page
TITLE = "OneDeck 世界观迭代 v1 —— 新怪谈·都市怪谈·邪教（命名总案）"
BASE = "https://api.notion.com/v1"


def api(method, url, body=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Authorization", "Bearer " + TOKEN)
    req.add_header("Notion-Version", "2022-06-28")
    req.add_header("Content-Type", "application/json")
    data = json.dumps(body).encode("utf-8") if body is not None else None
    with urllib.request.urlopen(req, data) as resp:
        return json.loads(resp.read().decode("utf-8"))


try:
    me = api("GET", BASE + "/users/me")
    print("integration:", me.get("name") or me.get("id"))
except urllib.error.HTTPError as e:
    print("AUTH FAILED:", e.code, e.read().decode("utf-8")[:500])
    sys.exit(1)

parent = api("GET", BASE + "/pages/" + PARENT)
print("parent ok:", parent.get("url"))


def inline(text):
    out = []
    pos = 0
    pat = re.compile(r"(\*\*[^*]+\*\*|`[^`]+`)")
    for m in pat.finditer(text):
        if m.start() > pos:
            out.append({"type": "text", "text": {"content": text[pos:m.start()]}})
        tok = m.group(0)
        if tok.startswith("**"):
            out.append({"type": "text", "text": {"content": tok[2:-2]},
                        "annotations": {"bold": True}})
        else:
            out.append({"type": "text", "text": {"content": tok[1:-1]},
                        "annotations": {"code": True}})
        pos = m.end()
    if pos < len(text):
        out.append({"type": "text", "text": {"content": text[pos:]}})
    return out


def cells(row):
    r = row.strip()
    if r.startswith("|"):
        r = r[1:]
    if r.endswith("|"):
        r = r[:-1]
    return [c.strip() for c in r.split("|")]


lines = open(DOC, encoding="utf-8").read().splitlines()
blocks = []
i = 0
while i < len(lines):
    line = lines[i].rstrip()
    if not line.strip():
        i += 1
        continue
    if line.startswith("|"):
        rows = []
        while i < len(lines) and lines[i].strip().startswith("|"):
            rows.append(lines[i].strip())
            i += 1
        has_header = len(rows) > 1 and all(
            re.fullmatch(r":?-{2,}:?", c) for c in cells(rows[1]))
        width = len(cells(rows[0]))
        data = rows[2:] if has_header else rows[1:]
        trs = []
        for r in data:
            cs = cells(r)
            if len(cs) < width:
                cs += [""] * (width - len(cs))
            elif len(cs) > width:
                cs = cs[:width]
            trs.append({"object": "block", "type": "table_row",
                        "table_row": {"cells": [inline(c) for c in cs]}})
        blocks.append({"object": "block", "type": "table",
                       "table": {"table_width": width,
                                 "has_column_header": has_header,
                                 "children": trs}})
        continue
    if line.startswith("### "):
        blocks.append({"object": "block", "type": "heading_3",
                       "heading_3": {"rich_text": inline(line[4:])}})
    elif line.startswith("## "):
        blocks.append({"object": "block", "type": "heading_2",
                       "heading_2": {"rich_text": inline(line[3:])}})
    elif line.startswith("# "):
        blocks.append({"object": "block", "type": "heading_1",
                       "heading_1": {"rich_text": inline(line[2:])}})
    elif line.startswith("> "):
        blocks.append({"object": "block", "type": "quote",
                       "quote": {"rich_text": inline(line[2:])}})
    elif line == "---":
        blocks.append({"object": "block", "type": "divider", "divider": {}})
    elif re.match(r"^\d+\.\s", line):
        blocks.append({"object": "block", "type": "numbered_list_item",
                       "numbered_list_item": {
                           "rich_text": inline(re.sub(r"^\d+\.\s", "", line))}})
    elif line.startswith("- "):
        blocks.append({"object": "block", "type": "bulleted_list_item",
                       "bulleted_list_item": {"rich_text": inline(line[2:])}})
    else:
        blocks.append({"object": "block", "type": "paragraph",
                       "paragraph": {"rich_text": inline(line)}})
    i += 1

print("parsed blocks:", len(blocks))


def bcount(b):
    return 1 + len(b["table"]["children"]) if b["type"] == "table" else 1


batches, cur, cnt = [], [], 0
for b in blocks:
    bc = bcount(b)
    if cur and cnt + bc > 90:
        batches.append(cur)
        cur, cnt = [], 0
    cur.append(b)
    cnt += bc
if cur:
    batches.append(cur)
print("batches:", [len(b) for b in batches])

page = api("POST", BASE + "/pages", {
    "parent": {"page_id": PARENT},
    "properties": {"title": {"title": [{"type": "text",
                                        "text": {"content": TITLE}}]}},
})
pid = page["id"]
print("created:", page["url"])

for idx, batch in enumerate(batches):
    api("PATCH", BASE + "/blocks/" + pid + "/children", {"children": batch})
    print("batch", idx + 1, "ok")

total, cursor = 0, None
while True:
    url = BASE + "/blocks/" + pid + "/children?page_size=100"
    if cursor:
        url += "&start_cursor=" + cursor
    data = api("GET", url)
    total += len(data.get("results", []))
    if data.get("has_more"):
        cursor = data["next_cursor"]
    else:
        break
print("total blocks on page:", total)
