# -*- coding: utf-8 -*-
"""Dump every row of the live `decks` table for the P5 infinity scan (report-only step).

Local mode reads server/onedeck-api/data/onedeck.db read-only; --prod runs the same SELECT on the
ECS instance through `workbench exec` (same pattern as dump_catalog.py — this project's ECS ops go
through the Workbench CLI, not SSH).

Output: tools/outputs/decks_current.json, which Assets/Scripts/Editor/Headless/InfinityBatchScan.cs
reads with JsonUtility. Card lists travel as-is (no interpretation here): resolving them to prefabs
is the Unity side's job.

Usage:
	python tools/outputs/dump_decks.py            # local dev DB
	python tools/outputs/dump_decks.py --prod     # live ECS DB (read-only SELECT)

Exit code: 0 on success, non-zero on failure. It is NOT the deck row count -
any non-zero result means the dump failed and decks_current.json is unchanged.

Note for Windows: run with the repo root as the working directory (paths are relative to it).
"""
import json
import os
import subprocess
import sys

INSTANCE_ID = "i-uf66n1ofpudgn9b6rg7o"
REMOTE_DIR = "/var/www/onedeck/server"
REMOTE_DB = "/var/www/onedeck/data/onedeck.db"
LOCAL_DBS = [
	os.path.join("server", "onedeck-api", "data", "onedeck.db"),
	os.path.join("server", "data", "onedeck.db"),
]
OUT_PATH = os.path.join("tools", "outputs", "decks_current.json")

# Single-quoted JS only: the snippet travels inside one double-quoted shell argument, so double
# quotes would need escaping (dump_catalog.py carries the same note for the same reason).
REMOTE_JS = (
	"const db=require('better-sqlite3')('" + REMOTE_DB + "',{readonly:true});"
	"const cols=new Set(db.prepare('PRAGMA table_info(decks)').all().map(c=>c.name));"
	"const flagCol=cols.has('flag')?'flag':'0 AS flag';"
	"const rows=db.prepare('SELECT deck_id,username,game_version,session_num,hp_max,'+flagCol+',card_type_ids"
	" FROM decks ORDER BY deck_id').all();"
	"const decks=rows.map(r=>({deckId:r.deck_id,username:r.username,gameVersion:r.game_version,"
	"sessionNum:r.session_num,hpMax:r.hp_max,flag:r.flag,cardTypeIDs:JSON.parse(r.card_type_ids)}));"
	"console.log(JSON.stringify({generatedUtc:new Date().toISOString(),source:'ecs " + INSTANCE_ID + "',"
	"count:decks.length,decks:decks}));"
)


def summarize(payload):
	decks = payload.get("decks") or []
	contents = set()
	flagged = 0
	for d in decks:
		ids = d.get("cardTypeIDs") or []
		contents.add("|".join(sorted(ids)))
		if d.get("flag"):
			flagged += 1
	versions = sorted(set(d.get("gameVersion") or "?" for d in decks))
	print("decks: %d rows, %d distinct contents, %d already flagged" % (len(decks), len(contents), flagged))
	print("game versions: %s" % ", ".join(versions))
	# Exit code, not the row count: a non-zero result means the dump failed.
	return 0


def write_output(payload):
	os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
	with open(OUT_PATH, "w", encoding="utf-8", newline="") as fp:
		json.dump(payload, fp, ensure_ascii=False, indent=1)
	print("wrote %s" % OUT_PATH)
	return summarize(payload)


def dump_local():
	import sqlite3
	local_db = next((p for p in LOCAL_DBS if os.path.exists(p)), None)
	if not local_db:
		print("local DB not found, tried:\n  " + "\n  ".join(LOCAL_DBS))
		return 1
	uri = "file:%s?mode=ro" % os.path.abspath(local_db).replace("\\", "/")
	conn = sqlite3.connect(uri, uri=True)
	try:
		cols = set(r[1] for r in conn.execute("PRAGMA table_info(decks)"))
		flag_expr = '"flag"' if "flag" in cols else "0"
		rows = conn.execute(
			"SELECT deck_id, username, game_version, session_num, hp_max, %s, card_type_ids"
			" FROM decks ORDER BY deck_id" % flag_expr
		).fetchall()
	finally:
		conn.close()
	decks = [{
		"deckId": r[0], "username": r[1], "gameVersion": r[2], "sessionNum": r[3],
		"hpMax": r[4], "flag": r[5], "cardTypeIDs": json.loads(r[6]),
	} for r in rows]
	return write_output({"generatedUtc": None, "source": "local:" + local_db.replace("\\", "/"),
	                     "count": len(decks), "decks": decks})


def dump_prod():
	# cd first: `node -e` resolves requires from the CWD, and better-sqlite3 lives in the server
	# folder's node_modules (the default CWD is /root, which fails with MODULE_NOT_FOUND).
	cmd = "cd %s && node -e \"%s\"" % (REMOTE_DIR, REMOTE_JS)
	proc = subprocess.run(
		["workbench", "exec", "--instance-id", INSTANCE_ID, "--output", "json",
		 "--timeout", "60", "--command", cmd],
		capture_output=True, text=True, encoding="utf-8")
	if proc.returncode != 0:
		print("workbench exec failed (exit %d): %s" % (proc.returncode, proc.stderr.strip() or proc.stdout.strip()))
		return 1
	try:
		envelope = json.loads(proc.stdout)
	except ValueError:
		print("workbench output is not JSON:\n%s" % proc.stdout[:500])
		return 1
	if envelope.get("exit_code") != 0:
		print("remote command failed: %s" % (envelope.get("stderr") or envelope.get("output") or "")[:500])
		return 1
	lines = [l for l in (envelope.get("output") or "").splitlines() if l.strip().startswith("{")]
	if not lines:
		print("remote dump returned nothing parsable")
		return 1
	payload = json.loads(lines[-1])
	payload["source"] = "ecs " + INSTANCE_ID
	return write_output(payload)


def main():
	if "--prod" in sys.argv:
		print("dumping decks from PROD (%s) ..." % INSTANCE_ID)
		rc = dump_prod()
	else:
		print("dumping decks from local DB ...")
		rc = dump_local()
	sys.exit(rc)


if __name__ == "__main__":
	main()
