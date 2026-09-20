# OneDeck API (async PvP backend)

Express + better-sqlite3 backend for ghost-deck async PvP and balance stats.
Single-file server (`server.js`), same deployment model as pkidle.

## Layout on server

```
/var/www/onedeck/
├── server/     # this folder (code)
└── data/       # SQLite db + admin_token.txt + pm2 logs (server-side only, never commit)
```

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/players/register` | `{username}` -> `{playerId}`; 409 on name conflict |
| POST | `/api/decks` | upload ghost deck snapshot; a deck whose content fingerprint is already flagged lands flagged |
| GET | `/api/decks/opponents?playerId&gameVersion&maxSession&perSession[&includeSelf=1]` | batch opponent decks; flagged decks are never returned, decks CONTAINING an active combo are withheld too (multiset containment), `flaggedDeckIds` lists the flagged ids inside the requested range and `blockedCombos` carries the active combo card sets, both so the client can drop cached copies; `includeSelf=1` (client test toggle) drops the self-exclusion so the requester's own decks can come back |
| POST | `/api/matches/report` | battle result, idempotent by `reportId` |
| POST | `/api/loop-reports` | infinity-loop evidence against a ghost deck: `{playerId, gameVersion, opponentDeckId, verdict, seed, signals, payload}`. Verdict `EnemyDeck` (= a headless re-run proved the ghost loops by itself) flags the deck's CONTENT fingerprint and — when the payload carries a proven minimum (`oneMinimal` and not `truncated`/`multiSeedStable: false`) — registers the minimized card set in the combo library (response: `comboKey`, `comboStatus`). Every other verdict is stored as evidence and changes no state. Idempotent per `(deck, fingerprint, seed, verdict, reporter)` |
| POST | `/api/stats/snapshot` | lifetime cumulative shop/winrate stats (upsert, retry-safe) |
| POST | `/api/runs` | one full run record with shop visits + combats, idempotent by `runId`; zero-combat runs are skipped (responds ok, stores nothing) |
| POST | `/api/cards/catalog` | card metadata per game version (upsert) |
| GET | `/api/health` | liveness |
| GET | `/admin?token=...` | HTML dashboard (includes the infinity gate section) |
| POST | `/admin/decks/unflag?token=...&deckId=..` | lift the flag on a single deck row |
| POST | `/admin/decks/unflag?token=...&fingerprint=..` | clear a content key: unflags every row carrying it AND deregisters it, so later uploads are not auto-flagged |
| POST | `/admin/combos/status?token=...` | body `comboKey` + `status` (`retired` / `active`) + optional `reason`: retire a combo (stop withholding decks that contain it — use it when the cards were patched) or bring it back |

## Infinity gate (plan §20 / §21)

A deck that loops forever by itself must never be served, at two granularities:

- **Deck content (P3).** The flag keys on the deck's card MULTISET fingerprint — not on `deck_id`,
  because every snapshot upload inserts a new row and a row-level flag would be evaded by the next
  upload. One confirmed report flags; `loop_reports` keeps the reporter and the payload for the
  audit trail.
- **Combo library (P4).** A report whose evidence carries a PROVEN minimum (1-minimal, multi-seed
  stable, untruncated) also registers that card set, and from then on any deck CONTAINING the set
  is withheld — an offender cannot dodge the gate by padding their deck. Admin can retire a combo
  after the cards are patched, or reactivate it.

Deployment: existing dbs migrate on boot (`ensureColumn` + a fingerprint backfill for rows that
predate the column), so no manual migration step.

## Tests

```bash
npm test        # node --test, throwaway DATA_DIR (tests/loopReports.test.js, tests/combos.test.js)
```

## Deploy

```bash
cd /var/www/onedeck/server
npm install --omit=dev
pm2 start ecosystem.config.js
pm2 save
pm2 startup          # then run the printed command
sudo cp nginx/onedeck.conf /etc/nginx/conf.d/onedeck.conf
sudo rm /etc/nginx/sites-enabled/default   # stock placeholder site
sudo nginx -t && sudo systemctl reload nginx
```

## Update deploy (code-only change, e.g. the P3/P4 infinity gate)

**Transport is the Alibaba Cloud Workbench CLI, not SSH** — this project's ECS ops have gone
through it since the backend first shipped (`无公网 IP 运维走 workbench-cli skill`, see
`tools/outputs/dump_catalog.py` for the same pattern). One-off invocations are stateless, so
chain everything that shares context with `&&` in a single call.

```
INSTANCE=i-uf66n1ofpudgn9b6rg7o      # onedeck box
REMOTE=/var/www/onedeck/server        # code
DB=/var/www/onedeck/data/onedeck.db   # db (DATA_DIR default resolves here)
```

```bash
# 0) one-time: install the CLI and configure credentials
curl -fsSL https://workbench-cli.oss-cn-hangzhou.aliyuncs.com/install.sh | bash   # Windows: install.ps1
#   ~/.workbench/config.json (chmod 600) — mode AK | RamRoleArn | CredentialsCmd | CredentialsURI
#   Windows: the installer refuses (MINGW/MSYS); download workbench-windows-amd64.zip, verify it
#   against the published checksums.sha256, and put workbench.exe on PATH.

# ⚠ Git Bash rewrites a REMOTE path argument (/var/www/...) into D:/Program Files/Git/var/www/...,
#   which fails with InvalidParameter.Path. Set MSYS_NO_PATHCONV=1 for every upload/download.
export MSYS_NO_PATHCONV=1

# ⚠ `download`'s LOCAL path must be Windows-shaped: a Windows CLI reads /tmp/x.js as D:\tmp\x.js
#   (and creates D:\tmp). Use e.g. C:/Users/<you>/AppData/Local/Temp/x.js.

# 1) back the live db up ONLINE first — WAL-safe: a plain `cp onedeck.db` can silently miss
#    pages still in onedeck.db-wal. (This is a new file; nothing is overwritten.)
workbench upload server/onedeck-api/scripts/backup-db.js "$REMOTE/scripts/backup-db.js" --instance-id $INSTANCE
workbench exec --instance-id $INSTANCE --output json --timeout 60 \
  --command "cd $REMOTE && node scripts/backup-db.js"

# 2) push the server files. `upload` refuses to overwrite without confirmation, so the
#    server.js replacement is written through the remote shell instead (same file, one command).
workbench exec --instance-id $INSTANCE --output json --timeout 60 \
  --command "cd $REMOTE && cp server.js server.js.prev-$(date +%Y%m%d-%H%M%S) && echo backed-up"
workbench upload server/onedeck-api/server.js "$REMOTE/server.js.new" --instance-id $INSTANCE
workbench exec --instance-id $INSTANCE --output json \
  --command "cd $REMOTE && node --check server.js.new 2>/dev/null || cp server.js.new /tmp/c.js && node --check /tmp/c.js && rm -f /tmp/c.js"
workbench exec --instance-id $INSTANCE --output json \
  --command "cd $REMOTE && mv server.js.new server.js && pm2 restart onedeck-api"
#   `npm install --omit=dev` belongs here ONLY when the dependency block changed — otherwise it
#   just churns node_modules on a live box. Check with: git diff <deployed-commit>..HEAD -- package.json
#   Note: `node --check` refuses the .new extension, hence the copy-to-.js dance above.

# 3) verify from the dev machine: health, and that the NEW route exists.
#    401 unknown_player = deployed; 404 not_found = still the old code.
curl -s http://8.153.150.197/api/health
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://8.153.150.197/api/loop-reports \
  -H 'Content-Type: application/json' -d '{}'

# 4) verify the migration applied, then read the log tail
workbench exec --instance-id $INSTANCE --output json --command "cd $REMOTE && node scripts/inspect-db.js"
workbench exec --instance-id $INSTANCE --output json --command "pm2 logs onedeck-api --lines 30 --nostream"
```

`inspect-db.js` is read-only and prints `MISSING` for anything the code expects but the db lacks
(the boot migration adds columns and tables; the fingerprint backfill reports its row count in the
log). Both scripts resolve the db exactly like `server.js`, so no `DATA_DIR` is needed on the box.

`pm2 restart` is a service restart — per the workbench skill's own rule, announce it before running
it. **Rollback**: put `server.js.prev-<stamp>` back + `pm2 restart onedeck-api`; the db backup is
insurance for the unexpected, not a required rollback step (the migration is additive — ADD COLUMN
+ new tables — so an older `server.js` runs fine against a migrated db).

## Ops notes

- Admin token lives at `data/admin_token.txt` (auto-generated on first boot,
  or set env `ADMIN_TOKEN`).
- Logs: `pm2 logs onedeck-api`.
- DB: `data/onedeck.db` (SQLite, WAL mode).
- Idempotency: decks/cards append freely; match reports dedupe by `reportId`;
  runs dedupe by `runId`; stats snapshots upsert by
  `(playerId, kind, gameVersion, cardTypeID, sessionNum)` so retries never double-count.
- `trust proxy = true`: the app trusts X-Forwarded-For for rate-limit keys. This is
  safe only because it binds 127.0.0.1 behind nginx — never expose port 3000 directly.
- Local run: `npm install --omit=dev`, then `DATA_DIR=data node server.js`
  (PowerShell: `$env:DATA_DIR="data"; node server.js`) — binds 127.0.0.1:3000, db in
  `server/onedeck-api/data/` (gitignored). The default DATA_DIR resolves one level up
  and is meant for the ECS layout only.
