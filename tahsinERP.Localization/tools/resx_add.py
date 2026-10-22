# -*- coding: utf-8 -*-
"""
Tarjima kalitlarini .resx to'plamiga qo'shadi yoki yangilaydi (qolganlari tegilmaydi, tartib saqlanadi).

    python resx_add.py <to'plam yo'li, .resx'siz> <strings.py> [<strings2.py> ...]

    misol: python resx_add.py ../../tahsinERP/Resources/ErpStrings strings_layout.py

strings.py faylida:
    STRINGS = {
        "Key": ("o'zbekcha (lotin)", "русский"),
        "Key2": ("...", "...", "кирилл"),   # ixtiyoriy: avtomatik transliteratsiya noto'g'ri bo'lsa -> .uz-Cyrl.resx
    }

Fayl yo'q bo'lsa — standart resx sarlavhasi bilan yaratiladi (csproj'ga EmbeddedResource qo'shishni unutmang).
"""
import os
import re
import sys
import runpy
from xml.sax.saxutils import escape

DATA = re.compile(r'  <data name="([^"]+)" xml:space="preserve">\r?\n    <value>(.*?)</value>\r?\n  </data>\r?\n', re.S)
HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''.replace('\n', '\r\n')
KEY = re.compile(r'^[A-Za-z][A-Za-z0-9_.]*$')


def update(path, strings, idx):
    pairs = {k: v[idx] for k, v in strings.items() if len(v) > idx and v[idx] is not None}
    if not pairs and not os.path.exists(path):
        return 0
    text = open(path, encoding='utf-8-sig').read() if os.path.exists(path) else HEADER + '</root>\r\n'
    nl = '\r\n' if '\r\n' in text else '\n'
    entries = {m.group(1): m.group(2) for m in DATA.finditer(text)}
    order = list(entries)
    first = DATA.search(text)
    head = text[:first.start()] if first else text[:text.rindex('</root>')]
    for key, value in pairs.items():
        if not KEY.match(key):
            raise SystemExit('Noto\'g\'ri kalit: %r' % key)
        if key not in entries:
            order.append(key)
        entries[key] = escape(value)
    body = ''.join('  <data name="%s" xml:space="preserve">%s    <value>%s</value>%s  </data>%s' % (k, nl, entries[k], nl, nl)
                   for k in order)
    open(path, 'w', encoding='utf-8', newline='').write(head + body + '</root>' + nl)
    return len(entries)


if __name__ == '__main__':
    base = sys.argv[1]
    if base.endswith('.resx'):
        base = base[:-5]
    strings = {}
    for f in sys.argv[2:]:
        for k, v in runpy.run_path(f)['STRINGS'].items():
            if k in strings and strings[k][:2] != v[:2]:
                raise SystemExit('Kalit ikki xil qiymat bilan: %s (%s)' % (k, f))
            strings[k] = v
    n = update(base + '.resx', strings, 0)
    update(base + '.ru.resx', strings, 1)
    update(base + '.uz-Cyrl.resx', strings, 2)
    print('%s: %d kalit (%d yangilandi/qo\'shildi)' % (os.path.basename(base), n, len(strings)))
