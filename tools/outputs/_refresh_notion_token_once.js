// One-off OAuth token refresh for the Notion MCP credential used by notion_curse_summoner_finish.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";

function readJson(p) {
	return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, ""));
}

(async () => {
	try {
		const tok = readJson(TOK_FILE);
		const client = readJson(CLIENT_FILE);
		const body = new URLSearchParams({
			grant_type: "refresh_token",
			refresh_token: tok.refresh_token,
			client_id: client.client_id,
		});
		const resp = await fetch(TOKEN_URL, {
			method: "POST",
			headers: { "Content-Type": "application/x-www-form-urlencoded" },
			body,
		});
		if (!resp.ok) throw new Error("token refresh failed: HTTP " + resp.status + " " + (await resp.text()).slice(0, 200));
		const fresh = await resp.json();
		tok.access_token = fresh.access_token;
		if (fresh.refresh_token) tok.refresh_token = fresh.refresh_token;
		tok.expires_in = fresh.expires_in;
		fs.writeFileSync(TOK_FILE, JSON.stringify(tok, null, 2), "utf-8");
		console.log("token refreshed ok, expires_in=" + fresh.expires_in);
	} catch (e) {
		console.log("FAILED:", e.message);
		process.exit(1);
	}
})();
