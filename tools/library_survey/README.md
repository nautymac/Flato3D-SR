# Library survey

Finds flat 2D / pixel games in the owned Steam + Heroic (Epic, GOG, Amazon) library without installing them,
and writes `data/game_candidates.json` for the Flato3D installer (informational game list).

Run from this folder (Python 3, network access to store.steampowered.com, api.steampowered.com, pcgamingwiki.com):

    python -I collect.py        # owned library -> Steam appids -> Steam tags (cache_*.json, library.json)
    python -I classify.py       # 2D detection + PCGamingWiki engine/API -> candidates.json
    python -I build_page.py out.html ../../data/game_candidates.json

Sources: `E:\Steam\appcache\appinfo.vdf` + `appcache\librarycache` (owned games), `%APPDATA%\heroic\store_cache\*.json`,
Steam IStoreBrowseService tags (with weights), PCGamingWiki page wikitext (engine, Direct3D/OpenGL/Vulkan, 32/64-bit,
perspectives, art style). Games checked on this PC are fixed in `OVERRIDES` (classify.py).

Fields per game: `kind` 1 = 2.5D / 3D-rendered (depth exists), 2 = flat with layer z, 3 = flat layers needing rules,
4 = one finished image (emulator / software); `api` opengl | opengl_alt (switchable to OpenGL) | d3d11 | d3d9 | oneimage |
browser | unsupported | unknown; `depth_rule` feet_y | parallax | draw_order | auto | ai; `status` untested | checked | verified.
