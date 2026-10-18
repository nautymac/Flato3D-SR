"""The survey page from the current data: only the games Flato3D can do.

    python -I build_targets_page.py out.html

Reads data/private/game_candidates_full.json (with stores and installed state) and keeps flat 2D games
(kind 2/3) whose route Flato3D handles: ordinary stereo games (kind 1) are Geo Gaming Hub's, and
one-image / browser / D3D12-Vulkan games cannot be done this way.
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', '..', 'data')
FIELDS = ('title', 'steam_appid', 'stores', 'installed', 'developer', 'kind', 'style', 'depth_rule', 'engine', 'api', 'phase', 'bits',
          'note', 'd3d', 'opengl', 'vulkan', 'tags', 'pcgw_page', 'status', 'uhd', 'uhd_note', 'upscale')


def main(out_html):
    with open(os.path.join(DATA, 'private', 'game_candidates_full.json'), encoding='utf-8') as f:
        full = json.load(f)
    games = full['games']
    keep = [g for g in games if g['kind'] in (2, 3) and g['api'] not in ('oneimage', 'browser', 'unsupported')]
    hub = sum(1 for g in games if g['kind'] == 1)
    out = len(games) - len(keep) - hub
    stores = re.match(r'(Steam \d+ · Epic \d+ · GOG \d+ · Amazon \d+ = \d+ [^,\s]+)', full.get('source', ''))
    meta = {
        'source': '%s · 평면 2D 후보 %d개 중 Flato3D 대상 %d개 (일반 입체 %d개는 Geo Gaming Hub, 할 수 없는 %d개 제외) · 2026-10-18 갱신 · 자료: Steam 태그, PCGamingWiki (CC BY-NC-SA 3.0)'
                  % (stores.group(1) if stores else '보유 목록', len(games), len(keep), hub, out),
    }
    # status from the Flato3D profiles (the candidate lists lag behind): verified = checked by the user, else working
    with open(os.path.join(DATA, 'game_profiles.json'), encoding='utf-8') as f:
        profiles = {p['steam_appid']: p.get('status') for p in json.load(f)['games'] if p.get('steam_appid')}
    meta['test'] = sorted(profiles)
    slim = []
    for g in keep:
        d = {k: g.get(k) for k in FIELDS}
        if g.get('steam_appid') in profiles:
            d['status'] = 'verified' if profiles[g['steam_appid']] == 'verified' else 'checked'
        slim.append(d)
    with open(os.path.join(HERE, 'page_template.html'), encoding='utf-8') as f:
        html = f.read()
    html = html.replace('/*DATA*/[]', json.dumps(slim, ensure_ascii=False, separators=(',', ':')).replace('</', '<\\/'))
    html = html.replace('/*META*/{}', json.dumps(meta, ensure_ascii=False).replace('</', '<\\/'))
    with open(out_html, 'w', encoding='utf-8') as f:
        f.write(html)
    print('wrote', out_html, len(html), 'bytes,', len(keep), 'games; source:', meta['source'])


if __name__ == '__main__':
    main(sys.argv[1])
