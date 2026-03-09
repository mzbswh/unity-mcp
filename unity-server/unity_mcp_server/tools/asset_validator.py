"""Asset naming and structure validation tool (Python-side)."""
import os
import re
from typing import Optional


# Default naming conventions
NAMING_RULES = {
    ".cs": r"^[A-Z][a-zA-Z0-9]+\.cs$",        # PascalCase
    ".prefab": r"^[A-Z][a-zA-Z0-9_]+\.prefab$", # PascalCase with underscores
    ".mat": r"^[A-Z][a-zA-Z0-9_]+\.mat$",       # PascalCase with underscores
    ".asset": r"^[A-Z][a-zA-Z0-9_]+\.asset$",
    ".png": r"^[a-z][a-z0-9_]+\.png$",           # snake_case for textures
    ".jpg": r"^[a-z][a-z0-9_]+\.jpg$",
}

# Expected folder structure under Assets/
EXPECTED_FOLDERS = [
    "Scripts", "Prefabs", "Materials", "Textures",
    "Scenes", "Audio", "Animations", "Fonts",
]


def validate_assets(project_path: str, custom_rules: Optional[dict] = None) -> dict:
    """Validate asset naming conventions and folder structure.

    Args:
        project_path: Path to the Unity project root (contains Assets/)
        custom_rules: Optional dict of {extension: regex_pattern} to override defaults

    Returns:
        Validation report with violations and suggestions.
    """
    assets_path = os.path.join(project_path, "Assets")
    if not os.path.isdir(assets_path):
        return {"error": f"Assets folder not found at {assets_path}"}

    rules = {**NAMING_RULES, **(custom_rules or {})}
    violations = []
    file_count = 0
    folder_count = 0

    for root, dirs, files in os.walk(assets_path):
        folder_count += len(dirs)
        for filename in files:
            if filename.startswith("."):
                continue
            file_count += 1
            ext = os.path.splitext(filename)[1].lower()
            if ext in rules:
                pattern = rules[ext]
                if not re.match(pattern, filename):
                    rel_path = os.path.relpath(
                        os.path.join(root, filename), assets_path
                    )
                    violations.append({
                        "file": rel_path,
                        "rule": f"Expected pattern: {pattern}",
                        "suggestion": f"Rename to match convention for {ext} files"
                    })

    # Check expected folders
    missing_folders = []
    for folder in EXPECTED_FOLDERS:
        if not os.path.isdir(os.path.join(assets_path, folder)):
            missing_folders.append(folder)

    return {
        "projectPath": project_path,
        "stats": {
            "totalFiles": file_count,
            "totalFolders": folder_count,
        },
        "namingViolations": violations,
        "violationCount": len(violations),
        "missingFolders": missing_folders,
    }
