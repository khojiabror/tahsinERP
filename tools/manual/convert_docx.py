# -*- coding: utf-8 -*-
"""Word qo'llanmalarini (modullar/<NN>-<slug>/<NN>-<slug>_{uz,ru}.docx) ERP "Qo'llanma" sahifasi uchun HTML'ga aylantiradi.

Ishlatish (repo ildizidan):
    python tools/manual/convert_docx.py --src "C:/.../Docs/Qollanma/modullar"
    python tools/manual/convert_docx.py --src ... --only 03          # faqat bitta modul
    python tools/manual/convert_docx.py --src ... --only 03 --lang ru

Natija (standart: tahsinERP/ManualData):
    <lang>/<NN>-<slug>.html  — modul kontenti (fragment: <style> palitra + <div class="wm-doc">)
    img/<hash>.webp          — rasmlar (kontent xeshi bo'yicha nom: ikki til bir xil rasmni bo'lishadi)
    manifest.json            — modullar ro'yxati va sarlavhalari

Idempotent: docx o'zgarsa qayta ishga tushirish yetadi. Tayyor rasmlar qayta kodlanmaydi, hech bir HTML
ishlatmaydigan rasmlar o'chiriladi. Titul va Word mundarijasi tashlanadi — kontent birinchi Heading 1 dan.

Word bilan 1:1 bo'lishi uchun hamma o'lcham pt da (brauzerda 1pt = Word'dagi 1pt, 100% masshtab),
paragraf oraliqlari Word kabi max(after, before) (CSS margin collapse), ranglar esa tungi rejim uchun
CSS o'zgaruvchilari orqali beriladi (--f-XXXXXX matn, --b-XXXXXX fon, --l-XXXXXX chiziq).
"""
import argparse
import colorsys
import hashlib
import html
import io
import json
import os
import re
import sys
import zipfile

from lxml import etree
from PIL import Image

W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
A = 'http://schemas.openxmlformats.org/drawingml/2006/main'
WP = 'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing'
PR = 'http://schemas.openxmlformats.org/package/2006/relationships'
NS = {'w': W, 'r': R, 'a': A, 'wp': WP}

MAX_IMG_WIDTH = 1400
WEBP_QUALITY = 80               # 1300px screenshot matni aniq o'qiladi, ~40 KB/rasm
IMG_TOKEN = '@@IMG@@/'          # kontroller beradigan URL bilan almashtiriladi
LANGS = ('uz', 'ru')


def q(tag):
    p, t = tag.split(':')
    return '{%s}%s' % (NS[p], t)


def wval(el, attr='val'):
    return None if el is None else el.get(q('w:' + attr))


def on(el):
    """Word toggle xossasi (<w:b/>, <w:b w:val="0"/>)."""
    if el is None:
        return None
    v = el.get(q('w:val'))
    return v not in ('0', 'false', 'off')


def twip(v):
    return int(float(v)) / 20.0 if v not in (None, '') else None


def fmt(n):
    s = ('%.2f' % n).rstrip('0').rstrip('.')
    return s if s not in ('-0', '') else '0'


# ---------------------------------------------------------------- xossalar

def parse_rpr(rpr):
    d = {}
    if rpr is None:
        return d
    for k in ('b', 'i', 'strike', 'caps', 'smallCaps', 'vanish'):
        v = on(rpr.find(q('w:' + k)))
        if v is not None:
            d[k] = v
    u = rpr.find(q('w:u'))
    if u is not None:
        d['u'] = wval(u) not in (None, 'none')
    c = rpr.find(q('w:color'))
    if c is not None and wval(c):
        d['color'] = wval(c).upper()
    sz = rpr.find(q('w:sz'))
    if sz is not None and wval(sz):
        d['sz'] = int(wval(sz)) / 2.0
    f = rpr.find(q('w:rFonts'))
    if f is not None and (f.get(q('w:ascii')) or f.get(q('w:hAnsi'))):
        d['font'] = f.get(q('w:ascii')) or f.get(q('w:hAnsi'))
    shd = rpr.find(q('w:shd'))
    if shd is not None and wval(shd, 'fill') not in (None, 'auto'):
        d['shd'] = wval(shd, 'fill').upper()
    hl = rpr.find(q('w:highlight'))
    if hl is not None and wval(hl) not in (None, 'none'):
        d['shd'] = HIGHLIGHT.get(wval(hl), 'FFFF00')
    va = rpr.find(q('w:vertAlign'))
    if va is not None:
        d['va'] = wval(va)
    return d


HIGHLIGHT = {'yellow': 'FFFF00', 'green': '00FF00', 'cyan': '00FFFF', 'magenta': 'FF00FF', 'blue': '0000FF',
             'red': 'FF0000', 'darkBlue': '000080', 'darkCyan': '008080', 'darkGreen': '008000',
             'darkMagenta': '800080', 'darkRed': '800000', 'darkYellow': '808000', 'darkGray': '808080',
             'lightGray': 'C0C0C0', 'black': '000000', 'white': 'FFFFFF'}


def parse_border(el):
    if el is None:
        return None
    v = wval(el)
    if v in (None, 'nil', 'none'):
        return ('none',)
    sz = int(el.get(q('w:sz')) or 4) / 8.0
    color = (el.get(q('w:color')) or 'auto').upper()
    return ('line', max(sz, 0.5), color, 'double' if v == 'double' else ('dashed' if 'dash' in v else ('dotted' if 'dot' in v else 'solid')),
            int(el.get(q('w:space')) or 0))


def parse_ppr(ppr):
    d = {}
    if ppr is None:
        return d
    jc = ppr.find(q('w:jc'))
    if jc is not None:
        d['jc'] = wval(jc)
    sp = ppr.find(q('w:spacing'))
    if sp is not None:
        for k in ('before', 'after', 'line'):
            if sp.get(q('w:' + k)) is not None:
                d['sp_' + k] = int(sp.get(q('w:' + k)))
        if sp.get(q('w:lineRule')):
            d['sp_rule'] = sp.get(q('w:lineRule'))
    ind = ppr.find(q('w:ind'))
    if ind is not None:
        for k, alias in (('left', 'start'), ('right', 'end'), ('hanging', None), ('firstLine', None)):
            v = ind.get(q('w:' + k)) or (alias and ind.get(q('w:' + alias)))
            if v is not None:
                d['ind_' + k] = int(v)
        if 'ind_hanging' in d:
            d.pop('ind_firstLine', None)
        elif 'ind_firstLine' in d:
            d['ind_hanging'] = 0
    shd = ppr.find(q('w:shd'))
    if shd is not None and wval(shd, 'fill') not in (None, 'auto'):
        d['shd'] = wval(shd, 'fill').upper()
    bdr = ppr.find(q('w:pBdr'))
    if bdr is not None:
        for side in ('top', 'left', 'bottom', 'right'):
            b = parse_border(bdr.find(q('w:' + side)))
            if b:
                d['bdr_' + side] = b
    num = ppr.find(q('w:numPr'))
    if num is not None:
        nid = num.find(q('w:numId'))
        lvl = num.find(q('w:ilvl'))
        if nid is not None:
            d['numId'] = wval(nid)
        if lvl is not None:
            d['ilvl'] = int(wval(lvl))
    for k in ('pageBreakBefore', 'contextualSpacing'):
        v = on(ppr.find(q('w:' + k)))
        if v is not None:
            d[k] = v
    ol = ppr.find(q('w:outlineLvl'))
    if ol is not None:
        d['outline'] = int(wval(ol))
    return d


class Styles:
    def __init__(self, xml):
        self.p_def, self.r_def = {}, {}
        self.styles = {}
        self.default_para = 'Normal'
        if xml is None:
            return
        root = etree.fromstring(xml)
        dd = root.find(q('w:docDefaults'))
        if dd is not None:
            self.r_def = parse_rpr(dd.find('w:rPrDefault/w:rPr', NS))
            self.p_def = parse_ppr(dd.find('w:pPrDefault/w:pPr', NS))
        for s in root.findall(q('w:style')):
            sid = s.get(q('w:styleId'))
            based = s.find(q('w:basedOn'))
            tbl = s.find(q('w:tblPr'))
            self.styles[sid] = {
                'type': s.get(q('w:type')), 'based': wval(based),
                'p': parse_ppr(s.find(q('w:pPr'))), 'r': parse_rpr(s.find(q('w:rPr'))),
                'tbl': tbl, 'tcpr': s.find(q('w:tcPr')),
            }
            if s.get(q('w:type')) == 'paragraph' and s.get(q('w:default')) in ('1', 'true'):
                self.default_para = sid
        self._cache = {}

    def chain(self, sid):
        out, seen = [], set()
        while sid and sid in self.styles and sid not in seen:
            seen.add(sid)
            out.append(self.styles[sid])
            sid = self.styles[sid]['based']
        return list(reversed(out))

    def para(self, sid):
        sid = sid or self.default_para
        if sid not in self._cache:
            p, r = dict(self.p_def), dict(self.r_def)
            for s in self.chain(sid):
                p.update(s['p'])
                r.update(s['r'])
            self._cache[sid] = (p, r)
        return self._cache[sid]

    def char(self, sid):
        r = {}
        for s in self.chain(sid):
            r.update(s['r'])
        return r

    def table_borders(self, sid):
        d = {}
        for s in self.chain(sid):
            if s['tbl'] is not None:
                d.update(table_pr(s['tbl']))
        return d


def table_pr(tblpr):
    d = {}
    if tblpr is None:
        return d
    b = tblpr.find(q('w:tblBorders'))
    if b is not None:
        for side in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV'):
            v = parse_border(b.find(q('w:' + side)) if b.find(q('w:' + side)) is not None else b.find(q('w:' + {'left': 'start', 'right': 'end'}.get(side, side))))
            if v:
                d['b_' + side] = v
    m = tblpr.find(q('w:tblCellMar'))
    if m is not None:
        for side in ('top', 'left', 'bottom', 'right'):
            e = m.find(q('w:' + side))
            if e is None:
                e = m.find(q('w:' + {'left': 'start', 'right': 'end'}.get(side, side)))
            if e is not None:
                d['m_' + side] = int(e.get(q('w:w')) or 0)
    return d


# ---------------------------------------------------------------- raqamlash

def to_roman(n):
    vals = [(1000, 'm'), (900, 'cm'), (500, 'd'), (400, 'cd'), (100, 'c'), (90, 'xc'), (50, 'l'), (40, 'xl'),
            (10, 'x'), (9, 'ix'), (5, 'v'), (4, 'iv'), (1, 'i')]
    s = ''
    for v, r in vals:
        while n >= v:
            s += r
            n -= v
    return s


def to_letter(n):
    s = ''
    while n > 0:
        n -= 1
        s = chr(97 + n % 26) + s
        n //= 26
    return s


BULLET_MAP = {'\uf0b7': '\u2022', '\uf0a7': '\u25aa', '\uf0d8': '\u27a2', '\uf0fc': '\u2714', '\uf076': '\u2756',
              '\uf06e': '\u25a0', '\uf0a8': '\u25a1', '\uf0e0': '\u2192', '\uf0d0': '\u2013'}


class Numbering:
    def __init__(self, xml):
        self.abstract, self.nums = {}, {}
        self.counters = {}
        self.started = set()
        if xml is None:
            return
        root = etree.fromstring(xml)
        for an in root.findall(q('w:abstractNum')):
            lv = {}
            for l in an.findall(q('w:lvl')):
                il = int(l.get(q('w:ilvl')))
                lv[il] = {
                    'start': int(wval(l.find(q('w:start'))) or 1) if l.find(q('w:start')) is not None else 1,
                    'fmt': wval(l.find(q('w:numFmt'))) or 'decimal',
                    'text': wval(l.find(q('w:lvlText'))) if l.find(q('w:lvlText')) is not None else '',
                    'p': parse_ppr(l.find(q('w:pPr'))), 'r': parse_rpr(l.find(q('w:rPr'))),
                    'suff': wval(l.find(q('w:suff'))) or 'tab',
                }
            self.abstract[an.get(q('w:abstractNumId'))] = lv
        for n in root.findall(q('w:num')):
            ov = {}
            for o in n.findall(q('w:lvlOverride')):
                so = o.find(q('w:startOverride'))
                if so is not None:
                    ov[int(o.get(q('w:ilvl')))] = int(wval(so))
            self.nums[n.get(q('w:numId'))] = (wval(n.find(q('w:abstractNumId'))), ov)

    def level(self, num_id, ilvl):
        if num_id not in self.nums:
            return None
        aid, _ = self.nums[num_id]
        return self.abstract.get(aid, {}).get(ilvl)

    def next_marker(self, num_id, ilvl):
        """Raqamni oshiradi va marker matnini qaytaradi (Word kabi: abstractNum bo'yicha davom, startOverride — qaytadan)."""
        if num_id not in self.nums:
            return None, None
        aid, ov = self.nums[num_id]
        levels = self.abstract.get(aid, {})
        lv = levels.get(ilvl)
        if lv is None:
            return None, None
        cnt = self.counters.setdefault(aid, {})
        if num_id not in self.started:
            self.started.add(num_id)
            for il, st in ov.items():
                cnt.pop(il, None)
                cnt['_ov%d' % il] = st
        if ilvl in cnt:
            cnt[ilvl] += 1
        else:
            cnt[ilvl] = cnt.pop('_ov%d' % ilvl, lv['start'])
        for deeper in [k for k in cnt if isinstance(k, int) and k > ilvl]:
            del cnt[deeper]
        text = lv['text'] or ''
        if lv['fmt'] == 'bullet':
            m = ''.join(BULLET_MAP.get(ch, ch) for ch in text)
            if m in ('o',):
                m = '\u25e6'
            return m or '\u2022', lv
        if lv['fmt'] == 'none':
            return '', lv

        def rep(mo):
            il = int(mo.group(1)) - 1
            l2 = levels.get(il, lv)
            v = cnt.get(il, l2['start'])
            f = l2['fmt']
            if f == 'lowerLetter':
                return to_letter(v)
            if f == 'upperLetter':
                return to_letter(v).upper()
            if f == 'lowerRoman':
                return to_roman(v)
            if f == 'upperRoman':
                return to_roman(v).upper()
            if f == 'decimalZero':
                return '%02d' % v
            return str(v)
        return re.sub(r'%(\d)', rep, text), lv


# ---------------------------------------------------------------- ranglar (kunduzgi/tungi)

class Palette:
    """Har rang uchun CSS o'zgaruvchi: kunduzgi qiymat — Word'dagi, tungi — yorug'lik teskari (kontrast saqlanadi)."""

    def __init__(self):
        self.used = {}

    def var(self, kind, hexv):
        hexv = (hexv or '').upper()
        if hexv in ('AUTO', ''):
            return 'var(--wm-text)' if kind != 'b' else 'transparent'
        if not re.fullmatch(r'[0-9A-F]{6}', hexv):
            return 'inherit'
        name = '--%s-%s' % (kind, hexv)
        self.used[name] = (kind, hexv)
        return 'var(%s)' % name

    @staticmethod
    def dark(kind, hexv):
        r, g, b = (int(hexv[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
        h, l, s = colorsys.rgb_to_hls(r, g, b)
        if kind == 'f':                         # matn: qorong'i -> yorug'
            if l < 0.62:
                l = max(0.70, min(0.90, 1.0 - l * 0.55))
                s = s * 0.85
        elif kind == 'b':                       # fon: oqish -> to'q, to'yingan o'rta ranglar qoladi
            if l > 0.62:
                l = 0.13 + (1.0 - l) * 0.55
                s = s * 0.55
        else:                                   # chiziq
            if l > 0.62:
                l = 0.30 + (1.0 - l) * 0.3
                s = s * 0.6
            elif l < 0.25:
                l = 0.45
        r, g, b = colorsys.hls_to_rgb(h, l, s)
        return '#%02X%02X%02X' % (round(r * 255), round(g * 255), round(b * 255))

    def css(self):
        if not self.used:
            return ''
        light = ';'.join('%s:#%s' % (n, h) for n, (k, h) in sorted(self.used.items()))
        dark = ';'.join('%s:%s' % (n, self.dark(k, h)) for n, (k, h) in sorted(self.used.items()))
        return '<style>.wm-doc{%s}html[data-theme="dark"] .wm-doc{%s}</style>' % (light, dark)


# ---------------------------------------------------------------- shriftlar

MONO = ('consolas', 'courier new', 'courier', 'lucida console', 'cascadia code', 'cascadia mono', 'menlo', 'monaco')


def font_stack(name):
    if not name:
        return None
    n = name.lower()
    if n in MONO or 'mono' in n:
        return "'%s',Consolas,'Courier New',monospace" % name
    if n in ('calibri', 'calibri light'):
        return "'%s',Carlito,'Segoe UI',Arial,sans-serif" % name
    if n in ('cambria', 'times new roman', 'georgia', 'cambria math'):
        return "'%s',Georgia,serif" % name
    return "'%s','Segoe UI',Arial,sans-serif" % name


# ---------------------------------------------------------------- konvertor

class Converter:
    def __init__(self, path, img_dir, log):
        self.path = path
        self.img_dir = img_dir
        self.log = log
        self.z = zipfile.ZipFile(path)
        names = set(self.z.namelist())
        rd = lambda n: self.z.read(n) if n in names else None
        self.styles = Styles(rd('word/styles.xml'))
        self.numbering = Numbering(rd('word/numbering.xml'))
        self.rels = {}
        rx = rd('word/_rels/document.xml.rels')
        if rx is not None:
            for r in etree.fromstring(rx).findall('{%s}Relationship' % PR):
                self.rels[r.get('Id')] = (r.get('Target'), r.get('TargetMode'))
        self.doc = etree.fromstring(self.z.read('word/document.xml'))
        self.core = rd('docProps/core.xml')
        self.palette = Palette()
        self.images = []
        self.hcount = 0
        self.base_font = None
        self.text_width = 481.9
        sect = self.doc.find('w:body/w:sectPr', NS)
        if sect is not None:
            pgsz, pgmar = sect.find(q('w:pgSz')), sect.find(q('w:pgMar'))
            if pgsz is not None and pgmar is not None:
                self.page_width = twip(pgsz.get(q('w:w')))
                self.margin_l = twip(pgmar.get(q('w:left')) or pgmar.get(q('w:start')))
                self.margin_r = twip(pgmar.get(q('w:right')) or pgmar.get(q('w:end')))
                self.text_width = self.page_width - self.margin_l - self.margin_r

    # --- sarlavha
    def title(self, num=None):
        """Titul sahifasidagi modul nomi: "NN. ..." bilan boshlangan paragraf, bo'lmasa eng katta shriftli."""
        body = self.doc.find(q('w:body'))
        best, best_sz = None, 0
        for p in body.iter(q('w:p')):
            pp, _ = self.p_props(p)
            if pp.get('outline') == 0:
                break
            txt = self.plain(p).strip()
            if not txt:
                continue
            if num and re.match(r'^%s\.\s' % num, txt):
                return txt
            if txt.lower() == 'tahsinerp':      # titul logotipi
                continue
            szs = [parse_rpr(r.find(q('w:rPr'))).get('sz', 0) for r in p.iter(q('w:r'))]
            sz = max(szs or [0]) or self.styles.para(self.p_style(p))[1].get('sz', 0)
            if sz > best_sz:
                best, best_sz = txt, sz
        if best:
            if not num:
                return best
            # "9. ...", "10-modul. ...", "Модуль 10. ..." -> "NN. ..."
            best = re.sub(r'^\d+\s*(-?\s*modul)?\s*[.:]\s*', '', best, flags=re.I)
            best = re.sub(r'^(modul|модуль)\s*\d+\s*[.:]\s*', '', best, flags=re.I)
            return '%s. %s' % (num, best)
        if self.core:
            t = etree.fromstring(self.core).find('{http://purl.org/dc/elements/1.1/}title')
            if t is not None and t.text:
                return t.text.strip()
        return os.path.basename(self.path)

    def plain(self, el):
        out = []
        for t in el.iter(q('w:t'), q('w:tab')):
            out.append(t.text or '' if t.tag == q('w:t') else ' ')
        return ''.join(out)

    def p_style(self, p):
        ps = p.find('w:pPr/w:pStyle', NS)
        return wval(ps)

    def p_props(self, p):
        sid = self.p_style(p)
        sp, sr = self.styles.para(sid)
        pp = dict(sp)
        direct = parse_ppr(p.find(q('w:pPr')))
        pp.update(direct)
        return pp, dict(sr)

    # --- asosiy
    def convert(self):
        body = self.doc.find(q('w:body'))
        _, nr = self.styles.para(None)
        self.base_size = nr.get('sz', 11)
        self.base_font = nr.get('font', 'Calibri')
        self.base_color = nr.get('color')
        started = False
        out = []
        for el in self.blocks(body):
            if not started:
                if el.tag == q('w:p') and self.p_props(el)[0].get('outline') == 0 and self.plain(el).strip():
                    started = True
                else:
                    if el.tag == q('w:p'):
                        self.track_numbering(el)
                    continue
            out.append(self.block(el))
        content, classes = classify('\n'.join(x for x in out if x))
        style = 'font-family:%s;font-size:%spt;max-width:%spt' % (font_stack(self.base_font), fmt(self.base_size), fmt(self.text_width))
        if self.base_color:
            style += ';--wm-text:%s' % self.palette.var('f', self.base_color)
        return self.palette.css() + '\n<style>%s</style>\n<div class="wm-doc" style="%s">\n%s\n</div>\n' % (classes, style, content)

    def track_numbering(self, p):
        pp, _ = self.p_props(p)
        if pp.get('numId') not in (None, '0'):
            self.numbering.next_marker(pp['numId'], pp.get('ilvl', 0))

    def blocks(self, parent):
        """Blok elementlar (p, tbl) — sdt (masalan Word mundarijasi) ichidagilari ham, mundarija tashlanadi."""
        for el in parent:
            if el.tag == q('w:sdt'):
                gal = el.find('w:sdtPr/w:docPartObj/w:docPartGallery', NS)
                if gal is not None and 'Contents' in (wval(gal) or ''):
                    continue
                c = el.find(q('w:sdtContent'))
                if c is not None:
                    yield from self.blocks(c)
            elif el.tag in (q('w:p'), q('w:tbl')):
                if el.tag == q('w:p') and (self.p_style(el) or '').upper().startswith('TOC'):
                    continue
                yield el
            elif el.tag in (q('w:customXml'), q('w:ins')):
                yield from self.blocks(el)

    def block(self, el):
        if el.tag == q('w:tbl'):
            return self.table(el)
        return self.paragraph(el)

    # --- paragraf
    def neighbor_style(self, p, nxt):
        sib = p.getnext() if nxt else p.getprevious()
        while sib is not None and sib.tag not in (q('w:p'), q('w:tbl')):
            sib = sib.getnext() if nxt else sib.getprevious()
        if sib is None or sib.tag != q('w:p'):
            return '\0'
        return self.p_style(sib) or self.styles.default_para

    def paragraph(self, p, in_cell=False):
        pp, pr = self.p_props(p)
        sid = self.p_style(p) or self.styles.default_para
        mark_r = parse_rpr(p.find('w:pPr/w:rPr', NS))
        # raqamlash
        marker_html = ''
        num_id = pp.get('numId')
        if num_id not in (None, '0'):
            ilvl = pp.get('ilvl', 0)
            lv = self.numbering.level(num_id, ilvl)
            if lv:
                direct = parse_ppr(p.find(q('w:pPr')))
                for k, v in lv['p'].items():
                    if k.startswith('ind_') and k not in direct:
                        pp[k] = v
                if 'ind_hanging' in lv['p'] and 'ind_hanging' not in direct and 'ind_firstLine' not in direct:
                    pp.pop('ind_firstLine', None)
                text, lv = self.numbering.next_marker(num_id, ilvl)
                mr = dict(pr)
                mr.update(mark_r)
                mr.pop('u', None)
                mr.update(lv['r'])
                hang = twip(pp.get('ind_hanging', 360)) or 0
                ms = self.run_css(mr, pr)
                if lv['r'].get('font') and lv['fmt'] == 'bullet':
                    ms = ms.replace('font-family:%s;' % font_stack(lv['r']['font']), '')
                marker_html = '<span class="wm-num" style="min-width:%spt;%s">%s</span>' % (
                    fmt(hang), ms, html.escape(text))
        # Word satr balandligini ishlatilgan shriftdan oladi: hamma run bir o'lchamda bo'lsa (masalan katakda 10pt),
        # paragrafga ham shu o'lcham beriladi — aks holda CSS 11pt "strut" qatorni balandlashtiradi
        sizes = set()
        for r in p.iter(q('w:r')):
            if r.find(q('w:t')) is None:
                continue
            rr = dict(pr)
            rpr_el = r.find(q('w:rPr'))
            rs = rpr_el.find(q('w:rStyle')) if rpr_el is not None else None
            if rs is not None:
                rr.update(self.styles.char(wval(rs)))
            rr.update(parse_rpr(rpr_el))
            sizes.add(rr.get('sz', self.base_size))
        if len(sizes) == 1:
            pr['sz'] = sizes.pop()
        elif not sizes and mark_r.get('sz'):
            pr['sz'] = mark_r['sz']
        # matn
        inner = self.runs(p, pr)
        is_empty = not inner.strip() and not marker_html
        # uslub
        css = []
        before, after = pp.get('sp_before', 0), pp.get('sp_after', 0)
        if pp.get('contextualSpacing'):
            if self.neighbor_style(p, False) == sid:
                before = 0
            if self.neighbor_style(p, True) == sid:
                after = 0
        if before:
            css.append('margin-top:%spt' % fmt(before / 20.0))
        if after:
            css.append('margin-bottom:%spt' % fmt(after / 20.0))
        size = pr.get('sz', self.base_size)
        if 'sp_line' in pp:
            rule = pp.get('sp_rule', 'auto')
            if rule == 'auto':
                css.append('line-height:%s' % fmt(pp['sp_line'] / 240.0 * 1.22))
            elif rule == 'exact':
                css.append('line-height:%spt' % fmt(pp['sp_line'] / 20.0))
            else:
                css.append('line-height:max(%spt,1.22em)' % fmt(pp['sp_line'] / 20.0))
        left = twip(pp.get('ind_left')) or 0
        right = twip(pp.get('ind_right')) or 0
        hang = twip(pp.get('ind_hanging')) or 0
        first = twip(pp.get('ind_firstLine')) or 0
        if left:
            css.append('padding-left:%spt' % fmt(left))
        if right:
            css.append('padding-right:%spt' % fmt(right))
        if hang:
            css.append('text-indent:-%spt' % fmt(hang))
        elif first:
            css.append('text-indent:%spt' % fmt(first))
        jc = pp.get('jc')
        if jc in ('center', 'right', 'both', 'end', 'distribute'):
            css.append('text-align:%s' % {'both': 'justify', 'end': 'right', 'distribute': 'justify'}.get(jc, jc))
        if pp.get('shd'):
            css.append('background:%s' % self.palette.var('b', pp['shd']))
        for side in ('top', 'left', 'bottom', 'right'):
            b = pp.get('bdr_' + side)
            if b and b[0] == 'line':
                css.append('border-%s:%spt %s %s' % (side, fmt(b[1]), b[3], self.palette.var('l', b[2])))
                css.append('padding-%s:%spt' % (side, fmt(b[4] + (left if side == 'left' else 0))))
        css.append(self.run_css(pr, None, para=True).rstrip(';'))
        tag, attrs = 'p', ''
        outline = pp.get('outline')
        if outline is not None and outline <= 2 and not in_cell and not is_empty:
            self.hcount += 1
            tag = 'h%d' % (outline + 1)
            attrs = ' id="s%d"' % self.hcount
        cls = []
        if pp.get('pageBreakBefore') and tag == 'h1':
            cls.append('wm-pb')
        if marker_html:
            cls.append('wm-li')
        css = ';'.join(c for c in css if c)
        return '<%s%s%s style="%s">%s%s</%s>' % (
            tag, attrs, (' class="%s"' % ' '.join(cls)) if cls else '', css,
            marker_html, inner if not is_empty else '<br>', tag)

    def run_css(self, r, base, para=False):
        """Run xossalari -> CSS. base — paragrafdagi standart (farqlarigina yoziladi)."""
        base = base or {}
        css = []

        def diff(k, default=None):
            return r.get(k, default) != base.get(k, default)
        if para or diff('b', False):
            css.append('font-weight:%s' % ('700' if r.get('b') else '400'))
        if para or diff('i', False):
            css.append('font-style:%s' % ('italic' if r.get('i') else 'normal'))
        if (para and r.get('sz') and r.get('sz') != self.base_size) or (not para and diff('sz', self.base_size)):
            css.append('font-size:%spt' % fmt(r.get('sz', self.base_size)))
        if (para and r.get('color')) or (not para and diff('color')):
            css.append('color:%s' % self.palette.var('f', r.get('color') or 'auto'))
        if (para and r.get('font') and r['font'] != self.base_font) or (not para and diff('font', self.base_font)):
            fs = font_stack(r.get('font') or self.base_font)
            if fs:
                css.append('font-family:%s' % fs)
        deco = []
        if r.get('u'):
            deco.append('underline')
        if r.get('strike'):
            deco.append('line-through')
        if deco and (para or diff('u', False) or diff('strike', False)):
            css.append('text-decoration:%s' % ' '.join(deco))
        elif not para and (diff('u', False) or diff('strike', False)):
            css.append('text-decoration:none')
        if r.get('shd') and (para or diff('shd')):
            css.append('background:%s' % self.palette.var('b', r['shd']))
        if r.get('caps'):
            css.append('text-transform:uppercase')
        if r.get('smallCaps'):
            css.append('font-variant:small-caps')
        if r.get('va') in ('superscript', 'subscript') and not para:
            css.append('vertical-align:%s;font-size:%spt' % ('super' if r['va'] == 'superscript' else 'sub', fmt(r.get('sz', self.base_size) * 0.65)))
        return ';'.join(css) + (';' if css else '')

    def runs(self, p, pr):
        """Paragraf ichidagi run'lar -> HTML (bir xil formatdagi qo'shni bo'laklar bitta span'ga)."""
        segs = []          # (css, html, link)
        state = {'fld': []}

        def walk(parent, link=None):
            for ch in parent:
                t = ch.tag
                if t == q('w:r'):
                    self.run(ch, pr, segs, state, link)
                elif t == q('w:hyperlink'):
                    href = None
                    if ch.get(q('w:anchor')):
                        href = '#' + bm_id(ch.get(q('w:anchor')))
                    elif ch.get(q('r:id')) in self.rels:
                        href = self.rels[ch.get(q('r:id'))][0]
                    walk(ch, href)
                elif t == q('w:bookmarkStart'):
                    name = ch.get(q('w:name')) or ''
                    if name and not name.startswith('_GoBack'):
                        segs.append((None, '<a class="wm-bm" id="%s"></a>' % bm_id(name), None))
                elif t in (q('w:ins'), q('w:smartTag'), q('w:customXml'), q('w:fldSimple')):
                    walk(ch, link)
                elif t == q('w:sdt'):
                    c = ch.find(q('w:sdtContent'))
                    if c is not None:
                        walk(c, link)
        walk(p)
        # birlashtirish
        out, i = [], 0
        while i < len(segs):
            css, h, link = segs[i]
            if css is None:
                out.append(h)
                i += 1
                continue
            j = i + 1
            buf = [h]
            while j < len(segs) and segs[j][0] == css and segs[j][2] == link:
                buf.append(segs[j][1])
                j += 1
            body = ''.join(buf)
            if css:
                body = '<span style="%s">%s</span>' % (css.rstrip(';'), body)
            if link:
                ext = not link.startswith('#')
                body = '<a href="%s"%s>%s</a>' % (html.escape(link, True), ' target="_blank" rel="noopener"' if ext else '', body)
            out.append(body)
            i = j
        return ''.join(out)

    def run(self, r, pr, segs, state, link):
        rpr_el = r.find(q('w:rPr'))
        rr = dict(pr)
        rs = rpr_el.find(q('w:rStyle')) if rpr_el is not None else None
        if rs is not None:
            rr.update(self.styles.char(wval(rs)))
        rr.update(parse_rpr(rpr_el))
        if rr.get('vanish'):
            return
        css = self.run_css(rr, pr)
        fld = state['fld']
        for ch in r:
            t = ch.tag
            if t == q('w:fldChar'):
                ft = ch.get(q('w:fldCharType'))
                if ft == 'begin':
                    fld.append('instr')
                elif ft == 'separate' and fld:
                    fld[-1] = 'result'
                elif ft == 'end' and fld:
                    fld.pop()
                continue
            if fld and fld[-1] == 'instr':
                continue
            if t == q('w:t'):
                if ch.text:
                    segs.append((css, html.escape(ch.text, False), link))
            elif t == q('w:tab'):
                segs.append((css, '<span class="wm-tab"></span>', link))
            elif t in (q('w:br'), q('w:cr')):
                if ch.get(q('w:type')) != 'page':
                    segs.append((css, '<br>', link))
            elif t == q('w:noBreakHyphen'):
                segs.append((css, '\u2011', link))
            elif t == q('w:sym'):
                c = ch.get(q('w:char')) or ''
                try:
                    chr_ = chr(int(c, 16))
                    if 0xF000 <= ord(chr_) <= 0xF0FF:
                        chr_ = BULLET_MAP.get(chr_, chr(ord(chr_) - 0xF000))
                    segs.append((css, html.escape(chr_), link))
                except ValueError:
                    pass
            elif t == q('w:drawing'):
                segs.append((None, self.drawing(ch), None))

    # --- rasm
    def drawing(self, d):
        blip = d.find('.//a:blip', NS)
        ext = d.find('.//wp:extent', NS)
        if blip is None:
            return ''
        rid = blip.get(q('r:embed'))
        if rid not in self.rels:
            return ''
        target = self.rels[rid][0]
        name = 'word/' + target if not target.startswith('/') else target[1:]
        data = self.z.read(name)
        src = d.find('.//a:srcRect', NS)
        crop = tuple(int(src.get(k) or 0) for k in ('l', 't', 'r', 'b')) if src is not None else (0, 0, 0, 0)
        h = hashlib.sha1(data + repr(crop).encode()).hexdigest()[:16]
        fname = h + '.webp'
        self.images.append(fname)
        out = os.path.join(self.img_dir, fname)
        if not os.path.exists(out):
            encode_image(data, crop, out)
        doc_pr = d.find('.//wp:docPr', NS)
        alt = (doc_pr.get('descr') or doc_pr.get('title') or '') if doc_pr is not None else ''
        width = int(ext.get('cx')) / 12700.0 if ext is not None else None
        style = ('width:%spt' % fmt(width)) if width else ''
        return '<img class="wm-img" src="%s%s" alt="%s" loading="lazy" style="%s">' % (
            IMG_TOKEN, fname, html.escape(alt, True), style)

    # --- jadval
    def table(self, t, depth=0):
        tblpr = t.find(q('w:tblPr'))
        ts = tblpr.find(q('w:tblStyle')) if tblpr is not None else None
        tp = self.styles.table_borders(wval(ts)) if ts is not None else {}
        tp.update(table_pr(tblpr))
        grid = [twip(g.get(q('w:w'))) or 0 for g in t.findall('w:tblGrid/w:gridCol', NS)]
        total = sum(grid) or self.text_width
        tw = tblpr.find(q('w:tblW')) if tblpr is not None else None
        width = None
        if tw is not None and tw.get(q('w:type')) == 'dxa' and int(tw.get(q('w:w')) or 0) > 0:
            width = twip(tw.get(q('w:w')))
        elif tw is not None and tw.get(q('w:type')) == 'pct':
            v = tw.get(q('w:w')) or '0'
            width = (float(v.rstrip('%')) if v.endswith('%') else int(v) / 50.0) / 100.0 * self.text_width
        width = width or total
        jc = tblpr.find(q('w:jc')) if tblpr is not None else None
        tcss = ['width:%spt' % fmt(width)]
        if jc is not None and wval(jc) == 'center':
            tcss.append('margin-left:auto;margin-right:auto')
        ind = tblpr.find(q('w:tblInd')) if tblpr is not None else None
        if ind is not None and int(ind.get(q('w:w')) or 0):
            tcss.append('margin-left:%spt' % fmt(twip(ind.get(q('w:w')))))
        cols = ''.join('<col style="width:%s%%">' % fmt(g / total * 100) for g in grid)
        rows = t.findall(q('w:tr'))
        head, bodyr = [], []
        nrows = len(rows)
        for ri, tr in enumerate(rows):
            is_head = tr.find('w:trPr/w:tblHeader', NS) is not None and on(tr.find('w:trPr/w:tblHeader', NS))
            cells = tr.findall(q('w:tc'))
            col = 0
            ch = []
            for tc in cells:
                tcpr = tc.find(q('w:tcPr'))
                span = 1
                gs = tcpr.find(q('w:gridSpan')) if tcpr is not None else None
                if gs is not None:
                    span = int(wval(gs))
                vm = tcpr.find(q('w:vMerge')) if tcpr is not None else None
                if vm is not None and wval(vm) != 'restart':
                    col += span
                    continue
                rowspan = 1
                if vm is not None:
                    rowspan = self.vmerge_span(rows, ri, col)
                css = []
                first, last = col == 0, col + span >= len(grid)
                top, bottom = ri == 0, ri + rowspan >= nrows
                tcb = tcpr.find(q('w:tcBorders')) if tcpr is not None else None
                for side, edge, inner in (('top', top, 'insideH'), ('bottom', bottom, 'insideH'),
                                          ('left', first, 'insideV'), ('right', last, 'insideV')):
                    b = None
                    if tcb is not None:
                        e = tcb.find(q('w:' + side))
                        if e is None:
                            e = tcb.find(q('w:' + {'left': 'start', 'right': 'end'}.get(side, side)))
                        b = parse_border(e)
                    if b is None:
                        b = tp.get('b_' + (side if edge else inner))
                    if b is None or b[0] == 'none':
                        css.append('border-%s:none' % side)
                    else:
                        css.append('border-%s:%spt %s %s' % (side, fmt(b[1]), b[3], self.palette.var('l', b[2])))
                mar = {s: tp.get('m_' + s, 108 if s in ('left', 'right') else 0) for s in ('top', 'left', 'bottom', 'right')}
                tm = tcpr.find(q('w:tcMar')) if tcpr is not None else None
                if tm is not None:
                    for s in mar:
                        e = tm.find(q('w:' + s))
                        if e is None:
                            e = tm.find(q('w:' + {'left': 'start', 'right': 'end'}.get(s, s)))
                        if e is not None:
                            mar[s] = int(e.get(q('w:w')) or 0)
                css.append('padding:%s' % ' '.join('%spt' % fmt(mar[s] / 20.0) for s in ('top', 'right', 'bottom', 'left')))
                shd = tcpr.find(q('w:shd')) if tcpr is not None else None
                if shd is not None and wval(shd, 'fill') not in (None, 'auto'):
                    css.append('background:%s' % self.palette.var('b', wval(shd, 'fill')))
                va = tcpr.find(q('w:vAlign')) if tcpr is not None else None
                css.append('vertical-align:%s' % {'center': 'middle', 'bottom': 'bottom'}.get(wval(va), 'top'))
                inner_html = '\n'.join(self.cell_block(e, depth) for e in self.blocks(tc))
                attrs = ''
                if span > 1:
                    attrs += ' colspan="%d"' % span
                if rowspan > 1:
                    attrs += ' rowspan="%d"' % rowspan
                ch.append('<td%s style="%s"><div class="wm-cell">%s</div></td>' % (attrs, ';'.join(css), inner_html))
                col += span
            row = '<tr>%s</tr>' % ''.join(ch)
            (head if is_head and not bodyr else bodyr).append(row)
        single = len(rows) == 1 and len(rows[0].findall(q('w:tc'))) == 1
        cls = 'wm-table' + (' wm-box' if single else '')
        thead = '<thead>%s</thead>' % ''.join(head) if head else ''
        return '<div class="wm-tw"><table class="%s" style="%s"><colgroup>%s</colgroup>%s<tbody>%s</tbody></table></div>' % (
            cls, ';'.join(tcss), cols, thead, ''.join(bodyr))

    def vmerge_span(self, rows, ri, col):
        n = 1
        for tr in rows[ri + 1:]:
            c = 0
            found = False
            for tc in tr.findall(q('w:tc')):
                tcpr = tc.find(q('w:tcPr'))
                gs = tcpr.find(q('w:gridSpan')) if tcpr is not None else None
                if c == col:
                    vm = tcpr.find(q('w:vMerge')) if tcpr is not None else None
                    found = vm is not None and wval(vm) != 'restart'
                    break
                c += int(wval(gs)) if gs is not None else 1
            if not found:
                break
            n += 1
        return n

    def cell_block(self, el, depth):
        if el.tag == q('w:tbl'):
            return self.table(el, depth + 1)
        return self.paragraph(el, in_cell=True)


def classify(content, min_uses=3):
    """Ko'p takrorlanadigan style="..." larni .wm-doc .sN klasslariga chiqaradi (jadval kataklari tufayli HTML bir necha marta kichrayadi)."""
    counts = {}
    for st in re.findall(r' style="([^"]+)"', content):
        counts[st] = counts.get(st, 0) + 1
    names = {}
    for st, n in sorted(counts.items(), key=lambda a: -a[1]):
        if n >= min_uses and len(st) > 12:
            names[st] = 's%d' % len(names)

    def rep(m):
        attrs = m.group(2)
        sm = re.search(r' style="([^"]+)"', attrs)
        if not sm or sm.group(1) not in names:
            return m.group(0)
        attrs = attrs.replace(sm.group(0), '')
        cm = re.search(r' class="([^"]*)"', attrs)
        if cm:
            attrs = attrs.replace(cm.group(0), ' class="%s %s"' % (cm.group(1), names[sm.group(1)]))
        else:
            attrs = ' class="%s"' % names[sm.group(1)] + attrs
        return '<%s%s>' % (m.group(1), attrs)
    content = re.sub(r'<(\w+)((?: [\w-]+="[^"]*")+)>', rep, content)
    # .wm-page prefiksi: ERP tungi rejimining "body p/span {color}" qoidalaridan va sahifa reset'idan ustun turish uchun
    css = ''.join('.wm-page .wm-doc .%s{%s}' % (n, st) for st, n in names.items())
    return content, css


def bm_id(name):
    return 'b-' + re.sub(r'[^A-Za-z0-9_-]', '_', name)


def encode_image(data, crop, out):
    im = Image.open(io.BytesIO(data))
    im.load()
    if any(crop):
        w, h = im.size
        l, t, r, b = (c / 100000.0 for c in crop)
        box = (max(0, round(w * l)), max(0, round(h * t)), min(w, round(w * (1 - r))), min(h, round(h * (1 - b))))
        if box[2] > box[0] and box[3] > box[1]:
            im = im.crop(box)
    if im.mode in ('RGBA', 'LA', 'P'):
        im = im.convert('RGBA')
        bg = Image.new('RGB', im.size, (255, 255, 255))
        bg.paste(im, mask=im.split()[-1])
        im = bg
    elif im.mode != 'RGB':
        im = im.convert('RGB')
    if im.width > MAX_IMG_WIDTH:
        im = im.resize((MAX_IMG_WIDTH, round(im.height * MAX_IMG_WIDTH / im.width)), Image.LANCZOS)
    tmp = out + '.tmp'
    im.save(tmp, 'WEBP', quality=WEBP_QUALITY, method=6)
    os.replace(tmp, out)


# ---------------------------------------------------------------- ishga tushirish

MODULE_RE = re.compile(r'^(\d\d)-([a-z0-9-]+)$')


def find_modules(src):
    mods = []
    for d in sorted(os.listdir(src)):
        m = MODULE_RE.match(d)
        if m and os.path.isdir(os.path.join(src, d)):
            mods.append(d)
    return mods


def rebuild_manifest(out_dir):
    mods = {}
    for lang in LANGS:
        ld = os.path.join(out_dir, lang)
        if not os.path.isdir(ld):
            continue
        for f in sorted(os.listdir(ld)):
            if not f.endswith('.html'):
                continue
            mid = f[:-5]
            with open(os.path.join(ld, f), encoding='utf-8') as fh:
                first = fh.readline()
            m = re.match(r'<!--title:(.*?)-->', first)
            e = mods.setdefault(mid, {'id': mid, 'num': mid[:2], 'titles': {}})
            e['titles'][lang] = html.unescape(m.group(1)) if m else mid
    data = {'modules': [mods[k] for k in sorted(mods)]}
    with open(os.path.join(out_dir, 'manifest.json'), 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
    return data


def cleanup_images(out_dir, log):
    used = set()
    for lang in LANGS:
        ld = os.path.join(out_dir, lang)
        if os.path.isdir(ld):
            for f in os.listdir(ld):
                if f.endswith('.html'):
                    with open(os.path.join(ld, f), encoding='utf-8') as fh:
                        used.update(re.findall(re.escape(IMG_TOKEN) + r'([0-9a-f]{16}\.webp)', fh.read()))
    img = os.path.join(out_dir, 'img')
    removed = 0
    for f in os.listdir(img):
        if f not in used:
            os.remove(os.path.join(img, f))
            removed += 1
    if removed:
        log('  eskirgan rasmlar o\'chirildi: %d' % removed)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--src', required=True, help='modullar papkasi (ichida 01-umumiy/..., 02-.../ ...)')
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument('--out', default=os.path.normpath(os.path.join(here, '..', '..', 'tahsinERP', 'ManualData')))
    ap.add_argument('--only', action='append', help='modul raqami yoki nomi (bir necha marta berish mumkin)')
    ap.add_argument('--lang', choices=LANGS, action='append')
    args = ap.parse_args()
    log = lambda s: print(s, flush=True)
    langs = args.lang or list(LANGS)
    os.makedirs(os.path.join(args.out, 'img'), exist_ok=True)
    mods = find_modules(args.src)
    if args.only:
        mods = [m for m in mods if any(m == o or m.startswith(o.zfill(2) + '-') or m.startswith(o) for o in args.only)]
    if not mods:
        sys.exit('Modul topilmadi: %s' % args.src)
    errors = 0
    for mod in mods:
        for lang in langs:
            path = os.path.join(args.src, mod, '%s_%s.docx' % (mod, lang))
            if not os.path.exists(path):
                log('! %s yo\'q' % path)
                errors += 1
                continue
            try:
                c = Converter(path, os.path.join(args.out, 'img'), log)
                body = c.convert()
                title = c.title(mod[:2])
            except Exception as e:  # noqa: BLE001 — bitta fayl xatosi qolganlarini to'xtatmasin
                log('! %s: %s: %s' % (os.path.basename(path), type(e).__name__, e))
                errors += 1
                continue
            os.makedirs(os.path.join(args.out, lang), exist_ok=True)
            out = os.path.join(args.out, lang, mod + '.html')
            text = '<!--title:%s-->\n<!--manba: %s_%s.docx; tools/manual/convert_docx.py — qo\'lda tahrir qilmang-->\n%s' % (
                html.escape(title, False), mod, lang, body)
            with open(out, 'w', encoding='utf-8', newline='\n') as fh:
                fh.write(text)
            log('%s/%s: %d sarlavha, %d rasm, %d KB' % (lang, mod, c.hcount, len(c.images), len(text.encode('utf-8')) // 1024))
    rebuild_manifest(args.out)
    cleanup_images(args.out, log)
    total = 0
    for root, _, files in os.walk(args.out):
        total += sum(os.path.getsize(os.path.join(root, f)) for f in files)
    nimg = len(os.listdir(os.path.join(args.out, 'img')))
    log('Jami: %s — %d rasm, %.1f MB' % (args.out, nimg, total / 1048576.0))
    sys.exit(1 if errors else 0)


if __name__ == '__main__':
    main()
