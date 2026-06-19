#!/usr/bin/env python3
# Bulk-wraps active Debug.Log* calls with TestManager.Log* and adds the required using directive.

import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]

FILES = [
    "Assets/Scripts/Managers/CombatManager.cs",
    "Assets/Scripts/Managers/PhaseManager.cs",
    "Assets/Scripts/Managers/EffectChainManager.cs",
    "Assets/Scripts/Managers/AnimationStateTracker.cs",
    "Assets/Scripts/Managers/RecorderAnimationPlayer.cs",
    "Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs",
    "Assets/Scripts/UXPrototype/CombatUXManager.cs",
    "Assets/Scripts/UXPrototype/CardPhysObjScript.cs",
    "Assets/Scripts/Effects/BuryEffect.cs",
    "Assets/Scripts/Effects/StageEffect.cs",
    "Assets/Scripts/Effects/EffectScript.cs",
    "Assets/Scripts/Editor/CardTypeIDValidator.cs",
]

USING_DIRECTIVE = "using DefaultNamespace.Managers;"

CALL_MAPPING = {
    "Debug.Log(": "TestManager.Log(",
    "Debug.LogWarning(": "TestManager.LogWarning(",
    "Debug.LogError(": "TestManager.LogError(",
}

LOG_CALL_RE = re.compile(r"(UnityEngine\.)?(Debug\.Log(?:Warning|Error)?\()")


def is_commented(line: str, match_start: int) -> bool:
    before = line[:match_start]
    return "//" in before or "/*" in before


def add_using_if_missing(lines: list[str]) -> list[str]:
    for line in lines:
        if line.strip() == USING_DIRECTIVE:
            return lines

    insert_index = 0
    for i, line in enumerate(lines):
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("using ") or stripped.startswith("#"):
            insert_index = i + 1
            continue
        break

    new_lines = list(lines)
    new_lines.insert(insert_index, USING_DIRECTIVE + "\r\n")
    if insert_index < len(new_lines) and new_lines[insert_index + 1].strip():
        new_lines.insert(insert_index + 1, "\r\n")
    return new_lines


def replace_calls(lines: list[str]) -> list[str]:
    new_lines = []
    changed = False
    for line in lines:
        new_line = line

        def repl(m: re.Match) -> str:
            nonlocal changed
            if is_commented(line, m.start()):
                return m.group(0)
            call = m.group(2)
            replacement = CALL_MAPPING.get(call)
            if replacement is None:
                return m.group(0)
            changed = True
            return replacement

        new_line = LOG_CALL_RE.sub(repl, line)
        new_lines.append(new_line)
    return new_lines, changed


def process_file(rel_path: str) -> bool:
    path = PROJECT_ROOT / rel_path
    text = path.read_text(encoding="utf-8", newline="")
    lines = text.splitlines(keepends=True)

    lines = add_using_if_missing(lines)
    lines, changed = replace_calls(lines)

    if changed or USING_DIRECTIVE in text:
        path.write_text("".join(lines), encoding="utf-8", newline="")
        print(f"Updated: {rel_path}")
        return True
    print(f"No changes: {rel_path}")
    return False


def main() -> int:
    for rel_path in FILES:
        process_file(rel_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
