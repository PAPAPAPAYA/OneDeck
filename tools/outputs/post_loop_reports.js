'use strict';

/**
 * Post the verdicts from an InfinityBatchScan report to the server (plan §23).
 *
 * The scan computes; this posts. Splitting them keeps the one data-affecting step countable and
 * dry-runnable: by default this script POSTs NOTHING and only prints what it would send.
 *
 *   node tools/outputs/post_loop_reports.js tools/outputs/infinity_scan_<stamp>.json
 *        -> dry run: lists every entry it would post (deck, cards, size)
 *
 *   node tools/outputs/post_loop_reports.js <report.json> --post --player-id <playerId> [--server URL]
 *        -> posts one /api/loop-reports per PROVEN entry and prints each response
 *
 * By default only entries whose verdict is proven — 1-minimal, multi-seed stable, untruncated,
 * the same bar the server applies to a combo — are posted. A deck that loops on one observed seed
 * but not another is withheld and listed (--include-unproven overrides that deliberately).
 *
 * Only entries with status = "infinite" AND a deckId > 0 are postable: a local recorded deck has
 * no server row to accuse. The server flags on verdict "EnemyDeck" and registers the combo when
 * the payload carries a proven minimum (§20/§21) — so this script is what turns a scan into
 * production protection, and it is deliberately explicit about doing so.
 */

const fs = require('fs');
const path = require('path');

const DEFAULT_SERVER = 'http://8.153.150.197';

function parseArgs(argv)
{
	const args = { reportFile: null, post: false, playerId: '', server: DEFAULT_SERVER, gameVersion: '', includeUnproven: false };
	for (let i = 0; i < argv.length; i++)
	{
		const arg = argv[i];
		if (arg === '--post') args.post = true;
		else if (arg === '--dry-run') args.post = false;
		else if (arg === '--include-unproven') args.includeUnproven = true;
		else if (arg === '--player-id') args.playerId = argv[++i] || '';
		else if (arg === '--server') args.server = (argv[++i] || DEFAULT_SERVER).replace(/\/$/, '');
		else if (arg === '--game-version') args.gameVersion = argv[++i] || '';
		else if (!args.reportFile) args.reportFile = arg;
	}
	return args;
}

function loadReport(file)
{
	const raw = fs.readFileSync(file, 'utf8');
	const report = JSON.parse(raw);
	if (!report || !Array.isArray(report.decks))
	{
		throw new Error('not a scan report (expected a "decks" array): ' + file);
	}
	return report;
}

/**
 * Entries worth posting. The bar is the SAME ingest gate the server applies to a combo
 * (oneMinimal + untruncated + multi-seed stable, §4/§21): a deck that only loops on one observed
 * seed is not yet a proven loop, and posting it would flag a real player's deck on weak evidence.
 * --include-unproven lifts that bar deliberately, for a case an operator has judged by hand.
 */
function postable(report, includeUnproven)
{
	return report.decks.filter((deck) =>
	{
		if (!deck || deck.status !== 'infinite') return false;
		if (!(Number(deck.deckId) > 0)) return false;
		if (typeof deck.reportJson !== 'string' || deck.reportJson.length === 0) return false;
		if (includeUnproven) return true;
		return deck.oneMinimal === true && deck.truncated !== true && deck.multiSeedStable !== false;
	});
}

/** Infinite verdicts the default bar refuses (reported so the operator sees what was left out). */
function withheld(report, includeUnproven)
{
	const posted = new Set(postable(report, includeUnproven));
	return report.decks.filter((deck) => deck && deck.status === 'infinite' && !posted.has(deck));
}

function describe(report)
{
	const counts = new Map();
	for (const deck of report.decks)
	{
		const status = deck && deck.status ? deck.status : 'unknown';
		counts.set(status, (counts.get(status) || 0) + 1);
	}
	return counts;
}

async function postOne(server, playerId, gameVersion, deck)
{
	// The report payload already states its own verdict/evidence; this wrapper adds who/where.
	let seed = 0;
	let signals = '';
	try
	{
		const parsed = JSON.parse(deck.reportJson);
		if (Array.isArray(parsed.reproSeeds) && parsed.reproSeeds.length > 0) seed = parsed.reproSeeds[0];
		signals = (parsed.tripSignal || '') + ' | batch-scan ' + (deck.label || '');
	}
	catch { /* payload stays opaque; the server stores it verbatim */ }

	const body = {
		playerId,
		gameVersion: gameVersion || deck.gameVersion || '4.0',
		opponentDeckId: Number(deck.deckId),
		verdict: 'EnemyDeck',
		seed,
		signals: signals.slice(0, 1000),
		payload: deck.reportJson,
	};

	const response = await fetch(server + '/api/loop-reports', {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(body),
		// A dead box must fail loudly instead of hanging a batch step forever.
		signal: AbortSignal.timeout(30000),
	});
	let json = null;
	try { json = await response.json(); } catch { /* non-json */ }
	return { status: response.status, body: json };
}

async function main()
{
	const args = parseArgs(process.argv.slice(2));
	if (!args.reportFile) {
		console.error('usage: node tools/outputs/post_loop_reports.js <report.json> [--post --player-id <id>] [--server URL] [--include-unproven]');
		process.exit(2);
	}
	const file = path.resolve(args.reportFile);
	const report = loadReport(file);
	const targets = postable(report, args.includeUnproven);
	const skipped = withheld(report, args.includeUnproven);

	console.log('[post_loop_reports] report: ' + file);
	console.log('[post_loop_reports] generated: ' + (report.generatedUtc || '?') + ' | unity ' + (report.unityVersion || '?'));
	const counts = describe(report);
	console.log('[post_loop_reports] entries: '
		+ [...counts.entries()].map(([k, v]) => k + '=' + v).join(' '));
	console.log('[post_loop_reports] postable (infinite, proven minimum, with a server deck row): '
		+ targets.length + (args.includeUnproven ? '  [--include-unproven: bar lifted]' : ''));

	for (const deck of targets)
	{
		let cards = deck.cards || [];
		console.log('  - deck ' + deck.deckId + '  ' + cards.join(',')
			+ '  (1-minimal=' + deck.oneMinimal + ' truncated=' + deck.truncated + ' multiSeed=' + deck.multiSeedStable + ')');
	}

	if (skipped.length > 0)
	{
		console.log('[post_loop_reports] WITHHELD (infinite but not a proven minimum — one observed seed is not enough, §4):');
		for (const deck of skipped)
		{
			console.log('  x deck ' + (deck.deckId || '-') + '  ' + (deck.source || '')
				+ '  1-minimal=' + deck.oneMinimal + ' truncated=' + deck.truncated + ' multiSeed=' + deck.multiSeedStable
				+ (Number(deck.deckId) > 0 ? '' : '  (no server row)'));
		}
	}

	if (!args.post)
	{
		console.log('[post_loop_reports] DRY RUN — nothing was sent. Re-run with --post --player-id <playerId> to file these.');
		return;
	}
	if (!args.playerId)
	{
		console.error('[post_loop_reports] --post requires --player-id (the reporter the server records)');
		process.exit(2);
	}

	let ok = 0;
	let failed = 0;
	for (const deck of targets)
	{
		const result = await postOne(args.server, args.playerId, args.gameVersion, deck);
		const verdict = result.body && result.body.flagged ? 'FLAGGED' : (result.body && result.body.deduped ? 'deduped' : 'stored');
		if (result.status === 200 || result.status === 201) ok++;
		else failed++;
		console.log('  deck ' + deck.deckId + ' -> HTTP ' + result.status + ' ' + verdict
			+ (result.body && result.body.comboKey ? ' combo=' + result.body.comboKey + ' (' + result.body.comboStatus + ')' : '')
			+ (result.body && result.body.error ? ' error=' + result.body.error : ''));
	}
	console.log('[post_loop_reports] done: ' + ok + ' accepted, ' + failed + ' failed');
	if (failed > 0) process.exit(1);
}

main().catch((error) =>
{
	console.error('[post_loop_reports] ' + error.message);
	process.exit(1);
});
