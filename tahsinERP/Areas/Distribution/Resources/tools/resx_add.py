# -*- coding: utf-8 -*-
"""
DstStrings.resx (uz, asosiy) va DstStrings.ru.resx ga kalitlarni qo'shadi yoki yangilaydi; qolganlari tegilmaydi.
Mavjud kalitlar tartibi saqlanadi, yangilari oxiriga qo'shiladi (diff toza bo'lsin).

    python resx_add.py <strings.py>     # faylda STRINGS = {"Key": ("uz", "ru"), ...}

Resources papkasi — shu skriptning yuqorisidagi papka.
"""
import os
import re
import sys
import runpy
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.dirname(HERE)
DATA = re.compile(r'  <data name="([^"]+)" xml:space="preserve">\r?\n    <value>(.*?)</value>\r?\n  </data>\r?\n', re.S)


def update(path, strings, idx):
    text = open(path, encoding='utf-8-sig').read()
    nl = '\r\n' if '\r\n' in text else '\n'
    entries = {m.group(1): m.group(2) for m in DATA.finditer(text)}
    order = list(entries)
    first = DATA.search(text)
    head = text[:first.start()] if first else text[:text.rindex('</root>')]
    for key, values in strings.items():
        if key not in entries:
            order.append(key)
        entries[key] = escape(values[idx])
    body = ''.join('  <data name="%s" xml:space="preserve">%s    <value>%s</value>%s  </data>%s' % (k, nl, entries[k], nl, nl)
                   for k in order)
    open(path, 'w', encoding='utf-8').write(head + body + '</root>' + nl)
    return len(entries)


if __name__ == '__main__':
    strings = runpy.run_path(sys.argv[1])['STRINGS']
    n = update(os.path.join(RES, 'DstStrings.resx'), strings, 0)
    update(os.path.join(RES, 'DstStrings.ru.resx'), strings, 1)
    print('%d kalit (%d yangilandi/qo\'shildi)' % (n, len(strings)))
