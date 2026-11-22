#!/usr/bin/env python3
"""Статическая проверка исходников China Fishing (БЕЗ Unity и БЕЗ компилятора).
Проверяет: баланс скобок, строки/комментарии, обращения Класс.Член к проектным классам,
значения enum, целостность данных (11 рыб, 37 предметов, наживки существуют).
Это НЕ замена компиляции."""
import re, glob, sys, os

root = sys.argv[1] if len(sys.argv) > 1 else '.'
files = sorted(glob.glob(os.path.join(root, 'Assets/**/*.cs'), recursive=True))
errors = []

def strip(src):
    out = []; i = 0; n = len(src); line = 1
    while i < n:
        c = src[i]
        if src.startswith('//', i):
            j = src.find('\n', i); j = n if j < 0 else j; i = j; continue
        if src.startswith('/*', i):
            j = src.find('*/', i); j = n if j < 0 else j + 2; out.append('\n' * src[i:j].count('\n')); i = j; continue
        if c == '"':
            verbatim = i > 0 and src[i-1] == '@'
            j = i + 1
            while j < n:
                if verbatim:
                    if src[j] == '"':
                        if j + 1 < n and src[j+1] == '"': j += 2; continue
                        break
                else:
                    if src[j] == '\\': j += 2; continue
                    if src[j] == '"': break
                    if src[j] == '\n': break
                j += 1
            out.append('""'); i = j + 1; continue
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                if src[j] == '\\': j += 1
                j += 1
            out.append("'c'"); i = j + 1; continue
        out.append(c); i += 1
    return ''.join(out)

clean = {}
for f in files:
    src = open(f, encoding='utf-8').read()
    c = strip(src); clean[f] = c
    for a, b in ('{}', '()', '[]'):
        if c.count(a) != c.count(b): errors.append(f"{os.path.basename(f)}: несбалансированы {a}{b} ({c.count(a)} vs {c.count(b)})")
    # стек для порядка
    st = []; pairs = {')': '(', ']': '[', '}': '{'}
    for ln, text in enumerate(c.split('\n'), 1):
        for ch in text:
            if ch in '([{': st.append((ch, ln))
            elif ch in ')]}':
                if not st or st[-1][0] != pairs[ch]: errors.append(f"{os.path.basename(f)}:{ln}: лишняя/неверная скобка {ch}"); st = []; break
                st.pop()

# ---- классы и их члены (грубо)
allsrc = '\n'.join(clean.values())
classes = {}
for m in re.finditer(r'\b(?:public |internal |static |sealed |abstract )*(class|enum|struct)\s+(\w+)', allsrc):
    kind, name = m.group(1), m.group(2)
    i = allsrc.find('{', m.end())
    if i < 0: continue
    depth = 0; j = i
    while j < len(allsrc):
        if allsrc[j] == '{': depth += 1
        elif allsrc[j] == '}':
            depth -= 1
            if depth == 0: break
        j += 1
    body = allsrc[i+1:j]
    classes.setdefault(name, {'kind': kind, 'body': ''})['body'] += body
members = {}
for name, d in classes.items():
    if d['kind'] == 'enum':
        members[name] = set(re.findall(r'(\w+)\s*(?:,|=|$|\n)', d['body'])) | set(re.findall(r'\b(\w+)\b', d['body']))
    else:
        ms = set(re.findall(r'(\w+)\s*(?:\(|=|;|\{|,)', d['body']))
        ms |= set(re.findall(r'\b(\w+)\b\s*(?=;|,|=)', d['body']))
        ms |= set(re.findall(r'event\s+[\w<>\.]+\s+(\w+)', d['body']))
        members[name] = ms

static_like = ['GameData', 'Game', 'Mats', 'UIKit', 'WorldBuilder', 'Inp', 'FishModel', 'IconFactory', 'FishPreview', 'AudioManager']
singleton = {'AudioManager.I': 'AudioManager', 'FishingController.I': 'FishingController', 'GameManager.I': 'GameManager',
             'GameUI.I': 'GameUI', 'PlayerController.I': 'PlayerController', 'WorldBuilder.I': 'WorldBuilder'}
enums = ['FState', 'ItemCategory', 'Rarity']

for f, c in clean.items():
    b = os.path.basename(f)
    for cls in static_like:
        for m in re.finditer(r'(?<![\w\.])' + cls + r'\.(\w+)', c):
            mem = m.group(1)
            if mem == 'I' and cls in singleton.values(): continue
            if mem not in members.get(cls, set()): errors.append(f"{b}: {cls}.{mem} — не найден в классе {cls}")
    for key, cls in singleton.items():
        for m in re.finditer(re.escape(key) + r'\??\.(\w+)', c):
            if m.group(1) not in members.get(cls, set()): errors.append(f"{b}: {key}.{m.group(1)} — не найден в классе {cls}")
    for en in enums:
        for m in re.finditer(r'(?<![\w\.])' + en + r'\.(\w+)', c):
            if m.group(1) not in members.get(en, set()): errors.append(f"{b}: {en}.{m.group(1)} — нет такого значения enum")

# ---- данные
gd = open(os.path.join(root, 'Assets/Scripts/Data/GameData.cs'), encoding='utf-8').read()
fish = re.findall(r'\n\s*F\("(\w+)"', gd); items = re.findall(r'\n\s*[INLGM]\("(\w+)"', gd)
if len(fish) != 11: errors.append(f"рыб {len(fish)} вместо 11")
if len(items) < 37: errors.append(f"предметов {len(items)} меньше 37")
if len(set(items)) != len(items): errors.append("дубли id предметов")
for blk in re.findall(r'new\[\] \{([^}]*)\}', gd):
    for b_ in re.findall(r'"(\w+)"', blk):
        if b_ not in items and not b_.startswith('rod_') and b_ not in fish: errors.append(f"неизвестный id в списке наживок: {b_}")


# ---- секции магазина: у каждого товара с sub должна быть такая секция в своей категории
gd = clean.get(next((f for f in clean if f.endswith('GameData.cs')), ''), '')
raw = open(next(f for f in files if f.endswith('GameData.cs')), encoding='utf-8').read()
sec_block = raw[raw.find('sections = new Dictionary'):raw.find('public const string OtherSection')]
for m in re.finditer(r'I\("(\w+)",\s*"[^"]*",\s*ItemCategory\.(\w+).*?sub: "([^"]+)"', raw):
    iid, cat, sub = m.groups()
    cat_start = sec_block.find('ItemCategory.' + cat)
    if cat_start < 0 or '"' + sub + '"' not in sec_block:
        errors.append(f"товар {iid}: подкатегория «{sub}» не найдена среди секций категории {cat}")

# подкатегории у N(...) и L(...)
for m in re.finditer(r'\n\s*([NL])\("(\w+)", "[^"]*", "([^"]+)"', raw):
    kind, iid, sub = m.groups()
    if '"' + sub + '"' not in sec_block: errors.append(f"товар {iid}: подкатегория «{sub}» не найдена среди секций")

# рецепты и подкатегории G/M
for m in re.finditer(r'\n\s*R\("(\w+)", \d+, \d+, ([^;]*)\);', gd):
    for rid in [m.group(1)] + re.findall(r'"(\w+)", \d', m.group(2)):
        if rid not in items: errors.append(f"рецепт: неизвестный id {rid}")
for m in re.finditer(r'\n\s*([GM])\("(\w+)", "[^"]*", "([^"]+)"', raw):
    kind, iid, sub = m.groups()
    if '"' + sub + '"' not in sec_block: errors.append(f"товар {iid}: подкатегория «{sub}» не найдена среди секций")

print(f"Файлов: {len(files)}; классов/enum: {len(classes)}; рыб: {len(fish)}; предметов (без циклов): {len(items)}")
if errors:
    print("НАЙДЕНО ПРОБЛЕМ:", len(errors))
    for e in errors: print(" -", e)
    sys.exit(1)
print("Статическая проверка пройдена (это не компиляция!).")
