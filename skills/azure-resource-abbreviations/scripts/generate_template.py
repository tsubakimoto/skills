#!/usr/bin/env python3
"""
Generate a naming template for a specific Azure resource type.
Usage: python generate_template.py "Virtual Machine"
"""

import sys
from pathlib import Path
import re

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent))

from azure_abbreviations import get_abbreviation, search_abbreviation


NAMING_TEMPLATES = {
    "standard": "{abbr}-{env}-{instance}-{region}",
    "simple": "{abbr}{env}{instance}",
    "descriptive": "{abbr}-{purpose}-{env}-{instance}",
}

ENVIRONMENT_CODES = {
    "dev": "Development",
    "test": "Testing",
    "stg": "Staging",
    "prod": "Production",
}

REGION_CODES = {
    "eus": "East US",
    "wus": "West US",
    "neu": "North Europe",
    "weu": "West Europe",
    "sea": "Southeast Asia",
    "eas": "East Asia",
    "jpeast": "Japan East",
}


def generate_examples(abbreviation: str) -> dict:
    """Generate naming examples for an abbreviation."""
    examples = {}
    
    # Standard template
    examples["standard"] = {
        "template": NAMING_TEMPLATES["standard"],
        "example": f"{abbreviation}-prod-web01-eus",
        "description": "Resource-Environment-Instance-Region"
    }
    
    # Simple template
    examples["simple"] = {
        "template": NAMING_TEMPLATES["simple"],
        "example": f"{abbreviation}prod1",
        "description": "ResourceEnvironmentInstance (no separators)"
    }
    
    # Descriptive template
    examples["descriptive"] = {
        "template": NAMING_TEMPLATES["descriptive"],
        "example": f"{abbreviation}-webserver-prod-01",
        "description": "Resource-Purpose-Environment-Instance"
    }
    
    return examples


def main():
    if len(sys.argv) < 2:
        print("Usage: python generate_template.py <resource_type>")
        print("\nExamples:")
        print("  python generate_template.py 'Virtual Machine'")
        print("  python generate_template.py 'Web App'")
        sys.exit(1)
    
    resource_type = " ".join(sys.argv[1:])
    
    # Try exact match first
    abbreviation = get_abbreviation(resource_type)
    if not abbreviation:
        # Try search
        results = search_abbreviation(resource_type)
        if results:
            first_category = list(results.keys())[0]
            first_resource = list(results[first_category].keys())[0]
            abbreviation = results[first_category][first_resource]
            print(f"Using '{first_resource}': {abbreviation}\n")
        else:
            print(f"✗ Resource type '{resource_type}' not found")
            sys.exit(1)
    
    print(f"Azure Resource: {resource_type}")
    print(f"Abbreviation: {abbreviation}\n")
    print("=" * 70)
    print("NAMING TEMPLATES\n")
    
    examples = generate_examples(abbreviation)
    
    for template_name, details in examples.items():
        print(f"[{template_name.upper()}]")
        print(f"Template:     {details['template']}")
        print(f"Example:      {details['example']}")
        print(f"Description:  {details['description']}")
        print()
    
    print("=" * 70)
    print("\nNAMING COMPONENTS\n")
    print("Environment Codes:")
    for code, meaning in ENVIRONMENT_CODES.items():
        print(f"  {code:<6} = {meaning}")
    
    print("\nCommon Region Codes:")
    for code, meaning in REGION_CODES.items():
        print(f"  {code:<8} = {meaning}")
    
    print("\n" + "=" * 70)
    print("\nBEST PRACTICES:")
    print("  • Use lowercase letters and numbers")
    print("  • Use hyphens (-) as separators")
    print("  • Keep names concise but descriptive")
    print("  • Include environment and region for production resources")
    print("  • Use consistent abbreviations across your organization")
    print("  • Respect character limits (varies by resource type)")


if __name__ == "__main__":
    main()
