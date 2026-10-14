# Stage 3: candidates.json -> survey page (HTML) + data file for the Flato3D installer.
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from collect import HERE, jload

TEST_APPIDS = [307580, 598550, 686200, 215510, 322450]  # Technobabylon, Huntdown, DK:AS, Rocketbirds, FRAMED

def main(out_html, out_json):
    cand = jload(os.path.join(HERE, 'candidates.json'))
    lib = jload(os.path.join(HERE, 'library.json'))
    counts = {s: sum(1 for i in lib if i['store'] == s) for s in ('steam', 'epic', 'gog', 'amazon')}
    uniq = len({('s%d' % i['steam_appid']) if i.get('steam_appid') else i['title'].lower() for i in lib})
    meta = {
        'source': 'Steam %d · Epic %d · GOG %d · Amazon %d = %d 항목, 중복을 합쳐 %d개 게임 → 후보 %d개 · 2026-10-14 조사 · 자료: Steam 태그, PCGamingWiki'
                  % (counts['steam'], counts['epic'], counts['gog'], counts['amazon'], len(lib), uniq, len(cand)),
        'test': TEST_APPIDS,
    }
    slim = [{k: d[k] for k in ('title', 'steam_appid', 'stores', 'installed', 'developer', 'kind', 'style', 'depth_rule',
                               'engine', 'api', 'phase', 'bits', 'note', 'd3d', 'opengl', 'vulkan', 'tags', 'pcgw_page')}
            for d in cand]
    html = open(os.path.join(HERE, 'page_template.html'), encoding='utf-8').read()
    html = html.replace('/*DATA*/[]', json.dumps(slim, ensure_ascii=False, separators=(',', ':')).replace('</', '<\\/'))
    html = html.replace('/*META*/{}', json.dumps(meta, ensure_ascii=False).replace('</', '<\\/'))
    open(out_html, 'w', encoding='utf-8').write(html)
    data = {'schema': 1, 'generated': '2026-10-14', 'source': meta['source'], 'games': cand}
    json.dump(data, open(out_json, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('wrote', out_html, len(html), 'bytes;', out_json, len(cand), 'games')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
