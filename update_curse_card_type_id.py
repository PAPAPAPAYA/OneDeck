#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量更新 prefab 文件中的 CostNEffectContainer 组件，添加 cursedCardTypeID 引用
"""

import os
import re
import glob

# CurseCardTypeID.asset 的引用
CURSE_CARD_TYPE_ID_REF = "{fileID: 11400000, guid: 07a2aa375c0142b418e46314e9b2ca22, type: 2}"

# CostNEffectContainer 脚本的 guid
COST_N_EFFECT_CONTAINER_GUID = "a21da06ba55646f29c59d9dbf90834b3"

def find_cost_n_effect_container_blocks(content):
    """
    找到所有 CostNEffectContainer 组件的代码块
    """
    # 匹配 MonoBehaviour 块，其中 Script guid 是 CostNEffectContainer
    pattern = r'--- !u!114 &[0-9-]+\nMonoBehaviour:\n(?:  .*\n)+?  m_Script: \{fileID: 11500000, guid: ' + COST_N_EFFECT_CONTAINER_GUID + r', type: 3\}\n(?:  .*\n)+?  effectResultString: .*\n'
    
    blocks = []
    for match in re.finditer(pattern, content):
        blocks.append((match.start(), match.end(), match.group()))
    
    return blocks

def add_cursed_card_type_id(content):
    """
    在所有 CostNEffectContainer 组件中添加 cursedCardTypeID 字段
    """
    # 查找所有 CostNEffectContainer 组件块
    pattern = r'(  effectResultString: \{fileID: [^}]+\})\n(  checkCostEvent:)'
    
    def replace_func(match):
        # 如果已经存在 cursedCardTypeID，则跳过
        block_start = match.start()
        block_preview = content[block_start-200:block_start+200]
        if 'cursedCardTypeID:' in block_preview:
            return match.group(0)
        
        # 添加 cursedCardTypeID 行
        return f'{match.group(1)}\n  cursedCardTypeID: {CURSE_CARD_TYPE_ID_REF}\n{match.group(2)}'
    
    # 先找到所有 CostNEffectContainer 组件的位置
    script_pattern = r'm_Script: \{fileID: 11500000, guid: ' + COST_N_EFFECT_CONTAINER_GUID + r', type: 3\}'
    
    result = content
    for match in re.finditer(script_pattern, content):
        # 找到这个组件块的范围
        block_start = content.rfind('--- !u!114 &', 0, match.start())
        block_end_match = re.search(r'\n--- ', content[match.start():])
        if block_end_match:
            block_end = match.start() + block_end_match.start()
        else:
            block_end = len(content)
        
        block = content[block_start:block_end]
        
        # 检查是否已经设置了 cursedCardTypeID
        if 'cursedCardTypeID:' in block:
            continue
        
        # 在 effectResultString 后添加 cursedCardTypeID
        new_block = re.sub(
            r'(  effectResultString: \{fileID: [^}]+, guid: [^}]+, type: 2\})\n(  checkCostEvent:)',
            rf'\1\n  cursedCardTypeID: {CURSE_CARD_TYPE_ID_REF}\n\2',
            block
        )
        
        if new_block != block:
            result = result.replace(block, new_block)
    
    return result

def process_prefab_file(filepath):
    """
    处理单个 prefab 文件
    """
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 检查是否包含 CostNEffectContainer
    if COST_N_EFFECT_CONTAINER_GUID not in content:
        return False, "不包含 CostNEffectContainer 组件"
    
    new_content = add_cursed_card_type_id(content)
    
    if new_content == content:
        return False, "无需更新（所有组件已配置或无法更新）"
    
    with open(filepath, 'w', encoding='utf-8', newline='\n') as f:
        f.write(new_content)
    
    return True, "已更新"

def main():
    base_path = r"D:\Unity Projects\OneDeck\Assets\Prefabs\Cards\3.0 no cost (current)"
    
    # 找到所有 prefab 文件
    prefab_files = []
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file.endswith('.prefab'):
                prefab_files.append(os.path.join(root, file))
    
    print(f"找到 {len(prefab_files)} 个 prefab 文件")
    
    updated_count = 0
    skipped_count = 0
    
    for filepath in prefab_files:
        try:
            updated, message = process_prefab_file(filepath)
            filename = os.path.basename(filepath)
            if updated:
                print(f"[已更新] {filename}: {message}")
                updated_count += 1
            else:
                print(f"[跳过] {filename}: {message}")
                skipped_count += 1
        except Exception as e:
            print(f"[错误] {os.path.basename(filepath)}: {e}")
    
    print(f"\n处理完成: 更新了 {updated_count} 个文件, 跳过了 {skipped_count} 个文件")

if __name__ == "__main__":
    main()
