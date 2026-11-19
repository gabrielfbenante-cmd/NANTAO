"""Gera o perfil do r2modman / Thunderstore Mod Manager (.r2z) a partir de modpack/NantaoTerror.mods.json.

Uso: python3 tools/make_r2z.py modpack/NantaoTerror.mods.json NantaoTerror saida.r2z
O .r2z é um zip com export.r2x (YAML): ao importar, o gerenciador baixa todos os mods do Thunderstore.
"""
import json, sys, zipfile

def r2x(profile, mods):
    lines = [f"profileName: {profile}", "mods:"]
    for full in sorted(mods, key=str.lower):
        major, minor, patch = (int(x) for x in mods[full].split("."))
        lines += [f"  - name: {full}", "    version:", f"      major: {major}", f"      minor: {minor}",
                  f"      patch: {patch}", "    enabled: true"]
    return "\n".join(lines) + "\n"

if __name__ == "__main__":
    src, profile, out = sys.argv[1], sys.argv[2], sys.argv[3]
    mods = json.load(open(src, encoding="utf-8"))
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("export.r2x", r2x(profile, mods))
    print(f"{out}: {len(mods)} mods no perfil '{profile}'")
