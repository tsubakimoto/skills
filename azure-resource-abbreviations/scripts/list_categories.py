#!/usr/bin/env python3
"""
List all Azure resource categories.
Usage: python list_categories.py
"""

import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from azure_abbreviations import get_all_categories


def main():
    categories = get_all_categories()
    
    print("Available Azure Resource Categories:\n")
    for i, category in enumerate(sorted(categories), 1):
        print(f"{i:2}. {category}")
    
    print(f"\nTotal: {len(categories)} categories")
    print("\nUse 'python get_category.py <category_name>' to see resources in a category")


if __name__ == "__main__":
    main()
