# Stage 2: tag-based 2D detection -> PCGamingWiki (engine, graphics API, bitness, perspectives, art style)
# -> Flato3D type / route / depth rule. Writes candidates.json (data for the Flato3D installer).
import json, os, re, sys, time, urllib.parse, urllib.request
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from collect import HERE, jload, jsave, norm, norm_loose

UA = {'User-Agent': 'Mozilla/5.0 Flato3D-library-survey/0.1'}
TAGNAME = {t['tagid']: t['name'] for t in jload(os.path.join(HERE, 'tags.json'))}

CORE2D = {'2D', 'Pixel Graphics', 'Hand-drawn', '2D Platformer', 'Side Scroller', 'Point & Click', 'Visual Novel',
          '2D Fighter', "Shoot 'Em Up", 'Top-Down Shooter'}
WEAK2D = {'Retro', 'Old School', "Beat 'em up", 'Metroidvania', 'Precision Platformer', 'Bullet Hell', 'Comic Book',
          'Puzzle Platformer', 'Top-Down', 'Cartoon', 'Cartoony', 'Arcade', 'Platformer', 'Isometric'}
THREED = {'3D', 'First-Person', 'FPS', 'Third Person', 'Third-Person Shooter', 'Open World', 'Realistic',
          '3D Platformer', 'Voxel', 'VR', 'Immersive Sim', 'Racing', 'Driving', 'Flight', 'Hack and Slash'}
GROUPS = {
    'adventure': {'Point & Click': 1.0, 'Adventure': 0.3, 'Hidden Object': 0.8, 'Detective': 0.4},
    'vn': {'Visual Novel': 1.0, 'Dating Sim': 0.6, 'Interactive Fiction': 0.6},
    'side': {'Side Scroller': 1.0, '2D Platformer': 1.0, 'Platformer': 0.6, 'Metroidvania': 1.0,
             "Beat 'em up": 0.8, 'Precision Platformer': 1.0, 'Puzzle Platformer': 0.8, '2D Fighter': 0.8,
             "Shoot 'Em Up": 0.6},
    'topdown': {'Top-Down': 1.0, 'Top-Down Shooter': 1.0, 'Twin Stick Shooter': 0.8, 'Isometric': 0.6,
                'Dungeon Crawler': 0.4, 'Action RPG': 0.3, 'Bullet Hell': 0.4},
    'static': {'Card Game': 1.0, 'Puzzle': 0.6, 'Board Game': 1.0, 'Match 3': 1.0, 'Deckbuilding': 0.8,
               'Tower Defense': 0.6, 'Turn-Based Strategy': 0.5, 'Management': 0.4},
}
RULE = {'adventure': 'feet_y', 'vn': 'draw_order', 'side': 'parallax', 'topdown': 'draw_order', 'static': 'draw_order'}

def tag_profile(tags):
    if not tags:
        return None
    wmax = max(w for _, w in tags) or 1
    named = [(TAGNAME.get(t, str(t)), w / wmax, i) for i, (t, w) in enumerate(tags)]
    s2 = sum(w for n, w, i in named if n in CORE2D) + 0.4 * sum(w for n, w, i in named if n in WEAK2D)
    s3 = sum(w for n, w, i in named if n in THREED)
    core_rank = min([i for n, w, i in named if n in CORE2D] or [99])
    w25 = max([w for n, w, i in named if n == '2.5D'] or [0])
    groups = {g: sum(w * f for n, w, i in named for gn, f in m.items() if n == gn) for g, m in GROUPS.items()}
    return {'names': [n for n, _, _ in named], 's2': round(s2, 2), 's3': round(s3, 2), 'core_rank': core_rank,
            'w25': round(w25, 2), 'pixel': any(n == 'Pixel Graphics' for n, _, _ in named),
            'handdrawn': any(n in ('Hand-drawn', 'Comic Book') for n, _, _ in named), 'groups': groups}

def tag_class(p):
    if not p: return None
    if p['core_rank'] <= 14 and p['s2'] >= 0.5 and p['s2'] >= 1.2 * p['s3']:
        return '2.5D' if p['w25'] >= 0.6 else '2D'
    if p['w25'] >= 0.5 and p['s3'] < 1.5:
        return '2.5D'
    return None

# ---------------------------------------------------------------- PCGamingWiki
def http(url, raw=False, tries=6, follow=True):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers=UA)
            if not follow:
                class NoRedir(urllib.request.HTTPRedirectHandler):
                    def redirect_request(self, *a, **k): return None
                op = urllib.request.build_opener(NoRedir)
                try:
                    r = op.open(req, timeout=40); return r.geturl()
                except urllib.error.HTTPError as e:
                    if e.code in (301, 302, 303): return e.headers.get('Location')
                    if e.code == 404: return None
                    raise
            with urllib.request.urlopen(req, timeout=60) as r:
                b = r.read().decode('utf-8')
                return b if raw else json.loads(b)
        except urllib.error.HTTPError as e:
            if e.code in (429, 502, 503, 504): time.sleep(15 * (i + 1)); continue
            if e.code == 404: return None
            raise
        except Exception as e:
            time.sleep(4 * (i + 1))
    raise RuntimeError('failed ' + url)

API = 'https://www.pcgamingwiki.com/w/api.php?'

def pcgw_pages(titles, cache):
    """Fetch wikitext for titles (50 per request). cache: title -> {'page': name, 'text': ...} or None."""
    todo = [t for t in dict.fromkeys(titles) if t and t not in cache]
    for i in range(0, len(todo), 50):
        chunk = todo[i:i + 50]
        q = {'action': 'query', 'prop': 'revisions', 'rvprop': 'content', 'rvslots': 'main', 'redirects': 1,
             'format': 'json', 'titles': '|'.join(chunk)}
        d = http(API + urllib.parse.urlencode(q))
        qd = d.get('query', {})
        alias = {}
        for n in qd.get('normalized', []): alias[n['from']] = n['to']
        red = {r['from']: r['to'] for r in qd.get('redirects', [])}
        pages = {p['title']: p for p in qd.get('pages', {}).values()}
        for t in chunk:
            name = alias.get(t, t); name = red.get(name, name)
            p = pages.get(name)
            if p and 'revisions' in p:
                cache[t] = {'page': p['title'], 'text': p['revisions'][0]['slots']['main']['*']}
            else:
                cache[t] = None
        print('pcgw titles', i + len(chunk), '/', len(todo), flush=True)
        time.sleep(1.0)

def pcgw_by_appid(appid, cache):
    k = 'appid:%s' % appid
    if k in cache: return cache[k]
    loc = http('https://www.pcgamingwiki.com/api/appid.php?appid=%s' % appid, follow=False)
    page = None
    if loc and '/wiki/' in loc:
        page = urllib.parse.unquote(loc.split('/wiki/', 1)[1]).replace('_', ' ')
    cache[k] = None
    if page:
        pcgw_pages([page], cache)
        cache[k] = cache.get(page)
    time.sleep(0.5)
    return cache[k]

def pcgw_search(title, cache):
    k = 'search:' + title
    if k in cache: return cache[k]
    d = http(API + urllib.parse.urlencode({'action': 'query', 'list': 'search', 'srsearch': title, 'srlimit': 3, 'format': 'json'}))
    cache[k] = None
    for r in (d or {}).get('query', {}).get('search', []):
        if norm_loose(r['title']) == norm_loose(title):
            pcgw_pages([r['title']], cache); cache[k] = cache.get(r['title']); break
    time.sleep(0.5)
    return cache[k]

def field(text, name):
    m = re.search(r'^\|\s*' + re.escape(name) + r'\s*=([^\n]*)', text, re.M | re.I)
    return m.group(1).strip() if m else ''

def parse_pcgw(text):
    eng = []
    for m in re.finditer(r'\{\{Infobox game/row/engine\|([^|}]+)([^}]*)', text):
        nm = re.search(r'\|\s*name\s*=\s*([^|}]+)', m.group(2))
        eng.append((nm.group(1) if nm else m.group(1)).strip())
    tax = {}
    for m in re.finditer(r'\{\{Infobox game/row/taxonomy/([a-z ]+?)\s*\|([^}]*)\}\}', text):
        v = [x.strip() for x in m.group(2).split(',') if x.strip()]
        if v: tax[m.group(1).strip()] = v
    def clean(v):
        v = re.sub(r'<ref.*?(</ref>|/>)', '', v); v = re.sub(r'\{\{[^}]*\}\}', '', v)
        return v.strip()
    d3d = clean(field(text, 'direct3d versions'))
    return {
        'engines': list(dict.fromkeys(eng)),
        'steam_appids': re.findall(r'\d+', field(text, 'steam appid')),
        'd3d': [x.strip() for x in re.split(r'[,/]', d3d) if x.strip()],
        'ddraw': clean(field(text, 'directdraw versions')),
        'opengl': clean(field(text, 'opengl versions')),
        'opengl_notes': clean(field(text, 'opengl notes')),
        'vulkan': clean(field(text, 'vulkan versions')),
        'glide': clean(field(text, 'glide versions')),
        'software': clean(field(text, 'software mode')),
        'x86': field(text, 'windows 32-bit exe').lower(), 'x64': field(text, 'windows 64-bit exe').lower(),
        'perspectives': tax.get('perspectives', []), 'art': tax.get('art styles', []), 'genres': tax.get('genres', []),
    }

# ---------------------------------------------------------------- Flato3D classification
BROWSER = ('rpg maker mv', 'rpg maker mz', 'nw.js', 'electron', 'construct', 'chromium', 'html5', 'cocos creator')
ONEIMAGE = ('dosbox', 'scummvm', 'emulat', 'm2 ', 'eclipse engine', 'retroarch', 'mame', 'snes9x', 'zsnes', 'mednafen')

def yes(v):
    return bool(v) and v.lower() not in ('false', 'unknown', 'n/a', 'no', 'hackable')

def route(pc, title):
    if not pc:
        return 'unknown', 'PCGamingWiki 정보 없음'
    eng = ' '.join(pc['engines']).lower()
    d3d = []
    for v in pc['d3d']:
        m = re.match(r'(\d+)', v)
        if m: d3d.append(int(m.group(1)))
    gl, vk = yes(pc['opengl']), yes(pc['vulkan'])
    gnote = pc.get('opengl_notes', '')
    if gl and ((re.search(r'os x|macos|mac\b|linux', gnote, re.I) and not re.search(r'windows', gnote, re.I))
               or re.search(r'prior to|before v|older version|previous version|removed', gnote, re.I)):
        gl = False  # OpenGL only on Mac/Linux, or only in old builds
    low = yes(pc['ddraw']) or yes(pc['glide']) or any(v <= 8 for v in d3d)
    est = ''
    first = pc['engines'][0].lower() if pc['engines'] else ''
    if any(b in eng for b in BROWSER) and 'unity' not in eng:
        return 'browser', 'Chromium/NW.js 계열 — 어려움'
    if (any(o in eng for o in ONEIMAGE if o != 'scummvm') or first.startswith(('scumm', 'saga', 'agi', 'sci', 'virtual theatre'))
            and 'scummvm' in eng) or re.search(r'dosbox', title.lower()):
        return 'oneimage', '완성 화면 한 장(에뮬레이터 등) — AI 깊이만 가능'
    if yes(pc['software']) and not (gl or d3d or vk):
        return 'oneimage', '소프트웨어 렌더링 — AI 깊이만 가능'
    if not (gl or d3d or vk or low):
        for key, api in ENGINE_API:
            if key in eng:
                if api == 'opengl': gl = True
                elif api == 'd3d11': d3d = [11]
                elif api == 'd3d9': d3d = [9]
                est = '엔진 기본값으로 추정'
                break
    if d3d or low:
        if gl:
            how = next((h for k, h in GL_SWITCH if k in eng), 'PCGamingWiki 참고')
            return 'opengl_alt', 'OpenGL로 전환 가능: ' + how
        if any(v in (10, 11) for v in d3d):
            note = [est] if est else []
            if 'unity' in eng: note.append('-force-glcore로 OpenGL 시도 가능(게임마다 다름)')
            if 'fna' in eng: return 'opengl_alt', 'OpenGL로 전환 가능: Steam 실행 옵션 /gldevice:OpenGL (환경 변수 FNA3D_FORCE_DRIVER=OpenGL은 Steam 재실행 때 빠짐)'
            return 'd3d11', '; '.join(note)
        if any(v == 9 for v in d3d) or low:
            return 'd3d9', '; '.join(x for x in ('dgVoodoo로 D3D11 변환', est) if x)
        if any(v == 12 for v in d3d):
            return 'unsupported', 'D3D12 전용'
    if gl:
        return 'opengl', est
    if vk:
        return 'unsupported', 'Vulkan 전용'
    return 'unknown', 'API 정보 없음'

# engine -> default Windows API, used only when PCGamingWiki has no API entries
ENGINE_API = [
    ('unreal engine', 'd3d11'), ('unity 4', 'd3d9'), ('unity 3', 'd3d9'), ('unity', 'd3d11'),
    ('gamemaker: studio 1', 'd3d9'), ('game maker', 'd3d9'), ('gamemaker', 'd3d11'), ('clickteam', 'd3d9'),
    ('chowdren', 'opengl'), ('libgdx', 'opengl'), ('lwjgl', 'opengl'), ('solar2d', 'opengl'), ('corona', 'opengl'),
    ('löve', 'opengl'), ('love2d', 'opengl'), ('defold', 'opengl'), ('godot 3', 'opengl'), ('godot', 'opengl'),
    ('openfl', 'opengl'), ('lime', 'opengl'), ('haxeflixel', 'opengl'), ('ren\'py', 'opengl'), ('monogame', 'opengl'),
    ('fna', 'd3d11'), ('xna', 'd3d9'), ('heaps', 'd3d11'), ('adventure game studio', 'd3d9'),
    ('visionaire', 'opengl'), ('wintermute', 'd3d9'), ('source', 'd3d9'), ('adobe air', 'd3d11'),
]
GL_SWITCH = [
    ('adventure game studio', 'winsetup.exe / acsetup.cfg에서 driver=OGL'),
    ('unity', '실행 옵션 -force-glcore (또는 -force-opengl)'),
    ('fna', 'Steam 실행 옵션 /gldevice:OpenGL'),
    ('heaps', '게임 설정 확인'),
]

ENGINES_3D = ('unreal', 'cryengine', 'source', 'telltale tool', 'storm3d', 'frozenbyte', 'avalanche', 'mt framework',
              'eco-motion', 'diesel', 'bluepoint', 'vision', 'drinkbox')

# Facts checked on this PC (installed files / earlier 3D sessions) override the wiki-based guess.
OVERRIDES = {
    307580: {'api': 'opengl', 'phase': 1, 'status': 'checked',
             'note': '설치본 확인: AGS 3.6.1.35 x86, acsetup.cfg driver=OGL, 셰이더 z=0·w=1(평면)'},
    598550: {'status': 'checked', 'bits': 'x64', 'note': '설치본 확인: Unity 2019.4.13f1 x64, D3D11. 카메라 원근/직교 미확인'},
    686200: {'status': 'checked', 'bits': 'x86', 'note': '설치본 확인: D3D9 x86, SDL2, 고정 기능 위주(셰이더 4개)'},
    215510: {'api': 'opengl', 'phase': 1, 'status': 'checked', 'bits': 'x86',
             'note': '설치본 확인: opengl32 + Cg(cgGL) — ARB 셰이더 경로 지원 필요할 수 있음'},
    322450: {'status': 'checked', 'note': '설치본 확인: OpenGL(SDL2 + GLEW) x86'},
    232790: {'kind': 1, 'depth_rule': 'auto', 'status': 'verified', 'note': 'OGL3D 검증됨: 2.5D 원근 레이어, 자동 수렴'},
    262060: {'force': True, 'class': '2.5D', 'kind': 1, 'depth_rule': 'auto', 'api': 'opengl', 'phase': 1, 'status': 'verified',
             'note': 'OGL3D 검증됨: 게임 화면은 원근 카메라(인트로 영상만 평면)'},
    606730: {'status': 'checked', 'note': 'geo-11 SR(dgVoodoo 경유)로 실행 확인, 2.5D'},
    2369900: {'kind': 2, 'note': 'masterotaku geo-11 픽스 있음: DS 레이어 z + 텍스처별 깊이'},
    1018010: {'kind': 4, 'depth_rule': 'ai', 'note': 'M2 에뮬레이터 — masterotaku 픽스는 화면 비율·업스케일만(3D 없음)'},
    1552550: {'kind': 4, 'depth_rule': 'ai', 'note': 'M2 에뮬레이터(GBA) — 한 장 화면 추정'},
}

RESEARCH = {}
if os.path.exists(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'api_research.json')):
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'api_research.json'), encoding='utf-8') as _f:
        RESEARCH = json.load(_f)['games']

PHASE = {'opengl': 1, 'opengl_alt': 1, 'd3d11': 3, 'd3d9': 3, 'browser': 9, 'oneimage': 9, 'unsupported': 9, 'unknown': 8}

def main():
    lib = jload(os.path.join(HERE, 'library.json'))
    tags = jload(os.path.join(HERE, 'cache_tags.json'), {})
    pc_cache_p = os.path.join(HERE, 'cache_pcgw.json')
    pc_cache = jload(pc_cache_p, {})

    # merge store entries into games (by steam appid, else by loose title)
    games = {}
    for it in lib:
        key = ('s%d' % it['steam_appid']) if it.get('steam_appid') else ('t' + norm_loose(it['title']))
        g = games.setdefault(key, {'title': it['title'], 'steam_appid': it.get('steam_appid'), 'stores': [], 'installed': False,
                                   'developer': it.get('developer', ''), 'genres': []})
        g['stores'].append({'store': it['store'], 'id': it['id']})
        g['installed'] |= bool(it.get('installed'))
        g['genres'] += it.get('genres') or []
        if it['store'] == 'steam': g['title'] = it['title']
    print('unique games', len(games))

    for g in games.values():
        t = tags.get(str(g['steam_appid'])) if g['steam_appid'] else None
        g['tagp'] = tag_profile(t['tags']) if t and t.get('tags') else None
        g['tagclass'] = tag_class(g['tagp'])

    # PCGamingWiki for tag candidates + games without Steam tags
    need = [g for g in games.values() if g['tagclass'] or not g['tagp'] or OVERRIDES.get(g['steam_appid'], {}).get('force')]
    print('pcgw needed', len(need))
    pcgw_pages([g['title'] for g in need], pc_cache); jsave(pc_cache_p, pc_cache)
    n = 0
    for g in need:
        e = pc_cache.get(g['title'])
        ok = e is not None
        if ok and g['steam_appid']:
            ok = str(g['steam_appid']) in parse_pcgw(e['text'])['steam_appids'] or not parse_pcgw(e['text'])['steam_appids']
        if not ok and g['steam_appid']:
            e = pcgw_by_appid(g['steam_appid'], pc_cache); n += 1
        if e is None and not g['steam_appid']:
            e = pcgw_search(g['title'], pc_cache); n += 1
        if n and n % 25 == 0: jsave(pc_cache_p, pc_cache); print('pcgw lookups', n, flush=True)
        g['pcgw'] = parse_pcgw(e['text']) if e else None
        g['pcgw_page'] = e['page'] if e else None
    jsave(pc_cache_p, pc_cache)

    out = []
    for g in games.values():
        pc = g.get('pcgw')
        cls = g['tagclass']
        persp = [p.lower() for p in (pc or {}).get('perspectives', [])]
        art = [a.lower() for a in (pc or {}).get('art', [])]
        if not g['tagp']:
            # no Steam tags: decide from PCGamingWiki taxonomy
            flat = any(p in persp for p in ('side view', 'scrolling', 'flip screen', 'top-down view', "bird's-eye view", 'text-based'))
            if ('pixel art' in art or flat) and not any(p in persp for p in ('first-person', 'third-person')):
                cls = '2D'
        if cls == '2D' and any(p in persp for p in ('first-person', 'third-person')) and not any(
                p in persp for p in ('side view', 'scrolling', 'flip screen', 'top-down view', "bird's-eye view")):
            cls = None
        ov = OVERRIDES.get(g['steam_appid'], {})
        if ov.get('force'):
            cls = ov.get('class', cls or '2D')
        if not cls:
            continue
        r, note = route(pc, g['title'])
        rs = RESEARCH.get(g['title'])   # researched by hand where PCGamingWiki has no API
        if rs and r == 'unknown':
            r = rs['api']
            note = f"{rs['evidence']} ({rs['confidence']})" + (f" {rs['note']}" if rs.get('note') else '')
        if cls == '2.5D' or any(e.lower().startswith(ENGINES_3D) for e in (pc or {}).get('engines', [])):
            kind = 1  # perspective / 3D-rendered scene
        elif r == 'oneimage':
            kind = 4
        else:
            kind = 3
        gp = (g['tagp'] or {}).get('groups') or {}
        genre = max(gp, key=gp.get) if gp and max(gp.values()) > 0.2 else None
        if not genre:
            gn = ' '.join(g['genres'] + (pc or {}).get('genres', [])).lower()
            genre = 'adventure' if 'adventure' in gn or 'point' in gn else 'side' if 'platform' in gn else 'static' if ('puzzle' in gn or 'card' in gn) else 'topdown' if 'top-down view' in persp else 'side' if 'side view' in persp else None
        pixel = bool((g['tagp'] or {}).get('pixel')) or 'pixel art' in art
        bits = None
        if pc:
            bits = 'x86' if pc['x86'] == 'true' and pc['x64'] != 'true' else 'x64' if pc['x64'] == 'true' and pc['x86'] != 'true' else 'x86/x64' if pc['x86'] == 'true' else None
        if rs and not bits: bits = rs.get('bits') or None
        out.append({
            'title': g['title'], 'steam_appid': g['steam_appid'], 'stores': g['stores'], 'installed': g['installed'],
            'developer': g['developer'], 'class': cls, 'kind': kind, 'pixel': pixel,
            'style': 'pixel' if pixel else 'illustrated',
            'genre': genre, 'depth_rule': 'auto' if kind == 1 else ('ai' if kind == 4 else RULE.get(genre, 'draw_order')),
            'engine': (pc or {}).get('engines', []) or ([rs['engine']] if rs and rs.get('engine') else []), 'api': r, 'phase': PHASE[r], 'bits': bits, 'note': note,
            'd3d': (pc or {}).get('d3d', []), 'opengl': (pc or {}).get('opengl', ''), 'vulkan': (pc or {}).get('vulkan', ''),
            'perspectives': (pc or {}).get('perspectives', []), 'art': (pc or {}).get('art', []),
            'tags': ((g['tagp'] or {}).get('names') or [])[:10], 'pcgw_page': g.get('pcgw_page'),
            'status': 'untested',
        })
        e = out[-1]
        for k, v in ov.items():
            if k not in ('force', 'class'): e[k] = v
        if 'kind' in ov and 'depth_rule' not in ov and e['kind'] not in (1, 4):
            e['depth_rule'] = RULE.get(genre, 'draw_order')
    out.sort(key=lambda x: (x['phase'], x['kind'], not x['pixel'], x['title'].lower()))
    jsave(os.path.join(HERE, 'candidates.json'), out)
    import collections
    print('candidates', len(out))
    print('by class', collections.Counter(x['class'] for x in out))
    print('by api', collections.Counter(x['api'] for x in out))
    print('by kind', collections.Counter(x['kind'] for x in out))
    print('pixel', sum(x['pixel'] for x in out))

if __name__ == '__main__':
    main()
