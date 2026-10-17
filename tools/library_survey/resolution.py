# Stage 3: native resolution support -> upscale recommendation, for the installer's pre-selection.
#
#   python -I resolution.py [cache_pcgw.json]
#
# Reads the PCGamingWiki page of every candidate (from the cache classify.py keeps, missing pages from
# the API), takes the Video table's "4K Ultra HD" value and notes, and adds to data/game_candidates.json
# and data/private/game_candidates_full.json:
#   uhd       true | false | limited | hackable | unknown | "" (no page)
#   uhd_note  the notes, without references and markup (shortened)
#   upscale   "1" = recommend [Display] upscale (no 4K, or 4K with a UI that does not scale),
#             "0" = native resolution works, "" = not known
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'data')
FILES = [os.path.join(DATA, 'game_candidates.json'), os.path.join(DATA, 'private', 'game_candidates_full.json')]
API = 'https://www.pcgamingwiki.com/w/api.php?'
UA = {'User-Agent': 'Flato3D library survey (github.com/nautymac/Flato3D-SR)'}

# a UI that stays small or unscaled at 4K: the game is better rendered at 1080p and enlarged
UI_ISSUE = re.compile(r"\b(UI|HUD|interface|text|fonts?|menus?|subtitles?|cursor)\b[^.]{0,60}?"
                      r"\b(small|tiny|does(?:n'?t| not) scale|not scal\w*|unscaled|fixed size)"
                      r"|\b(small|tiny)\b[^.]{0,40}?\b(UI|HUD|interface|text|fonts?|menus?)\b", re.I)


def fetch(titles, cache):
    todo = [t for t in dict.fromkeys(titles) if t and t not in cache]
    for i in range(0, len(todo), 50):
        chunk = todo[i:i + 50]
        q = {'action': 'query', 'prop': 'revisions', 'rvprop': 'content', 'rvslots': 'main', 'redirects': 1,
             'format': 'json', 'titles': '|'.join(chunk)}
        for attempt in range(5):
            try:
                with urllib.request.urlopen(urllib.request.Request(API + urllib.parse.urlencode(q), headers=UA), timeout=60) as r:
                    d = json.loads(r.read().decode('utf-8'))
                break
            except Exception:
                time.sleep(5 * (attempt + 1))
        else:
            raise RuntimeError('PCGamingWiki not reachable')
        qd = d.get('query', {})
        alias = {n['from']: n['to'] for n in qd.get('normalized', [])}
        red = {r['from']: r['to'] for r in qd.get('redirects', [])}
        pages = {p['title']: p for p in qd.get('pages', {}).values()}
        for t in chunk:
            name = red.get(alias.get(t, t), alias.get(t, t))
            p = pages.get(name)
            cache[t] = {'page': p['title'], 'text': p['revisions'][0]['slots']['main']['*']} if p and 'revisions' in p else None
        print('fetched', i + len(chunk), '/', len(todo), flush=True)
        time.sleep(1.0)


def field(text, name):
    m = re.search(r'^\|\s*' + re.escape(name) + r'\s*=([^\n]*)', text, re.M | re.I)
    return m.group(1).strip() if m else ''


def clean(v):
    v = re.sub(r'<ref[^>]*/>|<ref.*?</ref>', '', v, flags=re.S)
    v = re.sub(r'\{\{\s*(?:Term|term)\s*\|([^}|]*)[^}]*\}\}', r'\1', v)
    v = re.sub(r'\{\{[^{}]*\}\}', '', v)
    v = re.sub(r'\[\[(?:[^\]|]*\|)?([^\]]*)\]\]', r'\1', v)
    v = re.sub(r'\[https?://\S+\s+([^\]]*)\]', r'\1', v)
    v = re.sub(r"'{2,}", '', v)
    v = re.sub(r'\s+', ' ', v).strip()
    return v[:200] + ('...' if len(v) > 200 else '')


# "Maximum resolution is 1600x900" / "1600x900 max"
MAXRES = re.compile(r'max(?:imum)?\b[^.]{0,30}?(\d{3,5})\s*[x×]\s*\d{3,5}|(\d{3,5})\s*[x×]\s*\d{3,5}\s*max', re.I)
# 4K can be set, only the aspect ratio is kept (AGS: pillarboxed or stretched): native output works
ASPECT_ONLY = re.compile(r'can be set|pillarbox|letterbox|black borders|static aspect', re.I)


def recommend(uhd, note):
    if uhd in ('true', 'always on'):
        return '1' if UI_ISSUE.search(note) else '0'
    if uhd == 'hackable':
        return '1'   # 4K only through a third-party tool
    if uhd in ('false', 'limited'):
        m = MAXRES.search(note)
        if m:
            return '0' if int(m.group(1) or m.group(2)) >= 3840 else '1'
        return '0' if ASPECT_ONLY.search(note) else '1'
    return ''


def main():
    cache_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'cache_pcgw.json')
    cache = {}
    if os.path.exists(cache_path):
        with open(cache_path, encoding='utf-8') as f:
            cache = json.load(f)
    with open(FILES[0], encoding='utf-8') as f:
        pub = json.load(f)
    fetch([g.get('pcgw_page') for g in pub['games']], cache)
    with open(cache_path, 'w', encoding='utf-8') as f:
        json.dump(cache, f, ensure_ascii=False)
    found = {}
    for g in pub['games']:
        c = cache.get(g.get('pcgw_page') or '')
        if not c:
            found[g['steam_appid'], g['title']] = ('', '', '')
            continue
        uhd = field(c['text'], '4k ultra hd').lower() or 'unknown'
        note = clean(field(c['text'], '4k ultra hd notes'))
        found[g['steam_appid'], g['title']] = (uhd, note, recommend(uhd, note))
    for path in FILES:
        if not os.path.exists(path):
            continue
        with open(path, encoding='utf-8') as f:
            d = json.load(f)
        for g in d['games']:
            uhd, note, up = found.get((g['steam_appid'], g['title']), ('', '', ''))
            g['uhd'], g['uhd_note'], g['upscale'] = uhd, note, up
        text = json.dumps(d, ensure_ascii=False, indent=1).replace('\n', '\r\n') + '\r\n'
        with open(path, 'w', encoding='utf-8', newline='') as f:
            f.write(text)
    counts = {}
    for uhd, _, up in found.values():
        counts[uhd or 'no page', up or '?'] = counts.get((uhd or 'no page', up or '?'), 0) + 1
    for k in sorted(counts):
        print('4K %-9s upscale %s: %d' % (k[0], k[1], counts[k]))


if __name__ == '__main__':
    main()
