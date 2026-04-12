# -*- coding: utf-8 -*-
import os
import re

def get_all_prefabs():
    base_path = 'Assets/Prefabs/Cards/3.0 no cost (current)'
    prefabs = []
    for root, dirs, files in os.walk(base_path):
        for f in files:
            if f.endswith('.prefab') and not f.endswith('.meta'):
                prefabs.append(os.path.join(root, f))
    return prefabs

def parse_prefab(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract all GameObjects
    gameobjects = {}
    go_pattern = r'--- !u!1 &(\d+)\s*\nGameObject:\s*\n((?:  .*\n)+)'
    
    for match in re.finditer(go_pattern, content):
        file_id = match.group(1)
        go_block = match.group(2)
        
        # Extract name
        name_match = re.search(r'm_Name: (.+)', go_block)
        name = name_match.group(1) if name_match else 'Unknown'
        # Decode Unicode escapes
        try:
            name = name.encode('latin1').decode('unicode_escape')
        except:
            pass
        
        gameobjects[file_id] = name
    
    # Extract Transform hierarchy
    transform_pattern = r'--- !u!4 &(\d+)\s*\nTransform:\s*\n((?:  .*\n)+?)(?=\n--- |\Z)'
    transforms = {}
    
    for match in re.finditer(transform_pattern, content):
        file_id = match.group(1)
        trans_block = match.group(2)
        
        # Extract GameObject reference
        go_match = re.search(r'm_GameObject: \{fileID: (\d+|0)\}', trans_block)
        go_id = go_match.group(1) if go_match else '0'
        
        # Extract Children
        children = []
        for child_match in re.finditer(r'- \{fileID: (\d+)\}', trans_block):
            child_trans_id = child_match.group(1)
            children.append(child_trans_id)
        
        # Extract Father
        father_match = re.search(r'm_Father: \{fileID: (\d+|0)\}', trans_block)
        father_id = father_match.group(1) if father_match else '0'
        
        transforms[file_id] = {'go_id': go_id, 'children': children, 'father': father_id}
    
    # Extract CostNEffectContainer and check cursedCardTypeID
    # CostNEffectContainer script GUID: a21da06ba55646f29c59d9dbf90834b3
    mono_pattern = r'--- !u!114 &(\d+)\s*\nMonoBehaviour:\s*\n((?:  .*\n)+?)(?=\n---|\Z)'
    cursed_null_items = []
    
    for match in re.finditer(mono_pattern, content, re.DOTALL):
        file_id = match.group(1)
        block = match.group(2)
        
        # Check if it's CostNEffectContainer by looking at the script GUID
        # CostNEffectContainer script GUID: a21da06ba55646f29c59d9dbf90834b3
        if 'a21da06ba55646f29c59d9dbf90834b3' in block:
            # Find cursedCardTypeID - check if it's set or null
            cursed_match = re.search(r'cursedCardTypeID:\s*(\{fileID:\s*(\d+|0)[^}]*\})?', block)
            
            is_null = False
            if cursed_match:
                matched_text = cursed_match.group(0)
                # Check if fileID is 0 or if the field is empty
                fileid_match = re.search(r'fileID:\s*(\d+|0)', matched_text)
                if fileid_match:
                    fileid_val = fileid_match.group(1)
                    if fileid_val == '0':
                        is_null = True
                else:
                    # cursedCardTypeID: with no value
                    is_null = True
            else:
                # Field not found = null
                is_null = True
            
            if is_null:
                # Find GameObject
                go_match = re.search(r'm_GameObject: \{fileID: (\d+|0)\}', block)
                go_id = go_match.group(1) if go_match else '0'
                cursed_null_items.append({'file_id': file_id, 'go_id': go_id})
    
    return gameobjects, transforms, cursed_null_items

def build_tree(gameobjects, transforms, go_file_id, prefix='', is_last=True):
    result = []
    go_name = gameobjects.get(go_file_id, 'Unknown')
    
    connector = 'L-- ' if is_last else '|-- '
    result.append(prefix + connector + go_name + ' [ID:' + go_file_id[-8:] + ']')
    
    # Find transform for this GameObject
    trans_id = None
    for tid, tdata in transforms.items():
        if tdata['go_id'] == go_file_id:
            trans_id = tid
            break
    
    if trans_id and trans_id in transforms:
        children_trans_ids = transforms[trans_id]['children']
        new_prefix = prefix + ('    ' if is_last else '|   ')
        for i, child_trans_id in enumerate(children_trans_ids):
            is_last_child = (i == len(children_trans_ids) - 1)
            if child_trans_id in transforms:
                child_go_id = transforms[child_trans_id]['go_id']
                result.extend(build_tree(gameobjects, transforms, child_go_id, new_prefix, is_last_child))
    
    return result

def main():
    prefabs = get_all_prefabs()
    output_lines = []
    
    output_lines.append('=' * 80)
    output_lines.append('共找到 ' + str(len(prefabs)) + ' 个 prefab 文件')
    output_lines.append('=' * 80)
    
    all_cursed_null = []
    
    for filepath in sorted(prefabs):
        rel_path = os.path.relpath(filepath, 'Assets/Prefabs/Cards/3.0 no cost (current)')
        output_lines.append('')
        output_lines.append('[' + rel_path + ']')
        output_lines.append('-' * 60)
        
        gameobjects, transforms, cursed_null = parse_prefab(filepath)
        
        # Find root nodes (no father or father is 0)
        root_go_ids = []
        for tid, tdata in transforms.items():
            if tdata['father'] == '0' and tdata['go_id'] != '0':
                root_go_ids.append(tdata['go_id'])
        
        # Print hierarchy
        if root_go_ids:
            for i, root_id in enumerate(root_go_ids):
                is_last = (i == len(root_go_ids) - 1)
                tree_lines = build_tree(gameobjects, transforms, root_id, '', is_last)
                for line in tree_lines:
                    output_lines.append(line)
        else:
            # No transform hierarchy, just list gameobjects
            for go_id, name in gameobjects.items():
                output_lines.append('L-- ' + name + ' [ID:' + go_id[-8:] + ']')
        
        # Collect cursedCardTypeID null items
        for item in cursed_null:
            go_name = gameobjects.get(item['go_id'], 'Unknown')
            all_cursed_null.append({
                'prefab': rel_path,
                'go_name': go_name,
                'go_id': item['go_id'][-8:],
                'component_id': item['file_id'][-8:]
            })
    
    # Print cursedCardTypeID null summary
    output_lines.append('')
    output_lines.append('=' * 80)
    output_lines.append('CursedCardTypeID 为 NULL 的 CostNEffectContainer 组件 (' + str(len(all_cursed_null)) + ' 个)')
    output_lines.append('=' * 80)
    
    for item in all_cursed_null:
        output_lines.append('  > ' + item['prefab'])
        output_lines.append('      GameObject: ' + item['go_name'] + ' [GO:' + item['go_id'] + ', Comp:' + item['component_id'] + ']')
    
    # Write to file with UTF-8 BOM for Notepad compatibility
    with open('prefab_analysis_result.txt', 'w', encoding='utf-8-sig') as f:
        f.write('\n'.join(output_lines))
    
    print('分析完成！结果已保存到 prefab_analysis_result.txt')
    print('CursedCardTypeID 为 null 的组件数量: ' + str(len(all_cursed_null)))

if __name__ == '__main__':
    main()
