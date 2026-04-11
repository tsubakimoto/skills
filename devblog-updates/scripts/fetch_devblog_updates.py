#!/usr/bin/env python3
"""Fetch Microsoft Developer Blogs RSS entries for a specified date.

Usage:
    python fetch_devblog_updates.py <YYYY-MM-DD>

Outputs a JSON array of matching entries to stdout.
Each entry has: title, link, date (YYYY-MM-DD), published_at, blog, blog_slug, author, description.
"""

import html
import json
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from urllib.parse import urlparse


FEED_URL = "https://devblogs.microsoft.com/landing"
CONTENT_NS = "{http://purl.org/rss/1.0/modules/content/}encoded"
DC_CREATOR_NS = "{http://purl.org/dc/elements/1.1/}creator"

TOKEN_REWRITES = {
    "ai": "AI",
    "api": "API",
    "aspnet": "ASP.NET",
    "azure": "Azure",
    "blog": "Blog",
    "copilot": "Copilot",
    "cpp": "C++",
    "csharp": "C#",
    "css": "CSS",
    "devops": "DevOps",
    "dotnet": ".NET",
    "github": "GitHub",
    "go": "Go",
    "html": "HTML",
    "ios": "iOS",
    "javascript": "JavaScript",
    "mcp": "MCP",
    "microsoft": "Microsoft",
    "sql": "SQL",
    "vs": "VS",
    "visualstudio": "Visual Studio",
    "vscode": "VS Code",
    "windows": "Windows",
    "xml": "XML",
}


def fetch_feed(url: str) -> str:
    req = urllib.request.Request(
        url,
        headers={"User-Agent": "devblog-updates-skill/1.0"},
    )
    with urllib.request.urlopen(req, timeout=30) as response:  # noqa: S310
        return response.read().decode("utf-8")


def parse_date(date_str: str) -> datetime | None:
    """Parse RFC 2822 or ISO 8601 date strings to an aware datetime."""
    date_str = date_str.strip()
    try:
        parsed = parsedate_to_datetime(date_str)
        return ensure_timezone(parsed)
    except Exception:
        pass

    try:
        return datetime.strptime(date_str, "%Y-%m-%dT%H:%M:%S%z")
    except ValueError:
        pass

    try:
        return datetime.strptime(date_str, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError:
        pass

    try:
        return datetime.strptime(date_str, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    except ValueError:
        return None


def ensure_timezone(value: datetime) -> datetime:
    return value if value.tzinfo is not None else value.replace(tzinfo=timezone.utc)


def strip_html(text: str) -> str:
    """Remove HTML tags and decode HTML entities."""
    text = re.sub(r"<[^>]+>", " ", text)
    text = html.unescape(text)
    return re.sub(r"\s+", " ", text).strip()


def format_blog_name(slug: str) -> str:
    if not slug:
        return "Microsoft Developer Blogs"

    parts = []
    for token in slug.split("-"):
        parts.append(TOKEN_REWRITES.get(token.lower(), token.capitalize()))
    return " ".join(parts)


def infer_blog_slug(link: str) -> str:
    path = urlparse(link).path.strip("/")
    if not path:
        return ""
    return path.split("/", 1)[0]


def to_utc_isoformat(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def extract_entries(xml_text: str, target_date: str) -> list[dict[str, str]]:
    """Extract feed entries whose publication date matches target_date (YYYY-MM-DD)."""
    root = ET.fromstring(xml_text)
    entries: list[dict[str, str]] = []

    if root.tag in ("rss",) or root.tag.endswith("}rss"):
        channel = root.find("channel")
        if channel is None:
            return entries

        for item in channel.findall("item"):
            title = _text(item, "title")
            link = _text(item, "link")
            pub_date_str = _text(item, "pubDate")
            description = _text(item, CONTENT_NS) or _text(item, "description")
            author = _text(item, DC_CREATOR_NS) or _text(item, "author")

            pub_date = parse_date(pub_date_str) if pub_date_str else None
            if pub_date and pub_date.strftime("%Y-%m-%d") == target_date:
                blog_slug = infer_blog_slug(link)
                entries.append(
                    {
                        "title": title,
                        "link": link,
                        "date": target_date,
                        "published_at": to_utc_isoformat(pub_date),
                        "blog": format_blog_name(blog_slug),
                        "blog_slug": blog_slug,
                        "author": author,
                        "description": strip_html(description),
                    }
                )

        return sorted(entries, key=lambda entry: entry["published_at"], reverse=True)

    ns = root.tag.split("}")[0][1:] if "}" in root.tag else ""

    def q(name: str) -> str:
        return f"{{{ns}}}{name}" if ns else name

    for entry in root.findall(q("entry")):
        title = _text(entry, q("title"))
        link_el = entry.find(q("link"))
        link = link_el.get("href", "") if link_el is not None else ""
        date_el = entry.find(q("published"))
        if date_el is None:
            date_el = entry.find(q("updated"))
        date_str = date_el.text if date_el is not None else ""
        summary_el = entry.find(q("content"))
        if summary_el is None:
            summary_el = entry.find(q("summary"))
        author_el = entry.find(q("author"))
        author_name_el = author_el.find(q("name")) if author_el is not None else None
        author = author_name_el.text.strip() if author_name_el is not None and author_name_el.text else ""
        description = summary_el.text or "" if summary_el is not None else ""

        pub_date = parse_date(date_str) if date_str else None
        if pub_date and pub_date.strftime("%Y-%m-%d") == target_date:
            blog_slug = infer_blog_slug(link)
            entries.append(
                {
                    "title": title,
                    "link": link,
                    "date": target_date,
                    "published_at": to_utc_isoformat(pub_date),
                    "blog": format_blog_name(blog_slug),
                    "blog_slug": blog_slug,
                    "author": author,
                    "description": strip_html(description),
                }
            )

    return sorted(entries, key=lambda entry: entry["published_at"], reverse=True)


def _text(element: ET.Element, tag: str) -> str:
    child = element.find(tag)
    return (child.text or "").strip() if child is not None and child.text is not None else ""


def main() -> None:
    if len(sys.argv) < 2:
        print("Usage: fetch_devblog_updates.py <YYYY-MM-DD>", file=sys.stderr)
        sys.exit(1)

    target_date = sys.argv[1]
    try:
        datetime.strptime(target_date, "%Y-%m-%d")
    except ValueError:
        print(f"Error: Invalid date '{target_date}'. Use YYYY-MM-DD format.", file=sys.stderr)
        sys.exit(1)

    print(f"Fetching {FEED_URL} ...", file=sys.stderr)
    try:
        xml_text = fetch_feed(FEED_URL)
    except Exception as exc:
        print(f"Error fetching feed: {exc}", file=sys.stderr)
        sys.exit(1)

    entries = extract_entries(xml_text, target_date)
    print(f"Found {len(entries)} entries for {target_date}.", file=sys.stderr)
    print(json.dumps(entries, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
