
import os
import re
from datetime import datetime

PROJECT_ROOT = r'd:\Unity Projects\OneDeck'
CARDS_DIR = os.path.join(PROJECT_ROOT, 'Assets', 'Prefabs', 'Cards', '3.0 no cost (current)')
GAMEEVENTS_DIR = os.path.join(PROJECT_ROOT, 'Assets', 'SORefs', 'GameEvents')
DOCS_DIR = os.path.join(PROJECT_ROOT, 'docs')

STATUS_EFFECT_NAMES = ['None', 'Infected', 'Mana', 'HeartChanged', 'Power', 'Rest', 'Revive', 'Counter']
TAG_NAMES = ['None', 'Linger', 'ManaX', 'DeathRattle']

def parse_hex_int_list(hex_str):
    if not hex_str:
        return []
    hex_str = hex_str.strip().replace(' ', '').replace('\n', '')
    if len(hex_str) % 8 != 0:
        try:
            return [int(hex_str)]
        except ValueError:
            return []
    result = []
    for i in range(0, len(hex_str), 8):
        chunk = hex_str[i:i+8]
        val = int.from_bytes(bytes.fromhex(chunk), byteorder='little', signed=True)
        result.append(val)
    return result

def extract_simple_field(block_text, field_name):
    m = re.search(r'^  ' + re.escape(field_name) + r':\s*(.*?)$', block_text, re.MULTILINE)
    if m:
        return m.group(1).strip()
    return ''

def extract_all_fields(block_text):
    fields = {}
    for m in re.finditer(r'^  (\w+):\s*(.*?)$', block_text, re.MULTILINE):
        fields[m.group(1)] = m.group(2).strip()
    return fields

def extract_calls(block_text, event_prop_name):
    calls = []
    pattern = r'  ' + re.escape(event_prop_name) + r':\n    m_PersistentCalls:\n      m_Calls:'
    match = re.search(pattern, block_text)
    if not match:
        return calls
    rest = block_text[match.end():]
    lines = rest.split('\n')
    call_blocks = []
    current_call = []
    in_call = False
    for line in lines:
        if line.startswith('      - '):
            if current_call:
                call_blocks.append('\n'.join(current_call))
            current_call = [line]
            in_call = True
        elif in_call:
            if line.startswith('  ') and not line.startswith('      ') and not line.startswith('       '):
                break
            if line.startswith('    ') or line.startswith('      ') or line.startswith('        '):
                current_call.append(line)
            elif line.strip() == '':
                current_call.append(line)
            else:
                break
    if current_call:
        call_blocks.append('\n'.join(current_call))
    for cb in call_blocks:
        target_type_match = re.search(r'm_TargetAssemblyTypeName:\s*(.+?)(?:\n|$)', cb)
        method_match = re.search(r'm_MethodName:\s*(.+?)(?:\n|$)', cb)
        int_arg_match = re.search(r'm_IntArgument:\s*(\d+)', cb)
        string_arg_match = re.search(r'm_StringArgument:\s*(.*)', cb)
        target_type = target_type_match.group(1).strip() if target_type_match else ''
        method = method_match.group(1).strip() if method_match else ''
        int_arg = int(int_arg_match.group(1)) if int_arg_match else 0
        string_arg = string_arg_match.group(1).strip() if string_arg_match else ''
        target_type = target_type.replace('\n', ' ').replace('  ', ' ').strip()
        target_type = re.sub(r',\s*Assembly-CSharp$', '', target_type)
        calls.append({'target_type': target_type, 'method': method, 'int_arg': int_arg, 'string_arg': string_arg})
    return calls
