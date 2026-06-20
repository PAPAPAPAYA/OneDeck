#!/usr/bin/env bash
# Check whether ShopPoolRef contains all 3.0 card prefabs
# (excluding the _DONT INCLUDE folder and its subfolders).
# Usage: ./check-shop-pool.sh [project-root]

set -euo pipefail

PROJECT_ROOT="${1:-.}"
cd "$PROJECT_ROOT"

SHOP_POOL="Assets/SORefs/ShopRefs/ShopPoolRef.asset"
CARD_FOLDER=$(find Assets/Prefabs/Cards -maxdepth 1 -type d -name '3.0*' | head -n 1)

if [ ! -f "$SHOP_POOL" ]; then
	echo "Error: ShopPoolRef not found at $SHOP_POOL" >&2
	exit 1
fi

if [ -z "$CARD_FOLDER" ]; then
	echo "Error: No 3.0 card folder found under Assets/Prefabs/Cards" >&2
	exit 1
fi

# Extract GUIDs from ShopPoolRef.deck only (between 'deck:' and 'defaultDeck:').
deck_guids_file=$(mktemp)
awk '/^  deck:/{flag=1; next} /^  defaultDeck:/{flag=0} flag' "$SHOP_POOL" \
	| grep -oE '[0-9a-f]{32}' \
	| sort -u > "$deck_guids_file"

# Extract GUIDs from all non-excluded prefab .meta files.
prefab_guids_file=$(mktemp)
find "$CARD_FOLDER" -type f -name "*.prefab" ! -path "*_DONT INCLUDE*" -print0 \
	| while IFS= read -r -d '' prefab; do
		meta="${prefab}.meta"
		if [ -f "$meta" ]; then
			grep -m1 "^guid:" "$meta" | awk '{print $2}'
		fi
	done \
	| sort -u > "$prefab_guids_file"

prefab_count=$(wc -l < "$prefab_guids_file" | tr -d ' ')
deck_count=$(wc -l < "$deck_guids_file" | tr -d ' ')

echo "=== Summary ==="
echo "Card folder: $CARD_FOLDER"
echo "Prefabs in 3.0 folder (excluding _DONT INCLUDE): $prefab_count"
echo "Entries in ShopPoolRef deck: $deck_count"

echo ""
echo "=== Prefabs NOT in ShopPoolRef (missing) ==="
missing_guids_file=$(mktemp)
comm -23 "$prefab_guids_file" "$deck_guids_file" > "$missing_guids_file"
if [ ! -s "$missing_guids_file" ]; then
	echo "None"
else
	while IFS= read -r guid; do
		meta_path=$(find "$CARD_FOLDER" -type f -name "*.prefab.meta" ! -path "*_DONT INCLUDE*" -print0 | while IFS= read -r -d '' meta; do
			if grep -q "^guid: $guid$" "$meta"; then
				echo "$meta"
				break
			fi
		done)
		if [ -n "$meta_path" ]; then
			echo "$guid -> ${meta_path%.meta}"
		else
			echo "$guid"
		fi
	done < "$missing_guids_file"
fi

echo ""
echo "=== ShopPoolRef entries NOT found in 3.0 prefabs (orphaned/removed) ==="
orphaned_guids_file=$(mktemp)
comm -13 "$prefab_guids_file" "$deck_guids_file" > "$orphaned_guids_file"
if [ ! -s "$orphaned_guids_file" ]; then
	echo "None"
else
	cat "$orphaned_guids_file"
fi

rm -f "$deck_guids_file" "$prefab_guids_file" "$missing_guids_file" "$orphaned_guids_file"
