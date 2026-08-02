import json, sys, urllib.request

URL = "http://127.0.0.1:8080/mcp"
SID_FILE = "tools/scripts/.mcp_session_id"

def post(payload, sid=None):
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if sid:
        headers["Mcp-Session-Id"] = sid
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=60) as resp:
        new_sid = resp.headers.get("Mcp-Session-Id", sid)
        body = resp.read().decode("utf-8", "replace")
    if not body.strip():
        return new_sid, None
    for line in body.splitlines():
        if line.startswith("data:"):
            return new_sid, json.loads(line[5:].strip())
    return new_sid, json.loads(body)

def main():
    # args: <method> <params-json>
    method = sys.argv[1]
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    try:
        with open(SID_FILE) as f:
            sid = f.read().strip()
    except OSError:
        sid = None
    if sid is None:
        sid, _ = post({"jsonrpc": "2.0", "id": 0, "method": "initialize",
                       "params": {"protocolVersion": "2025-03-26", "capabilities": {},
                                  "clientInfo": {"name": "kimi", "version": "0.1"}}})
        post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
        with open(SID_FILE, "w") as f:
            f.write(sid)
    sid, result = post({"jsonrpc": "2.0", "id": 1, "method": method, "params": params}, sid)
    print(json.dumps(result, ensure_ascii=False, indent=2))

if __name__ == "__main__":
    main()
