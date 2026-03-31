#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
扫描项目资源，提取所有使用过的唯一中文字符，
用于 TextMesh Pro Font Asset Creator 的 Custom Characters 模式。
"""

import os
import re

# 项目根目录（脚本所在目录）
PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
ASSETS_DIR = os.path.join(PROJECT_ROOT, "Assets")

# 输出路径
OUTPUT_DIR = os.path.join(ASSETS_DIR, "TextMesh Pro", "Fonts", "SourceHanSansCN")
OUTPUT_FILE = os.path.join(OUTPUT_DIR, "custom_chars.txt")

# 扫描的文件扩展名
SCAN_EXTENSIONS = {".cs", ".asset", ".prefab", ".json", ".yaml", ".yml", ".txt", ".md"}

# 跳过的目录
SKIP_DIRS = {"Library", "Temp", "obj", "Logs", ".git", ".idea", ".vscode", "Packages"}


def is_chinese_char(c):
    """判断字符是否为中文（CJK Unified Ideographs）"""
    return '\u4e00' <= c <= '\u9fff'


def extract_chinese_from_text(text):
    """从文本中提取所有唯一中文字符"""
    chars = set()
    for c in text:
        if is_chinese_char(c):
            chars.add(c)
    return chars


def read_file_text(filepath):
    """尝试以多种编码读取文件"""
    encodings = ["utf-8", "utf-8-sig", "gbk", "gb2312", "latin-1"]
    for enc in encodings:
        try:
            with open(filepath, "r", encoding=enc) as f:
                return f.read()
        except (UnicodeDecodeError, Exception):
            continue
    return ""


def scan_project():
    """扫描 Assets 目录，收集所有中文字符"""
    all_chars = set()
    scanned_files = 0

    for root, dirs, files in os.walk(ASSETS_DIR):
        # 跳过不需要的目录
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]

        for filename in files:
            ext = os.path.splitext(filename)[1].lower()
            if ext not in SCAN_EXTENSIONS:
                continue

            filepath = os.path.join(root, filename)
            text = read_file_text(filepath)
            if not text:
                continue

            chars = extract_chinese_from_text(text)
            if chars:
                all_chars.update(chars)
                scanned_files += 1

    return all_chars, scanned_files


def main():
    print("=" * 50)
    print("扫描项目中使用的中文字符...")
    print(f"Assets 目录: {ASSETS_DIR}")
    print("=" * 50)

    all_chars, scanned_files = scan_project()

    if not all_chars:
        print("\n未在项目中找到任何中文字符。")
        print("提示：如果你打算在卡牌描述或 UI 中使用中文，")
        print("      可以先写一些中文文案，再运行此脚本。")
        return

    # 按 Unicode 排序，输出为连续字符串
    sorted_chars = sorted(all_chars)
    char_string = "".join(sorted_chars)

    # 确保输出目录存在
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    # 写入文件
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        f.write(char_string)

    print(f"\n扫描完成！")
    print(f"  - 扫描文件数: {scanned_files}")
    print(f"  - 唯一中文字符数: {len(sorted_chars)}")
    print(f"  - 输出文件: {OUTPUT_FILE}")
    print(f"\n前 100 个字符预览:")
    print(char_string[:100])
    if len(char_string) > 100:
        print(f"... (共 {len(char_string)} 个字符)")

    print("\n使用方式:")
    print("1. 打开 Unity -> Window -> TextMeshPro -> Font Asset Creator")
    print("2. Character Set 选择 'Custom Characters'")
    print("3. 将上述文件内容全部复制粘贴到 Custom Characters 输入框")
    print("4. Atlas Resolution 设为 2048x2048 或 4096x4096")
    print("5. 点击 Generate Font Atlas")


if __name__ == "__main__":
    main()
