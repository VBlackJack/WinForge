from __future__ import annotations

import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Iterable, List, Set

REPO_ROOT = Path(__file__).resolve().parents[1]
DB_PATH = REPO_ROOT / "Apps" / "Database" / "applications.json"
PROFILES_DIR = REPO_ROOT / "Profiles"


def load_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as fh:
        return json.load(fh)


def analyze_database() -> Dict[str, List[str]]:
    report: Dict[str, List[str]] = {
        "errors": [],
        "warnings": [],
        "notes": [],
    }

    data = load_json(DB_PATH)
    applications: Dict[str, dict] = data.get("Applications", {})

    total_declared = data.get("TotalApplications")
    total_actual = len(applications)
    if total_declared != total_actual:
        report["errors"].append(
            f"TotalApplications metadata ({total_declared}) does not match actual count ({total_actual})."
        )

    priorities = {}
    duplicate_priorities: Dict[int, List[str]] = {}
    for app_id, app in applications.items():
        priority = app.get("DefaultPriority")
        if isinstance(priority, int):
            if priority in priorities:
                duplicate_priorities.setdefault(priority, [priorities[priority]]).append(app_id)
            else:
                priorities[priority] = app_id
        else:
            report["warnings"].append(
                f"Application '{app_id}' is missing an integer DefaultPriority (found: {priority!r})."
            )

    if duplicate_priorities:
        for priority, apps in sorted(duplicate_priorities.items()):
            report["errors"].append(
                "DefaultPriority {priority} is reused by applications: {apps}".format(
                    priority=priority,
                    apps=", ".join(apps),
                )
            )

    if priorities:
        max_priority = max(priorities)
        expected = set(range(1, max_priority + 1))
        missing = sorted(expected - set(priorities))
        if missing:
            report["warnings"].append(
                "Missing DefaultPriority values: {values}".format(values=", ".join(map(str, missing)))
            )

    verification_dates = Counter(app.get("LastVerified") for app in applications.values())
    future_dates = [date for date in verification_dates if date and date > data.get("LastUpdated", "")]
    if future_dates:
        report["warnings"].append(
            "Applications have LastVerified dates newer than database LastUpdated metadata: {dates}".format(
                dates=", ".join(sorted(future_dates))
            )
        )

    last_updated = data.get("LastUpdated")
    try:
        if last_updated:
            last_updated_dt = datetime.fromisoformat(last_updated)
            if last_updated_dt.date() > datetime.now(timezone.utc).date():
                report["warnings"].append(
                    "Database LastUpdated metadata ({value}) is in the future relative to today's date.".format(
                        value=last_updated
                    )
                )
    except ValueError:
        report["warnings"].append(
            "Database LastUpdated metadata ('{value}') is not a valid ISO date.".format(value=last_updated)
        )

    # Validate category statistics when present in metadata.
    categories_meta = data.get("Categories", {})
    if categories_meta:
        category_counts = Counter(app.get("Category") for app in applications.values())
        missing_categories = sorted(
            set(category_counts) - set(categories_meta)
        )
        if missing_categories:
            report["warnings"].append(
                "Categories missing metadata entries: {categories}".format(
                    categories=", ".join(missing_categories)
                )
            )

        extra_categories = sorted(
            set(categories_meta) - set(category_counts)
        )
        if extra_categories:
            report["warnings"].append(
                "Category metadata includes unused categories: {categories}".format(
                    categories=", ".join(extra_categories)
                )
            )

        for category, meta in categories_meta.items():
            declared_count = meta.get("Count")
            actual_count = category_counts.get(category, 0)
            if declared_count != actual_count:
                report["warnings"].append(
                    "Category '{category}' declares {declared} apps but database contains {actual}.".format(
                        category=category,
                        declared=declared_count,
                        actual=actual_count,
                    )
                )

    report["notes"].append(
        f"Database analysis completed for {total_actual} applications across {len(priorities)} priority entries."
    )
    return report


def resolve_profile_applications(profile_id: str, seen: Set[str] | None = None) -> List[str]:
    if seen is None:
        seen = set()
    if profile_id in seen:
        raise ValueError(f"Circular inheritance detected for profile '{profile_id}'.")
    seen.add(profile_id)

    profile_path = PROFILES_DIR / f"{profile_id}.json"
    if not profile_path.exists():
        raise FileNotFoundError(f"Profile definition '{profile_id}' not found at {profile_path}.")

    profile_data = load_json(profile_path)
    inherited_apps: List[str] = []
    for parent in profile_data.get("Inherits", []):
        inherited_apps.extend(resolve_profile_applications(parent, seen.copy()))

    own_apps = profile_data.get("Applications", [])
    return inherited_apps + own_apps


def analyze_profiles(applications: Dict[str, dict]) -> Dict[str, List[str]]:
    report: Dict[str, List[str]] = {
        "errors": [],
        "warnings": [],
        "notes": [],
    }

    for profile_path in sorted(PROFILES_DIR.glob("*.json")):
        profile_data = load_json(profile_path)
        profile_id = profile_path.stem
        declared_apps = profile_data.get("Applications", [])
        resolved_apps = resolve_profile_applications(profile_id)
        missing_apps = [app for app in resolved_apps if app not in applications]
        if missing_apps:
            report["errors"].append(
                "Profile '{profile}' references applications missing from database: {apps}".format(
                    profile=profile_id,
                    apps=", ".join(sorted(set(missing_apps))),
                )
            )

        duplicates = [app for app, count in Counter(resolved_apps).items() if count > 1]
        if duplicates:
            report["warnings"].append(
                "Profile '{profile}' contains duplicate application entries after inheritance: {apps}".format(
                    profile=profile_id,
                    apps=", ".join(sorted(duplicates)),
                )
            )

        metadata_expected = {
            "Base": 30,
            "Office": 35,
            "Gaming": 39,
            "Personnel": 64,
        }
        expected_count = metadata_expected.get(profile_id)
        if expected_count is not None:
            resolved_unique = len(dict.fromkeys(resolved_apps))
            if resolved_unique != expected_count:
                report["warnings"].append(
                    "Profile '{profile}' declares {expected} apps but resolved unique count is {actual}.".format(
                        profile=profile_id,
                        expected=expected_count,
                        actual=resolved_unique,
                    )
                )

        report["notes"].append(
            "Profile '{profile}' resolved to {count} applications ({unique} unique).".format(
                profile=profile_id,
                count=len(resolved_apps),
                unique=len(dict.fromkeys(resolved_apps)),
            )
        )

    return report


def merge_reports(*reports: Dict[str, List[str]]) -> Dict[str, List[str]]:
    merged = {"errors": [], "warnings": [], "notes": []}
    for report in reports:
        for key in merged:
            merged[key].extend(report.get(key, []))
    return merged


def main() -> None:
    database = load_json(DB_PATH)
    applications = database.get("Applications", {})

    db_report = analyze_database()
    profile_report = analyze_profiles(applications)

    report = merge_reports(db_report, profile_report)

    print("=== Win11Forge Consistency Report ===")
    for key in ("errors", "warnings", "notes"):
        entries = report[key]
        print(f"\n{key.upper()} ({len(entries)}):")
        if entries:
            for item in entries:
                print(f" - {item}")
        else:
            print(" - None")


if __name__ == "__main__":
    main()
