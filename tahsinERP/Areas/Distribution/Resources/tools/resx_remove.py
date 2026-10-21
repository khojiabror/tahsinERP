# -*- coding: utf-8 -*-
"""
DstStrings.resx va DstStrings.ru.resx dan ishlatilmaydigan kalitlarni o'chiradi; qolganlari va tartib saqlanadi.

    python resx_remove.py <strings.py>     # faylda REMOVE = ["Key", ...]
"""
import os
import re
import sys
import runpy

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.dirname(HERE)
DATA = re.compile(r'  <data name="([^"]+)" xml:space="preserve">\r?\n    <value>(.*?)</value>\r?\n  </data>\r?\n', re.S)


def remove(path, keys):
    text = open(path, encoding='utf-8-sig').read()
    removed = []

    def drop(m):
        if m.group(1) in keys:
            removed.append(m.group(1))
            return ''
        return m.group(0)

    text = DATA.sub(drop, text)
    open(path, 'w', encoding='utf-8').write(text)
    return removed


if __name__ == '__main__':
    keys = set(runpy.run_path(sys.argv[1]).get('REMOVE', []))
    for name in ('DstStrings.resx', 'DstStrings.ru.resx'):
        print(name, 'o\'chirildi:', ', '.join(remove(os.path.join(RES, name), keys)) or '-')
