"""
Frest shablonining asosiy rangini (#5A8DEE va hosilalari) CSS o'zgaruvchilariga bog'laydigan fayllarni
yaratadi. Shablon fayllari (app-assets/) o'zgarmaydi; natija assets/css/ ga yoziladi va commit qilinadi.
Ishga tushirish (tahsinERP/ papkasidan):  python tools/theme/build_theme_css.py

  assets/css/theme-accent.css  — bootstrap/extended/components/colors/menu: faqat asosiy rang deklaratsiyalari
  assets/css/theme-dark.css    — themes/dark-layout.css nusxasi: body.dark-layout -> html[data-theme="dark"] body
"""
import io, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))

# Frest asosiy rangi va uning hosilalari -> o'zgaruvchi (color uchun --accent-text, qolganlari --accent)
HEX = {
    '#5a8dee': 'accent',
    '#3775ea': 'hover', '#2c6de9': 'hover', '#2065e8': 'active', '#175ee4': 'active',
    '#719df0': 'accent', '#e2ecff': 'soft',
}
RGBA = re.compile(r'rgba\(\s*90\s*,\s*141\s*,\s*238\s*,\s*[0-9.]+\s*\)', re.I)
HEXRE = re.compile(r'#[0-9a-fA-F]{6}\b')


def var_for(kind, prop):
    if kind == 'soft':
        return 'var(--accent-soft)'
    if prop == 'color' and kind == 'accent':
        return 'var(--accent-text)'
    if prop == 'color':
        return 'var(--accent-text-hover)'
    return {'accent': 'var(--accent)', 'hover': 'var(--accent-hover)', 'active': 'var(--accent-active)'}[kind]


def map_value(prop, value):
    """Asosiy rang bo'lsa o'zgaruvchili qiymat, aks holda None."""
    hit = False

    def rgba(m):
        nonlocal hit
        hit = True
        return 'var(--accent-ring)' if 'shadow' in prop else 'var(--accent-soft)'

    value = RGBA.sub(rgba, value)

    def hx(m):
        nonlocal hit
        k = HEX.get(m.group(0).lower())
        if not k:
            return m.group(0)
        hit = True
        return var_for(k, prop)

    value = HEXRE.sub(hx, value)
    return value if hit else None


def strip_comments(css):
    return re.sub(r'/\*.*?\*/', '', css, flags=re.S)


def parse(css):
    """(prelude, body) bloklari; @media ichidagilar rekursiv."""
    i, n, out = 0, len(css), []
    while i < n:
        j = css.find('{', i)
        if j < 0:
            break
        prelude = css[i:j].strip()
        depth, k = 1, j + 1
        while k < n and depth:
            if css[k] == '{':
                depth += 1
            elif css[k] == '}':
                depth -= 1
            k += 1
        body = css[j + 1:k - 1]
        if prelude.startswith('@media') or prelude.startswith('@supports'):
            out.append((prelude, parse(body)))
        elif not prelude.startswith('@'):
            out.append((prelude, body))
        i = k
    return out


def accent_rules(blocks):
    res = []
    for prelude, body in blocks:
        if isinstance(body, list):
            inner = accent_rules(body)
            if inner:
                res.append((prelude, inner))
            continue
        decls = []
        for d in body.split(';'):
            if ':' not in d:
                continue
            prop, val = d.split(':', 1)
            prop = prop.strip().lower()
            if prop == '--blue':
                continue
            mapped = map_value(prop, val.strip())
            if mapped is not None:
                decls.append('  %s: %s;' % (prop, mapped))
        if decls:
            res.append((prelude, decls))
    return res


def render(rules, indent=''):
    lines = []
    for prelude, body in rules:
        if body and isinstance(body[0], tuple):
            lines.append(indent + prelude + ' {')
            lines.extend(render(body, indent + '  '))
            lines.append(indent + '}')
        else:
            lines.append(indent + re.sub(r'\s*\n\s*', ' ', prelude) + ' {')
            lines.extend(indent + d for d in body)
            lines.append(indent + '}')
    return lines


def read(rel):
    with io.open(os.path.join(ROOT, rel), encoding='utf-8') as f:
        return f.read()


def write(rel, text):
    with io.open(os.path.join(ROOT, rel), 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


HEADER = '/* GENERATSIYA QILINGAN — qo\'lda tahrir qilmang: python tools/theme/build_theme_css.py\n   Manba: %s */\n\n'

SOURCES = [
    'app-assets/css/bootstrap.css',
    'app-assets/css/bootstrap-extended.css',
    'app-assets/css/colors.css',
    'app-assets/css/components.css',
    'app-assets/css/core/menu/menu-types/vertical-menu.css',
]


def main():
    lines = []
    for src in SOURCES:
        rules = accent_rules(parse(strip_comments(read(src))))
        lines.append('/* --- %s --- */' % src)
        lines.extend(render(rules))
        lines.append('')
    write('assets/css/theme-accent.css', HEADER % ', '.join(SOURCES) + '\n'.join(lines))

    dark = read('app-assets/css/themes/dark-layout.css')
    dark = dark.replace('body.dark-layout', 'html[data-theme="dark"] body')

    def dark_decl(m):
        prop, val = m.group(1), m.group(2)
        mapped = map_value(prop.strip().lower(), val)
        return '%s: %s;' % (prop, mapped if mapped is not None else val)

    dark = re.sub(r'([a-zA-Z-]+)\s*:\s*([^;{}]*?(?:#5A8DEE|#5a8dee|rgba\(\s*90\s*,\s*141[^)]*\))[^;{}]*);', dark_decl, dark)
    write('assets/css/theme-dark.css', HEADER % 'app-assets/css/themes/dark-layout.css' + dark)


if __name__ == '__main__':
    main()
