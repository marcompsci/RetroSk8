#!/usr/bin/env python3
"""
Retro Sk8 string extractor (Phase 28).

Scans the game's runtime and core C# for player-facing English text and writes Tools/loc/strings.txt: one entry
per line, interpolated strings turned into templates ($"BEST {score:N0}" -> "BEST {0}"), line breaks as "\\n".
Translations live in Tools/loc/es.tsv and ja.tsv ("English<TAB>translation"); build_tables.py turns them into
Assets/RetroSk8/Scripts/Core/LocTables.cs. Run from the repo root:

    python3 Tools/loc/extract_strings.py            # rewrite strings.txt
    python3 Tools/loc/extract_strings.py --missing  # list strings a table doesn't have yet
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(ROOT, "Assets", "RetroSk8", "Scripts")
SCAN = ["Core", "Runtime"]
SKIP_FILES = {"Localization.cs", "LocTables.cs", "GameCenterSetup.cs", "StoreShotDirector.cs"}
OUT = os.path.join(ROOT, "Tools", "loc", "strings.txt")
# Arrays that hold data, not text to show: (file, the line that starts the array).
SKIP_REGIONS = [("Gallery.cs", "Blocked =")]
# Single mixed-case words that are shown to players (trick, park and game names).
SINGLE_WORDS = {"Shipyard", "Bluntslide", "Boneless", "Bonk", "Casper", "Footplant", "Revert", "Wallie", "Wallplant", "Wallride", "Letters", "Tailhook", "Epilogue"}

# Lines whose strings are never shown to players.
SKIP_LINE = re.compile(
    r"Debug\.Log|Debug\.LogWarning|Debug\.LogError|throw new|Exception\(|\[MenuItem|PlayerPrefs|Resources\.Load|"
    r"Shader\.Find|GameObject\.Find|Path\.Combine|PropertyToID|SetFloat\(|SetColor\(|SetTexture\(|HasProperty\(|"
    r"EnableKeyword|DllImport|\[Tooltip|\[Header|CreateAssetMenu|SendMessage\(|nameof\(|Log\(\"")


def tokenize(src):
    """Yields (kind, text, line) for string literals: kind is 'str' or 'interp' (text is the raw body)."""
    i, n, line = 0, len(src), 1
    while i < n:
        c = src[i]
        if c == "\n":
            line += 1
            i += 1
            continue
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i + 2)
            line += src.count("\n", i, n if j < 0 else j)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            # char literal
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            i = j + 1
            continue
        verbatim = src.startswith('@"', i) or src.startswith('$@"', i) or src.startswith('@$"', i)
        interp = src.startswith('$"', i) or src.startswith('$@"', i) or src.startswith('@$"', i)
        if c == '"' or interp or verbatim:
            start = src.index('"', i)
            j = start + 1
            depth = 0
            body = []
            while j < n:
                ch = src[j]
                if interp and ch == "{":
                    if src.startswith("{{", j) and depth == 0:
                        body.append("{{")
                        j += 2
                        continue
                    depth += 1
                    # skip the expression, including nested strings
                    k = j + 1
                    d = 1
                    while k < n and d > 0:
                        if src[k] == '"':
                            k += 1
                            while k < n and src[k] != '"':
                                k += 2 if src[k] == "\\" else 1
                        elif src[k] == "{":
                            d += 1
                        elif src[k] == "}":
                            d -= 1
                        k += 1
                    body.append(src[j:k])
                    depth = 0
                    j = k
                    continue
                if interp and ch == "}" and src.startswith("}}", j):
                    body.append("}}")
                    j += 2
                    continue
                if ch == "\\" and not verbatim:
                    body.append(src[j:j + 2])
                    j += 2
                    continue
                if ch == '"':
                    if verbatim and src.startswith('""', j):
                        body.append('"')
                        j += 2
                        continue
                    break
                if ch == "\n":
                    line += 1
                body.append(ch)
                j += 1
            yield ("interp" if interp else "str", "".join(body), line)
            i = j + 1
            continue
        i += 1


ESCAPES = {"\\n": "\n", "\\t": "\t", '\\"': '"', "\\\\": "\\", "\\'": "'", "\\u00B7": "·"}


def unescape(s):
    s = re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)
    for k, v in ESCAPES.items():
        s = s.replace(k, v)
    return s


def template(body):
    """$"..." body -> (template text, list of expression strings)."""
    out, exprs, i = [], [], 0
    while i < len(body):
        if body.startswith("{{", i):
            out.append("{"); i += 2; continue
        if body.startswith("}}", i):
            out.append("}"); i += 2; continue
        if body[i] == "{":
            d, k = 1, i + 1
            while k < len(body) and d > 0:
                if body[k] == "{": d += 1
                elif body[k] == "}": d -= 1
                k += 1
            exprs.append(body[i + 1:k - 1])
            out.append("{%d}" % (len(exprs) - 1))
            i = k
            continue
        out.append(body[i]); i += 1
    return unescape("".join(out)), exprs


def nested_strings(expr):
    return [unescape(m.group(1)) for m in re.finditer(r'"((?:[^"\\]|\\.)*)"', expr)]


def is_player_text(s):
    letters = [c for c in s if c.isalpha()]
    if len(letters) < 2:
        return False
    core = re.sub(r"\{\d\}", "", s)
    if re.fullmatch(r"[a-z0-9_./:\-]+", core):        # ids, keys, paths
        return False
    if "_" in core and " " not in core:                # GameObject names, ids
        return False
    if re.fullmatch(r"[A-Za-z]+[A-Z][a-z]+\w*", core): # PascalCase object names (BoardFrame)
        return False
    if " " not in core.strip() and any(c.islower() for c in core) and not any(ord(c) > 127 for c in core):
        return core.strip() in SINGLE_WORDS            # single mixed-case words are object names, except these
    if re.fullmatch(r"[A-Z]{1,3}:", core.strip()) or re.fullmatch(r"R[A-Z]", core.strip()):  # code prefixes (RP:, RG)
        return False
    if " " not in core and any(c.isdigit() for c in core) and len(core) < 10:
        return False                                   # save keys and versions (RS8B1)
    if " " not in core and len(core) > 24:             # alphabets and other character sets
        return False
    if core.strip().startswith("music_") or core.startswith("http"):
        return False
    return True


def extract():
    found = {}
    for top in SCAN:
        for dirpath, _, files in os.walk(os.path.join(SCRIPTS, top)):
            for f in sorted(files):
                if not f.endswith(".cs") or f in SKIP_FILES:
                    continue
                path = os.path.join(dirpath, f)
                src = open(path, encoding="utf-8").read()
                lines = src.split("\n")
                skip = set()
                for (fname, start) in SKIP_REGIONS:
                    if f != fname:
                        continue
                    for idx, l in enumerate(lines):
                        if start in l:
                            k = idx
                            while k < len(lines) and "};" not in lines[k]:
                                skip.add(k + 1)
                                k += 1
                            skip.add(k + 1)
                for kind, body, line in tokenize(src):
                    if line in skip or SKIP_LINE.search(lines[line - 1] if line - 1 < len(lines) else ""):
                        continue
                    if kind == "interp":
                        text, exprs = template(body)
                        cands = [text] + [x for e in exprs for x in nested_strings(e)]
                    else:
                        cands = [unescape(body)]
                    for t in cands:
                        t2 = t.strip()
                        if is_player_text(t2):
                            found.setdefault(t2, "%s:%d" % (os.path.relpath(path, SCRIPTS), line))
    return found


def escape(s):
    return s.replace("\t", "\\t").replace("\n", "\\n")


def main():
    found = extract()
    keys = sorted(found)
    if "--missing" in sys.argv:
        for lang in ("es", "ja"):
            p = os.path.join(ROOT, "Tools", "loc", lang + ".tsv")
            have = set()
            if os.path.exists(p):
                for l in open(p, encoding="utf-8"):
                    if "\t" in l:
                        have.add(l.split("\t", 1)[0])
            miss = [k for k in keys if escape(k) not in have]
            print(lang, "missing", len(miss))
            for k in miss[:50]:
                print("   ", escape(k))
        return
    with open(OUT, "w", encoding="utf-8") as fh:
        for k in keys:
            fh.write(escape(k) + "\n")
    with open(OUT.replace(".txt", "_context.tsv"), "w", encoding="utf-8") as fh:
        for k in keys:
            fh.write(escape(k) + "\t" + found[k] + "\n")
    print("wrote", len(keys), "strings to", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
