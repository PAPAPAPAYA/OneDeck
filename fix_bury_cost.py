from pathlib import Path
import re

base_dir = Path("Assets/Prefabs/Cards/3.0 no cost (current)")
prefabs = list(base_dir.rglob("*.prefab"))

fixed = []
failed = []

for p in prefabs:
    try:
        content = p.read_text(encoding="utf-8")
        new_content = re.sub(r"(buryCost):\s*\d+", r"\1: 0", content)
        if new_content != content:
            p.write_text(new_content, encoding="utf-8")
            fixed.append(str(p.relative_to("Assets/Prefabs/Cards")))
    except Exception as e:
        failed.append(f"{p}: {e}")

print(f"Fixed {len(fixed)} prefabs.")
if fixed:
    for f in fixed:
        print(f"  - {f}")
if failed:
    print(f"Failed {len(failed)}:")
    for f in failed:
        print(f"  - {f}")
