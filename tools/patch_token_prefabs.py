import os, re

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

JU_ON = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab"
RIFT = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/RIFT.prefab"

def read(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()

def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)

# --- JU_ON: insert alwaysShowAttack: 1 right after the cardType: 2 line ---
txt = read(JU_ON)
assert txt.count("cardType: 2") == 1, "JU_ON cardType count unexpected"
assert "alwaysShowAttack" not in txt, "JU_ON already has alwaysShowAttack"
lines = txt.splitlines(keepends=True)
out = []
for line in lines:
    out.append(line)
    if line.strip() == "cardType: 2":
        eol = "\r\n" if line.endswith("\r\n") else "\n"
        out.append("  alwaysShowAttack: 1" + eol)
write(JU_ON, "".join(out))
print("JU_ON patched")

# --- RIFT: cardType 0 -> 2 ---
txt = read(RIFT)
assert txt.count("cardType: 0") == 1, "RIFT cardType count unexpected"
txt = txt.replace("cardType: 0", "cardType: 2", 1)
write(RIFT, txt)
print("RIFT patched")

# verify
ju = read(JU_ON)
rift = read(RIFT)
assert "alwaysShowAttack: 1" in ju and "cardType: 2" in ju
assert "cardType: 2" in rift and "alwaysShowAttack" not in rift
print("verified: JU_ON has alwaysShowAttack:1 + cardType:2 ; RIFT has cardType:2")
