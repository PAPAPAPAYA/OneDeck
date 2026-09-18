# -*- coding: utf-8 -*-
"""Dump the server card_catalog table into TSV files for check_consistency.py.

Local mode (default): reads server/onedeck-api/data/onedeck.db read-only.
Prod mode (--prod): runs a read-only SELECT on the ECS instance via
`workbench exec` (alibabacloud-workbench-cli skill). The remote side uses the
server's own better-sqlite3 module, so no sqlite3 CLI is needed there.

Outputs (tools/outputs/):
	catalog_current.tsv     rows of the best game_version (mirrors loadCatalogMap
	                        in server.js: version with the most rows wins)
	catalog_versions.tsv    row counts per game_version (version health)

TSV row format mirrors the legacy _prod_catalog.tsv:
	CAT<TAB>card_type_id<TAB>name<TAB>rarity<TAB>cost<TAB>tags_json<TAB>updated_at

Usage:
	python dump_catalog.py            # local dev DB
	python dump_catalog.py --prod     # live ECS DB (read-only SELECT)
"""
import json
import os
import subprocess
import sys

INSTANCE_ID = "i-uf66n1ofpudgn9b6rg7o"
REMOTE_DIR = "/var/www/onedeck/server"
# server.js: DATA_DIR = env or path.join(__dirname, '..', 'data')
REMOTE_DB = "/var/www/onedeck/data/onedeck.db"
# Local launcher layouts vary; try both known spots.
LOCAL_DBS = [
	os.path.join("server", "onedeck-api", "data", "onedeck.db"),
	os.path.join("server", "data", "onedeck.db"),
]
OUT_DIR = os.path.join("tools", "outputs")
OUT_TSV = os.path.join(OUT_DIR, "catalog_current.tsv")
OUT_VER = os.path.join(OUT_DIR, "catalog_versions.tsv")

# Single-quoted JS only: the whole snippet travels inside one double-quoted
# argument of the remote shell command, so double quotes would need escaping.
REMOTE_JS = (
	"const db=require('better-sqlite3')('" + REMOTE_DB + "',{readonly:true});"
	"const vers=db.prepare('SELECT game_version g, COUNT(*) c FROM card_catalog"
	" GROUP BY game_version ORDER BY c DESC').all();"
	"for(const v of vers){console.log('VER\\t'+v.g+'\\t'+v.c);}"
	"const best=vers.length?vers[0].g:'';"
	"const rows=best?db.prepare('SELECT card_type_id, name, rarity, cost, tags,"
	" updated_at FROM card_catalog WHERE game_version=? ORDER BY card_type_id').all(best):[];"
	"for(const r of rows){console.log(['CAT',r.card_type_id,r.name,r.rarity,r.cost,"
	"r.tags,r.updated_at].join('\\t'));}"
)


def write_outputs(lines):
	rows = [l.split("\t") for l in lines if l.startswith("CAT\t")]
	vers = [l.split("\t") for l in lines if l.startswith("VER\t")]
	os.makedirs(OUT_DIR, exist_ok=True)
	with open(OUT_TSV, "w", encoding="utf-8", newline="") as fp:
		fp.write("\n".join(lines_lt(r) for r in rows) + ("\n" if rows else ""))
	with open(OUT_VER, "w", encoding="utf-8", newline="") as fp:
		for v in vers:
			fp.write("%s\t%s\n" % (v[1], v[2]))
	print("wrote %d catalog rows, %d game_versions -> %s" % (len(rows), len(vers), OUT_TSV))
	return len(rows)


def lines_lt(r):
	# Normalize to the 7-column legacy layout.
	return "\t".join((r + [""] * 7)[:7])


def dump_local():
	import sqlite3
	local_db = next((p for p in LOCAL_DBS if os.path.exists(p)), None)
	if not local_db:
		print("local DB not found, tried:\n  " + "\n  ".join(LOCAL_DBS))
		return 1
	uri = "file:%s?mode=ro" % os.path.abspath(local_db).replace("\\", "/")
	conn = sqlite3.connect(uri, uri=True)
	try:
		vers = conn.execute(
			"SELECT game_version, COUNT(*) FROM card_catalog GROUP BY game_version ORDER BY 2 DESC"
		).fetchall()
		best = vers[0][0] if vers else ""
		rows = conn.execute(
			"SELECT card_type_id, name, rarity, cost, tags, updated_at FROM card_catalog"
			" WHERE game_version = ? ORDER BY card_type_id", (best,)
		).fetchall() if best else []
	finally:
		conn.close()
	lines = ["VER\t%s\t%d" % (v[0], v[1]) for v in vers]
	lines += ["CAT\t%s\t%s\t%s\t%s\t%s\t%s" % r for r in rows]
	return write_outputs(lines)


def dump_prod():
	cmd = "cd %s && node -e \"%s\"" % (REMOTE_DIR, REMOTE_JS)
	proc = subprocess.run(
		["workbench", "exec", "--instance-id", INSTANCE_ID, "--output", "json",
		 "--command", cmd, "--timeout", "30"],
		capture_output=True, text=True, encoding="utf-8")
	if proc.returncode != 0:
		print("workbench exec failed (exit %d): %s" % (proc.returncode, proc.stderr.strip() or proc.stdout.strip()))
		return 1
	try:
		result = json.loads(proc.stdout)
	except ValueError:
		print("workbench output is not JSON:\n%s" % proc.stdout[:500])
		return 1
	if result.get("exit_code") != 0:
		print("remote command failed: %s" % (result.get("stderr") or result.get("output") or "")[:500])
		return 1
	lines = [l for l in (result.get("output") or "").splitlines() if l.strip()]
	if not lines:
		print("remote query returned no rows")
		return 1
	return write_outputs(lines)


def main():
	prod = "--prod" in sys.argv
	if prod:
		print("dumping card_catalog from PROD (%s:%s) ..." % (INSTANCE_ID, REMOTE_DIR))
		rc = dump_prod()
	else:
		print("dumping card_catalog from local DB ...")
		rc = dump_local()
	if rc != 0:
		sys.exit(rc)


if __name__ == "__main__":
	main()
