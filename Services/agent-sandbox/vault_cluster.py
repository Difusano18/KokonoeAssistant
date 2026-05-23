from __future__ import annotations

import json
import os
import re
import sys
from collections import Counter, defaultdict


STOP_WORDS = {
    "about", "after", "again", "being", "could", "from", "have", "notes",
    "there", "these", "this", "that", "their", "with", "would",
    "було", "буде", "вони", "вона", "воно", "його", "коли", "мене",
    "мені", "може", "після", "просто", "треба", "через", "щось",
    "если", "когда", "меня", "может", "после", "просто", "тебе",
}

THEME_RULES = (
    ("Kokonoe / agent architecture", ("kokonoe", "agent", "агент", "мультизада", "архітект", "architecture", "manus")),
    ("Dialogue / memory", ("chat", "telegram", "діалог", "розмов", "пам", "memory", "context")),
    ("Health / body", ("сон", "sleep", "health", "пульс", "bpm", "stress", "втом", "харч")),
    ("Code / engineering", ("код", "code", "build", "test", "bug", "баг", "fix", "service", "engine")),
    ("Finance / expenses", ("finance", "expense", "витрат", "грош", "money", "budget")),
    ("Creator / profile", ("creator", "profile", "yasu", "vova", "identity", "профіль")),
)


def extract_keywords(text: str) -> list[str]:
    words = re.findall(r"[A-Za-zА-Яа-яІіЇїЄєҐґ0-9_'-]{4,}", text.lower())
    cleaned = []
    for word in words:
        word = word.strip("_'-")
        if len(word) < 4 or word in STOP_WORDS:
            continue
        cleaned.append(word)
    return cleaned


def classify_theme(path: str, keywords: list[str]) -> str:
    haystack = (path.lower() + " " + " ".join(keywords[:40]))
    scores = []
    for theme, needles in THEME_RULES:
        score = sum(1 for needle in needles if needle in haystack)
        if score:
            scores.append((score, theme))
    if scores:
        scores.sort(reverse=True)
        return scores[0][1]
    return keywords[0] if keywords else "uncategorized"


def read_note(path: str) -> str:
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except OSError:
        return ""


def preview(text: str) -> str:
    text = re.sub(r"\s+", " ", text).strip()
    text = re.sub(r"^---.*?---", "", text, flags=re.S)
    text = text.strip(" -*#")
    return text[:180]


def should_skip(root: str, file_name: str) -> bool:
    lower = (root + "/" + file_name).replace("\\", "/").lower()
    return any(
        token in lower
        for token in (
            "/.git/",
            "/bin/",
            "/obj/",
            "/kokonoe-data/",
            "/projectmemory/",
            "/architecture/",
            "/kokonoe/agent/",
        )
    )


def main() -> None:
    vault_path = globals().get("VAULT_PATH") or (sys.argv[1] if len(sys.argv) > 1 else os.getcwd())
    limit = int(globals().get("LIMIT") or (sys.argv[2] if len(sys.argv) > 2 else 80))
    if not os.path.isdir(vault_path):
        print(json.dumps({"error": f"vault path not found: {vault_path}"}, ensure_ascii=False))
        return

    candidates = []
    for root, _dirs, files in os.walk(vault_path):
        for file_name in files:
            if not file_name.lower().endswith(".md") or should_skip(root, file_name):
                continue
            full = os.path.join(root, file_name)
            try:
                mtime = os.path.getmtime(full)
            except OSError:
                mtime = 0
            candidates.append((mtime, full))

    candidates.sort(reverse=True)
    candidates = candidates[: max(1, limit)]

    grouped: dict[str, list[dict[str, object]]] = defaultdict(list)
    scanned = 0
    for _mtime, full in candidates:
        content = read_note(full)
        if len(content.strip()) < 40:
            continue
        rel = os.path.relpath(full, vault_path).replace("\\", "/")
        keywords = extract_keywords(rel + "\n" + content)
        if not keywords:
            continue
        theme = classify_theme(rel, keywords)
        grouped[theme].append(
            {
                "path": rel,
                "keywords": Counter(keywords).most_common(8),
                "preview": preview(content),
            }
        )
        scanned += 1

    clusters = []
    for theme, items in grouped.items():
        keyword_counter: Counter[str] = Counter()
        for item in items:
            keyword_counter.update(dict(item["keywords"]))
        files = [str(item["path"]) for item in items[:3]]
        top_keywords = [word for word, _count in keyword_counter.most_common(5)]
        clusters.append(
            {
                "theme": theme,
                "size": len(items),
                "files": files,
                "keywords": top_keywords,
                "observation": f"{len(items)} note(s) cluster around {theme}",
                "recommendation": build_recommendation(theme, files, top_keywords),
            }
        )

    clusters.sort(key=lambda cluster: (-int(cluster["size"]), str(cluster["theme"])))
    print(
        json.dumps(
            {
                "vault": vault_path,
                "scanned": scanned,
                "cluster_count": len(clusters),
                "clusters": clusters[:5],
            },
            ensure_ascii=False,
        )
    )


def build_recommendation(theme: str, files: list[str], keywords: list[str]) -> str:
    if not files:
        return "No actionable file anchor found."
    anchor = files[0]
    if "architecture" in theme.lower() or "engineering" in theme.lower():
        return f"Review {anchor} for TODOs and convert repeated ideas into tracked agent tasks."
    if "health" in theme.lower():
        return f"Check {anchor} against recent sleep/body logs before sending health-related nudges."
    if "dialogue" in theme.lower() or "memory" in theme.lower():
        return f"Use {anchor} as memory context before answering identity or history questions."
    if keywords:
        return f"Inspect {anchor}; recurring keyword '{keywords[0]}' looks like the strongest anchor."
    return f"Inspect {anchor}; it is the most recent non-empty note in this cluster."


if __name__ == "__main__":
    main()
