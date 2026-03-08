#!/usr/bin/env python3
"""Fetch GitHub Changelog RSS entries for a specified date.

Usage:
    python fetch_changelog.py <YYYY-MM-DD>

Outputs a JSON array of matching entries to stdout.
Each entry has: title, link, date (YYYY-MM-DD), description (plain text).
"""

import html
import json
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime


FEED_URL = "https://github.blog/changelog/feed/"


def fetch_feed(url: str) -> str:
    req = urllib.request.Request(
        url,
        headers={"User-Agent": "github-changelog-skill/1.0"},
    )
    with urllib.request.urlopen(req, timeout=30) as response:  # noqa: S310
        return response.read().decode("utf-8")


def parse_date(date_str: str) -> datetime | None:
    """Parse RFC 2822 or ISO 8601 date strings to an aware datetime."""
    date_str = date_str.strip()
    # RFC 2822 (RSS pubDate)
    try:
        return parsedate_to_datetime(date_str)
    except Exception:
        pass
    # ISO 8601 with timezone offset (Atom)
    for fmt in ("%Y-%m-%dT%H:%M:%S%z", "%Y-%m-%dT%H:%M:%SZ"):
        try:
            return datetime.strptime(date_str, fmt)
        except ValueError:
            continue
    # ISO 8601 date only
    try:
        return datetime.strptime(date_str, "%Y-%m-%d").replace(tzinfo=timezone.utc)
    except ValueError:
        pass
    return None


def strip_html(text: str) -> str:
    """Remove HTML tags and decode HTML entities."""
    text = re.sub(r"<[^>]+>", "", text)
    text = html.unescape(text)
    # Collapse extra whitespace
    text = re.sub(r"\s+", " ", text).strip()
    return text


def extract_entries(xml_text: str, target_date: str) -> list[dict]:
    """Extract feed entries whose publication date matches target_date (YYYY-MM-DD)."""
    root = ET.fromstring(xml_text)
    entries: list[dict] = []

    # --- RSS 2.0 ---
    if root.tag in ("rss",) or root.tag.endswith("}rss"):
        channel = root.find("channel")
        if channel is None:
            return entries
        for item in channel.findall("item"):
            title = _text(item, "title")
            link = _text(item, "link")
            pub_date_str = _text(item, "pubDate")
            # Prefer content:encoded over description for richer text
            content_ns = "{http://purl.org/rss/1.0/modules/content/}encoded"
            description = _text(item, content_ns) or _text(item, "description")

            pub_date = parse_date(pub_date_str) if pub_date_str else None
            if pub_date and pub_date.strftime("%Y-%m-%d") == target_date:
                entries.append({
                    "title": title,
                    "link": link,
                    "date": target_date,
                    "description": strip_html(description),
                })
        return entries

    # --- Atom ---
    ns = root.tag.split("}")[0][1:] if "}" in root.tag else ""

    def q(name: str) -> str:
        return f"{{{ns}}}{name}" if ns else name

    for entry in root.findall(q("entry")):
        title = _text(entry, q("title"))
        link_el = entry.find(q("link"))
        link = link_el.get("href", "") if link_el is not None else ""
        date_el = entry.find(q("published")) or entry.find(q("updated"))
        date_str = date_el.text if date_el is not None else ""
        summary_el = entry.find(q("content")) or entry.find(q("summary"))
        description = summary_el.text or "" if summary_el is not None else ""

        pub_date = parse_date(date_str) if date_str else None
        if pub_date and pub_date.strftime("%Y-%m-%d") == target_date:
            entries.append({
                "title": title,
                "link": link,
                "date": target_date,
                "description": strip_html(description),
            })

    return entries


def _text(element: ET.Element, tag: str) -> str:
    child = element.find(tag)
    return (child.text or "").strip() if child is not None else ""


def main() -> None:
    if len(sys.argv) < 2:
        print("Usage: fetch_changelog.py <YYYY-MM-DD>", file=sys.stderr)
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
