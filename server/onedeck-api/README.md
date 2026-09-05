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
| POST | `/api/decks` | upload ghost deck snapshot |
| GET | `/api/decks/opponents?playerId&gameVersion&maxSession&perSession` | batch opponent decks |
| POST | `/api/matches/report` | battle result, idempotent by `reportId` |
| POST | `/api/stats/snapshot` | lifetime cumulative shop/winrate stats (upsert, retry-safe) |
| POST | `/api/runs` | one full run record with shop visits + combats, idempotent by `runId`; zero-combat runs are skipped (responds ok, stores nothing) |
| POST | `/api/cards/catalog` | card metadata per game version (upsert) |
| GET | `/api/health` | liveness |
| GET | `/admin?token=...` | HTML dashboard |

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
