"""Resolve as dependencias de um modpack no Thunderstore (versao mais alta exigida de cada pacote).

Uso: python3 tools/resolve_modpack.py $(grep -v "^#" modpack/NantaoTerror.roots.txt | cut -d" " -f1)
Gera closure.json (copie para modpack/NantaoTerror.mods.json) e depois rode tools/make_r2z.py.
"""
import json, sys, urllib.request, ssl, re
ctx = ssl.create_default_context(cafile="/root/.ccr/ca-bundle.crt")
cache = {}
def get(u):
    if u in cache: return cache[u]
    req = urllib.request.Request(u, headers={"User-Agent": "nantao-modpack-research"})
    with urllib.request.urlopen(req, timeout=60, context=ctx) as r: cache[u] = json.load(r)
    return cache[u]
def vt(s): return tuple(int(x) for x in s.split("."))
def latest(full):
    ns, name = full.split("-", 1)
    p = get(f"https://thunderstore.io/api/experimental/package/{ns}/{name}/")
    return p["latest"]["version_number"]
def deps_of(full, ver):
    ns, name = full.split("-", 1)
    return get(f"https://thunderstore.io/api/experimental/package/{ns}/{name}/{ver}/")["dependencies"]
roots = sys.argv[1:]
want = {}
queue = []
for r in roots:
    v = latest(r); want[r] = v; queue.append(r)
while queue:
    full = queue.pop()
    for d in deps_of(full, want[full]):
        m = re.match(r"^(.+)-(\d+\.\d+\.\d+)$", d)
        dn, dv = m.group(1), m.group(2)
        if dn not in want or vt(dv) > vt(want[dn]):
            want[dn] = dv; queue.append(dn)
json.dump(want, open("closure.json","w"), indent=1, sort_keys=True)
for k in sorted(want, key=str.lower): print(f"{k}-{want[k]}")
print(len(want), "pacotes")
