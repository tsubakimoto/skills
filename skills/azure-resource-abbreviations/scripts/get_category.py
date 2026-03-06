#!/usr/bin/env python3
"""
List all resources in a specific Azure category.
Usage: python get_category.py "Networking"
"""

import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from azure_abbreviations import get_category_resources, get_all_categories


def main():
    if len(sys.argv) < 2:
        print("Usage: python get_category.py <category_name>")
        print("\nAvailable categories:")
        for cat in sorted(get_all_categories()):
            print(f"  - {cat}")
        sys.exit(1)
    
    category_name = " ".join(sys.argv[1:])
    resources = get_category_resources(category_name)
    
    if not resources:
        print(f"✗ Category '{category_name}' not found")
        print("\nAvailable categories:")
        for cat in sorted(get_all_categories()):
            print(f"  - {cat}")
        sys.exit(1)
    
    print(f"[{category_name}]\n")
    print(f"{'Resource':<50} {'Abbreviation':<15}")
    print("-" * 65)
    
    for resource_name, abbreviation in sorted(resources.items()):
        print(f"{resource_name:<50} {abbreviation:<15}")
    
    print(f"\nTotal: {len(resources)} resources")


if __name__ == "__main__":
    main()
