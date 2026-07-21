import io
import re
import subprocess
import sys

COMMENT = re.compile(r"^\s*(//|/\*|\*(?!/))")
EMOJI = re.compile(
    "["
    "\U0001f300-\U0001faff"
    "\U00002600-\U000027bf"
    "\U0001f1e6-\U0001f1ff"
    "←-⇿"
    "⬀-⯿"
    "️"
    "]"
)

SOURCE_SUFFIXES = (".cs", ".java")
TEXT_SUFFIXES = (".cs", ".java", ".axaml", ".xml", ".md", ".yml", ".yaml", ".json")
SKIP_PREFIXES = ("brand/generated/",)


def tracked_files():
    out = subprocess.run(
        ["git", "ls-files"], capture_output=True, text=True, check=True
    ).stdout
    return [p for p in out.splitlines() if p and not p.startswith(SKIP_PREFIXES)]


def read(path):
    try:
        return io.open(path, encoding="utf-8", errors="replace").read().splitlines()
    except OSError:
        return []


def main():
    failures = []

    for path in tracked_files():
        if not path.startswith(("src/", "mod/")) and not path.endswith(".md"):
            continue

        lines = read(path)

        if path.endswith(SOURCE_SUFFIXES):
            for n, line in enumerate(lines, 1):
                if COMMENT.match(line):
                    failures.append("%s:%d comment: %s" % (path, n, line.strip()))

        if path.endswith(TEXT_SUFFIXES):
            for n, line in enumerate(lines, 1):
                if EMOJI.search(line):
                    failures.append("%s:%d emoji: %s" % (path, n, line.strip()))

    if failures:
        print("Convention violations found:")
        for failure in failures:
            print("  " + failure)
        return 1

    print("Conventions check passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
