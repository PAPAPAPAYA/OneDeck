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
| GET | `/api/decks/opponents?playerId&gameVersion&maxSession&perSession[&includeSelf=1]` | batch opponent decks; flagged decks are never returned and `flaggedDeckIds` lists the flagged ids inside the requested range (the client drops cached copies); `includeSelf=1` (client test toggle) drops the self-exclusion so the requester's own decks can come back |
| POST | `/api/matches/report` | battle result, idempotent by `reportId` |
| POST | `/api/loop-reports` | infinity-loop evidence against a ghost deck: `{playerId, gameVersion, opponentDeckId, verdict, seed, signals, payload}`. Verdict `EnemyDeck` (= a headless re-run proved the ghost loops by itself) flags the deck's CONTENT fingerprint; every other verdict is stored as evidence and changes no state. Idempotent per `(deck, fingerprint, seed, verdict, reporter)` |
| POST | `/api/stats/snapshot` | lifetime cumulative shop/winrate stats (upsert, retry-safe) |
| POST | `/api/runs` | one full run record with shop visits + combats, idempotent by `runId`; zero-combat runs are skipped (responds ok, stores nothing) |
| POST | `/api/cards/catalog` | card metadata per game version (upsert) |
| GET | `/api/health` | liveness |
| GET | `/admin?token=...` | HTML dashboard (includes the infinity gate section) |
| POST | `/admin/decks/unflag?token=...&deckId=..` | lift the flag on a single deck row |
| POST | `/admin/decks/unflag?token=...&fingerprint=..` | clear a content key: unflags every row carrying it AND deregisters it, so later uploads are not auto-flagged |

## Infinity gate (plan §20)

A deck that loops forever by itself must never be served. The flag keys on the deck's card
MULTISET fingerprint — not on `deck_id`, because every snapshot upload inserts a new row and a
row-level flag would be evaded by the next upload. One confirmed report flags; `loop_reports`
keeps the reporter and the payload for the audit trail, and the admin dashboard can lift a flag.
Deployment: existing dbs migrate on boot (`ensureColumn` + a fingerprint backfill for rows that
predate the column), so no manual migration step.

## Tests

```bash
npm test        # node --test, throwaway DATA_DIR (tests/loopReports.test.js)
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
