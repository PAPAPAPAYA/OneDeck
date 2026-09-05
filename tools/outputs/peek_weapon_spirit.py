import re

t = open("Assets/Prefabs/Cards/4.0/2_Rare/WEAPON_SPIRIT.prefab", encoding="utf-8").read()
m = re.search(r"^  cardDesc: (.*?)(?=^  \w)", t, re.M | re.S)
s = m.group(1)


def dec(x):
    x = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda mm: chr(int(mm.group(1), 16)), x)
    return x


print(dec(s))
