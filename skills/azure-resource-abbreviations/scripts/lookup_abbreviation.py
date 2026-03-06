#!/usr/bin/env python3
"""
Look up abbreviation for a specific Azure resource type.
Usage: python lookup_abbreviation.py "Virtual Machine"
"""

import sys
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from azure_abbreviations import get_abbreviation, search_abbreviation


def main():
    if len(sys.argv) < 2:
        print("Usage: python lookup_abbreviation.py <resource_type>")
        print("\nExamples:")
        print("  python lookup_abbreviation.py 'Virtual Machine'")
        print("  python lookup_abbreviation.py 'Web App'")
        print("  python lookup_abbreviation.py 'sql'")
        sys.exit(1)
    
    resource_type = " ".join(sys.argv[1:])
    
    # Try exact match first
    abbreviation = get_abbreviation(resource_type)
    if abbreviation:
        print(f"✓ {resource_type}: {abbreviation}")
        return
    
    # Try search if no exact match
    results = search_abbreviation(resource_type)
    if results:
        print(f"Found similar resources for '{resource_type}':\n")
        for category, resources in results.items():
            print(f"[{category}]")
            for res_name, abbr in resources.items():
                print(f"  {res_name}: {abbr}")
    else:
        print(f"✗ No abbreviation found for '{resource_type}'")
        sys.exit(1)


if __name__ == "__main__":
    main()
