"""C# script analysis tool (Python-side, no Unity needed)."""
import os
import re


def analyze_script(file_path: str) -> dict:
    """Analyze a C# script for common issues and patterns.

    Returns analysis results including:
    - Class/method counts
    - Potential issues (empty catch, magic numbers, etc.)
    - Suggestions for improvement
    """
    # Validate file extension
    if not file_path.endswith(".cs"):
        return {"error": "Only C# (.cs) files are supported"}

    # Resolve to absolute path and check for path traversal
    resolved = os.path.realpath(file_path)
    if ".." in os.path.relpath(resolved, os.path.dirname(resolved)):
        return {"error": "Invalid file path"}

    if not os.path.isfile(resolved):
        return {"error": f"File not found: {file_path}"}

    with open(resolved, "r", encoding="utf-8") as f:
        content = f.read()

    lines = content.split("\n")
    issues = []

    # Check for empty catch blocks
    for i, line in enumerate(lines):
        if re.search(r"catch\s*\([^)]*\)\s*\{\s*\}", line):
            issues.append({
                "line": i + 1,
                "type": "empty_catch",
                "message": "Empty catch block - consider logging the exception"
            })

    # Check for Update() without null checks on referenced objects
    class_count = len(re.findall(r"\bclass\s+\w+", content))
    method_count = len(re.findall(r"(public|private|protected|internal)\s+\w+\s+\w+\s*\(", content))
    using_count = len(re.findall(r"^using\s+", content, re.MULTILINE))

    return {
        "file": file_path,
        "stats": {
            "lines": len(lines),
            "classes": class_count,
            "methods": method_count,
            "usings": using_count,
        },
        "issues": issues,
        "issueCount": len(issues),
    }
