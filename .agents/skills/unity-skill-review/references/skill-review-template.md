# Skill Review: `<skill-name>`

**Date:** <YYYY-MM-DD>
**Subject:** `.agents/skills/<skill-name>/SKILL.md` (<line-count> lines)
**Method:** Static verification of every factual claim (source files + prefab YAML), sibling-skill sweep, live execution per the hook in `unity-skill-review` Section 3.

---

## 1. Live Test Status (<Executed | Blocked>)

<Hook executed, environment (Unity version, Play/Edit Mode), observed output vs. documented expected output. If blocked: what blocked it, and when/how completion is scheduled.>

---

## 2. Factual Errors in the Skill

<One subsection per error: what the skill claims, what the code/assets actually say, evidence as `path:line`, and the impact of following the wrong claim.>

---

## 3. Structural / Architectural Issues

<Duplication, missing preconditions, destructive side effects, drift from sibling documents, misplaced content.>

---

## 4. Verified-Accurate Claims (for the record)

<Claims checked and confirmed correct, with evidence. This section is what lets the next review check only deltas — never skip it.>

---

## 5. Recommendations (Prioritized)

1. <Fix, ordered by impact.>

---

## 6. Follow-up Log

<Filled in after fixes are applied: what was changed, where, when. Leave as "Pending" until then.>

- <YYYY-MM-DD>: <change applied>
