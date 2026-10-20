#!/usr/bin/env python3
"""Builds the Flato3D-SR status list from the status report issues (label 'report') and the game profiles.

Every game with a profile (data/game_profiles.json) is listed with the profile's state; the players' reports add
their result. A player's newest report on a game replaces their earlier ones (a corrected report counts once);
the game's status is the majority of the players' current results, a tie goes to the newest report.

Output:
  STATUS.md         English list
  STATUS.ko.md      Korean list
  data/status.json  the same data for the installer

Run by .github/workflows/status-list.yml. Local test:
  python status_list.py --fixture issues.json --out OUTDIR
"""
import argparse
import json
import os
import re
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REPO_URL = "https://github.com/{repo}"
EXCLUDE_LABELS = {"invalid", "duplicate", "spam", "wontfix"}
NO_RESPONSE = "_No response_"

# first match wins: "does not work" before "works", "some problems" before "works well"
RESULTS = [("broken", r"does\s*not\s*work|doesn.?t\s*work|not\s*work|broken|안\s*됨"),
           ("issues", r"some\s*problems?|issues?|problems?|문제"),
           ("works", r"works|잘\s*됨|잘됨")]
RESULT_TEXT = {
    "en": {"works": "✅ Works", "issues": "⚠️ Some problems", "broken": "❌ Does not work", "handled": "🔧 Handled", None: "-"},
    "ko": {"works": "✅ 잘 됨", "issues": "⚠️ 일부 문제", "broken": "❌ 안 됨", "handled": "🔧 처리됨", None: "-"},
}
ICON = {"works": "✅", "issues": "⚠️", "broken": "❌"}
PROFILE_TEXT = {
    "en": {"verified": "✅ confirmed", "working": "🔵 working", None: "-"},
    "ko": {"verified": "✅ 확인됨", "working": "🔵 동작", None: "-"},
}

FIELDS = (("game", ("game", "게임")), ("steam", ("steam",)), ("result", ("result", "결과")),
          ("notes", ("notes", "메모")), ("report", ("report", "보고")))


def api(url, token):
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json", "User-Agent": "flato3d-status-list"})
    if token:
        req.add_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read().decode("utf-8"))


def fetch_issues(repo, token):
    issues, page = [], 1
    while True:
        batch = api("https://api.github.com/repos/%s/issues?labels=report&state=all&per_page=100&page=%d" % (repo, page), token)
        issues += [i for i in batch if "pull_request" not in i]
        if len(batch) < 100:
            return issues
        page += 1


def sections(body):
    """'### Heading' + value blocks of an issue form body -> {field: value}."""
    out = {}
    parts = re.split(r"^###\s+(.+?)\s*$", (body or "").replace("\r\n", "\n"), flags=re.M)
    for i in range(1, len(parts) - 1, 2):
        head, val = parts[i].strip().lower(), parts[i + 1].strip()
        if val == NO_RESPONSE:
            val = ""
        for field, words in FIELDS:
            if any(head.startswith(w) for w in words) and field not in out:
                out[field] = val
                break
    return out


def result_of(text):
    t = (text or "").lower()
    for key, pat in RESULTS:
        if re.search(pat, t):
            return key
    return None


def appid_of(*texts):
    for t in texts:
        m = re.search(r"store\.steampowered\.com/app/(\d+)|steam\s*app\s*id\s*:?\s*(\d+)|^\s*(\d{3,8})\s*$", t or "", re.I | re.M)
        if m:
            return next(g for g in m.groups() if g)
    return None


def name_key(name):
    n = re.sub(r"[™®©]", "", (name or "").lower())
    return re.sub(r"[\W_]+", "", n)


def cell(text, limit=120):
    """Untrusted issue text -> one safe Markdown table cell."""
    t = re.sub(r"\s+", " ", text or "").strip()
    if len(t) > limit:
        t = t[: limit - 1].rstrip() + "…"
    t = t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return re.sub(r"([\\`*_\[\]|#!])", r"\\\1", t)


def parse(issue):
    labels = {((l.get("name") if isinstance(l, dict) else l) or "").lower() for l in issue.get("labels", [])}
    if labels & EXCLUDE_LABELS or issue.get("state_reason") == "not_planned":
        return None
    f = sections(issue.get("body"))
    title = issue.get("title") or ""
    game = (f.get("game") or "").strip()
    if not game:   # an issue written by hand: "[Game] result"
        m = re.match(r"\s*\[([^\]]+)\]", title)
        game = m.group(1).strip() if m and m.group(1).strip().lower() != "game" else ""
    result = result_of(f.get("result")) or result_of(re.sub(r"^\s*\[[^\]]*\]", "", title))
    if not game or not result:
        return None
    report = f.get("report", "")
    gpu = re.search(r"^GPU:\s*(.+)$", report, re.M)
    return {
        "number": issue["number"], "url": issue.get("html_url", ""), "created": issue.get("created_at", ""),
        "author": ((issue.get("user") or {}).get("login") or "").lower(), "open": issue.get("state") == "open",
        "game": game, "steam": appid_of(f.get("steam"), report), "result": result,
        "notes": f.get("notes", ""), "gpu": gpu.group(1).strip() if gpu else "",
    }


def load_profiles(root):
    with open(os.path.join(root, "data", "game_profiles.json"), encoding="utf-8") as f:
        return json.load(f)["games"]


def aggregate(reports, profiles):
    games, by_appid, by_name = {}, {}, {}

    def entry(key, name, appid):
        g = games.setdefault(key, {"key": key, "game": name, "steam": appid, "profile": None, "profile_id": None,
                                   "reports": [], "works": 0, "issues": 0, "broken": 0})
        if appid:
            by_appid[appid] = key
        by_name[name_key(name)] = key
        return g

    for p in profiles:
        appid = str(p.get("steam_appid") or "") or None
        g = entry("p:" + p["id"], p["name"], appid)
        g["profile"], g["profile_id"] = p.get("status"), p["id"]
    for r in sorted(reports, key=lambda r: (r["created"], r["number"])):
        key = by_appid.get(r["steam"]) if r["steam"] else None
        key = key or by_name.get(name_key(r["game"])) or ("r:" + (r["steam"] or name_key(r["game"])))
        g = games.get(key) or entry(key, r["game"], r["steam"])
        g["reports"].append(r)
        g[r["result"]] += 1
    for g in games.values():
        current = {}
        for r in g["reports"]:   # oldest first: a reporter's newest report wins
            if r["result"] != "works" and not r["open"]:
                continue   # a problem report closed as completed has been handled
            current[r["author"] or "#%d" % r["number"]] = r
        votes = {}
        for r in current.values():
            votes[r["result"]] = votes.get(r["result"], 0) + 1
        if votes:
            best = max(votes.values())
            newest = max(current.values(), key=lambda r: (r["created"], r["number"]))
            tied = [k for k, v in votes.items() if v == best]
            g["status"] = newest["result"] if newest["result"] in tied else sorted(tied)[0]
        else:
            g["status"] = "handled" if g["reports"] else None
        g["updated"] = g["reports"][-1]["created"][:10] if g["reports"] else ""
        g["open_problems"] = [r for r in g["reports"] if r["open"] and r["result"] != "works"]
        g["note"] = g["reports"][-1]["notes"] if g["reports"] else ""   # the newest report's own note
    return games


def render(games, lang, repo, total):
    ko = lang == "ko"
    base = REPO_URL.format(repo=repo)
    form = base + "/issues/new?template=status_report.yml"
    rows = sorted(games.values(), key=lambda g: g["game"].lower())
    L = []
    if ko:
        L += ["# Flato3D-SR 게임 상태", "",
              "게임별 상태를 자동으로 모은 목록입니다. **프로필**은 개발자가 맞춰 둔 설정의 상태이고(✅ 사용자 확인됨 · 🔵 동작), "
              "**플레이어 상태**는 상태 보고의 결과입니다(✅ 잘 됨 · ⚠️ 일부 문제 · ❌ 안 됨 · 🔧 처리됨). 같은 사람이 다시 보고하면 최근 보고만 셉니다. "
              "상태가 갈리면 다수를 따르고, 같으면 최근 보고를 따릅니다.", "",
              "보고는 설치기의 **[상태 보고]** 버튼이나 [양식](%s)으로 합니다. 처리한 문제 보고는 이슈를 닫고 상태에서 빠집니다. "
              "문제 보고가 모두 처리된 게임은 🔧 처리됨으로 표시됩니다." % form, "",
              "게임 %d개 · 보고 %d건 · English: [STATUS.md](STATUS.md)" % (len(rows), total), "",
              "| 게임 | 프로필 | 플레이어 상태 | 보고 | 열린 문제 | 최근 메모 | 이슈 |"]
    else:
        L += ["# Flato3D-SR game status", "",
              "Built automatically. **Profile** is the state of the developer's setup for the game (✅ confirmed by a player · 🔵 working); "
              "**Players** is the result of the status reports (✅ works · ⚠️ some problems · ❌ does not work · 🔧 handled). A player's newer report "
              "replaces their earlier one; the majority decides, a tie goes to the newest report.", "",
              "Report with the installer's **[Report status]** button or the [form](%s). A problem report that has been handled is closed and no "
              "longer counts; a game whose problem reports are all handled shows 🔧 handled." % form, "",
              "%d games · %d reports · 한국어: [STATUS.ko.md](STATUS.ko.md)" % (len(rows), total), "",
              "| Game | Profile | Players | Reports | Open problems | Latest note | Issues |"]
    L.append("|---|---|---|---|---|---|---|")
    for g in rows:
        name = cell(g["game"], 80)
        if g["steam"]:
            name = "[%s](https://store.steampowered.com/app/%s)" % (name, g["steam"])
        counts = " · ".join("%s %d" % (ICON[k], g[k]) for k in ("works", "issues", "broken") if g[k]) or "-"
        problems = " ".join("[#%d](%s)" % (r["number"], r["url"]) for r in g["open_problems"]) or "-"
        links = " ".join("[#%d](%s)" % (r["number"], r["url"]) for r in g["reports"][-5:]) or "-"
        status = RESULT_TEXT[lang][g["status"]] + (" (%s)" % g["updated"] if g["updated"] else "")
        L.append("| %s | %s | %s | %s | %s | %s | %s |" % (name, PROFILE_TEXT[lang].get(g["profile"], g["profile"] or "-"), status, counts,
                                                        problems, cell(g["note"]) or "-", links))
    L.append("")
    return "\n".join(L)


def to_json(games):
    out = []
    for g in sorted(games.values(), key=lambda g: g["game"].lower()):
        out.append({"game": g["game"], "steam": g["steam"], "profile": g["profile_id"], "profile_status": g["profile"], "status": g["status"],
                    "works": g["works"], "issues": g["issues"], "broken": g["broken"], "updated": g["updated"], "note": g["note"],
                    "open_problems": [r["number"] for r in g["open_problems"]], "reports": [r["number"] for r in g["reports"]]})
    return {"version": 1, "source": "GitHub issues (label report) + data/game_profiles.json", "games": out}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY", "nautymac/Flato3D-SR"))
    ap.add_argument("--out", default=ROOT)
    ap.add_argument("--fixture", help="JSON file with issues (API format) instead of calling GitHub")
    ap.add_argument("--check", type=int, help="only parse this issue: write listed=true|false and what= to GITHUB_OUTPUT")
    a = ap.parse_args()
    gh_out = os.environ.get("GITHUB_OUTPUT")
    if a.check:
        r = parse(api("https://api.github.com/repos/%s/issues/%d" % (a.repo, a.check), os.environ.get("GITHUB_TOKEN")))
        what = "%s / %s" % (r["game"], RESULT_TEXT["en"][r["result"]]) if r else ""
        print("issue #%d: %s" % (a.check, what or "not listed (no game or result, or excluded)"))
        if gh_out:
            with open(gh_out, "a", encoding="utf-8") as f:
                f.write("listed=%s\nwhat=%s\n" % ("true" if r else "false", what))
        return 0
    if a.fixture:
        with open(a.fixture, encoding="utf-8") as f:
            issues = json.load(f)
    else:
        issues = fetch_issues(a.repo, os.environ.get("GITHUB_TOKEN"))
    reports = [r for r in map(parse, issues) if r]
    games = aggregate(reports, load_profiles(ROOT))
    for name, text in (("STATUS.md", render(games, "en", a.repo, len(reports))), ("STATUS.ko.md", render(games, "ko", a.repo, len(reports)))):
        with open(os.path.join(a.out, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text + "\n")
    os.makedirs(os.path.join(a.out, "data"), exist_ok=True)
    with open(os.path.join(a.out, "data", "status.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(to_json(games), f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("%d reports from %d issues, %d games" % (len(reports), len(issues), len(games)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
