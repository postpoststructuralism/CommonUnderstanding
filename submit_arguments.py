"""
submit_arguments.py
===================
Playwright script to submit "for" and "against" arguments for every belief system
in the Reference Library (117 systems across 7 JSON files).

Usage:
    python submit_arguments.py [--dry-run] [--start-from N] [--for-only|--against-only]

The script:
1. Logs into the Common Understanding app at https://localhost:44347
2. Reads all belief systems from Data/BeliefSystems/*.json
3. For each system, generates a "for" argument (supporting) and "against" argument (critiquing)
4. Submits each via POST /Argument/Submit
5. Waits briefly after each submission to let the SSE analysis start
"""

import asyncio
import json
import os
import sys
import argparse
import random
from pathlib import Path
from playwright.async_api import async_playwright, Page

# ── Configuration ────────────────────────────────────────────────────────────

BASE_URL = "https://localhost:7187"
USERNAME = "researcher"
PASSWORD = "Research123!"
BELIEF_SYSTEMS_DIR = Path(__file__).parent / "CommonUnderstanding" / "Data" / "BeliefSystems"

# Ignore SSL certificate errors for localhost dev
BROWSER_CONTEXT_OPTS = {"ignore_https_errors": True}

# Delay between submissions (seconds) to avoid overwhelming the server
SUBMIT_DELAY = 3.0

# ── Argument Templates ───────────────────────────────────────────────────────

FOR_TEMPLATES = [
    "I support {name} because {principle}. This belief system provides a coherent framework for understanding existence and morality. Its emphasis on {value} has guided countless individuals toward meaningful lives. The core teaching that {teaching} offers profound wisdom that remains relevant today. While no system is perfect, {name} has demonstrated remarkable resilience and adaptability across centuries and cultures.",
    "{name} offers a compelling worldview grounded in {principle}. I find its approach to {value} particularly persuasive because it addresses fundamental human needs for purpose and connection. The tradition's longevity speaks to its ability to resonate across generations. Its teachings on {teaching} provide practical guidance for navigating life's challenges with grace and wisdom.",
    "I believe {name} deserves serious consideration because of its rich intellectual tradition around {principle}. The system's framework for understanding {value} is both rigorous and accessible. Throughout history, its adherents have contributed immensely to philosophy, art, and social organization. The principle of {teaching} is especially valuable in today's fragmented world.",
    "The wisdom of {name} lies in its nuanced understanding of {principle}. I am drawn to how it conceptualizes {value} — not as an abstract ideal but as a lived practice. Its teachings on {teaching} have proven transformative for millions. In an age of moral confusion, {name} offers a grounded ethical compass that deserves our attention and respect.",
    "{name} represents one of humanity's most sophisticated attempts to grapple with {principle}. Its approach to {value} is both intellectually satisfying and emotionally resonant. The tradition's emphasis on {teaching} provides a much-needed counterbalance to modern materialism. I find its framework for understanding the human condition to be remarkably comprehensive and insightful.",
]

AGAINST_TEMPLATES = [
    "While {name} has historical significance, I question whether {principle} can withstand modern scrutiny. The system's stance on {value} may have served past societies but creates problems in contemporary contexts. Its teaching that {teaching} can be interpreted in ways that limit human flourishing. We should critically examine whether these traditional frameworks still serve us today.",
    "I have reservations about {name} because its core claim about {principle} lacks empirical support. The emphasis on {value} can sometimes lead to dogmatic thinking that stifles inquiry. Furthermore, the doctrine of {teaching} has been used to justify practices that many now consider problematic. A more evidence-based approach to understanding reality would be preferable.",
    "{name} presents challenges when its principle of {principle} is applied rigidly. The focus on {value} can marginalize those who hold different perspectives. Historically, the teaching that {teaching} has sometimes been weaponized to maintain power structures. We need frameworks that are more inclusive and adaptable to diverse human experiences.",
    "I find {name} problematic in its treatment of {principle}. The system's conception of {value} often fails to account for the full complexity of human experience. Its insistence on {teaching} can become a barrier to progress and critical thinking. While respecting cultural traditions, we must also be willing to move beyond frameworks that no longer serve human well-being.",
    "The limitations of {name} become apparent when we examine {principle} through a contemporary lens. Its approach to {value} reflects the biases and limited knowledge of its originating era. The teaching that {teaching}, while perhaps well-intentioned, can perpetuate harmful patterns. A more dynamic, evidence-informed worldview would better serve humanity's evolving needs.",
]

# ── Helper Functions ─────────────────────────────────────────────────────────

def load_belief_systems() -> list[dict]:
    """Load all belief systems from the JSON files."""
    systems = []
    json_files = sorted(BELIEF_SYSTEMS_DIR.glob("*.json"))
    if not json_files:
        print(f"ERROR: No JSON files found in {BELIEF_SYSTEMS_DIR}")
        sys.exit(1)

    for fp in json_files:
        with open(fp, "r", encoding="utf-8") as f:
            data = json.load(f)
            for item in data:
                item["_source_file"] = fp.name
                systems.append(item)

    print(f"Loaded {len(systems)} belief systems from {len(json_files)} files.")
    return systems


def pick(items: list[str]) -> str:
    """Pick a random item from a list."""
    return random.choice(items)


def generate_argument(system: dict, stance: str) -> str:
    """Generate a for or against argument for a belief system."""
    name = system.get("Name", "this belief system")
    principles = system.get("CorePrinciples", ["understanding the world", "living ethically"])
    principle = pick(principles) if principles else "its foundational teachings"

    # Extract a short value-like phrase from a principle
    values = [
        "compassion and wisdom",
        "justice and order",
        "personal transformation",
        "community harmony",
        "spiritual liberation",
        "moral clarity",
        "inner peace",
        "social cohesion",
        "intellectual rigor",
        "ethical living",
    ]
    value = pick(values)

    # Pick a different principle for the teaching part
    teaching = pick([p for p in principles if p != principle]) if len(principles) > 1 else principle

    if stance == "for":
        template = pick(FOR_TEMPLATES)
    else:
        template = pick(AGAINST_TEMPLATES)

    return template.format(name=name, principle=principle, value=value, teaching=teaching)


async def login(page: Page) -> bool:
    """Log into the application. Returns True on success."""
    print("Logging in...")
    await page.goto(f"{BASE_URL}/Account/Login", wait_until="domcontentloaded")

    # Fill credentials
    await page.fill("#username", USERNAME)
    await page.fill("#password", PASSWORD)

    # Click sign in — use the button text
    await page.click("button:has-text('Sign In')")

    # Wait for navigation away from login page
    await page.wait_for_load_state("domcontentloaded")
    await asyncio.sleep(1)  # Extra beat for cookie auth to settle

    # Check if we're still on the login page (failed)
    if "/Account/Login" in page.url:
        # Check for error message
        error = await page.query_selector(".alert-danger")
        if error:
            error_text = await error.inner_text()
            print(f"  Login failed: {error_text.strip()}")
        else:
            print("  Login failed — still on login page.")
        return False

    print(f"  Logged in successfully. Current URL: {page.url}")
    return True


async def submit_argument(page: Page, system_name: str, argument_text: str, stance: str, index: int) -> bool:
    """Submit a single argument. Returns True on success."""
    print(f"  [{index}] Submitting {stance} argument for: {system_name}")

    try:
        # Navigate to submit page
        await page.goto(f"{BASE_URL}/Argument/Submit", wait_until="domcontentloaded")
        await asyncio.sleep(0.5)  # Let the page settle

        # Fill the textarea
        await page.fill("#argumentText", argument_text)

        # Click submit — use the form's submit button specifically
        await page.click("#submitForm button[type='submit']")

        # Wait for redirect to Analyze page
        await page.wait_for_url("**/Argument/Analyze/*", timeout=15000)

        # We should now be on /Argument/Analyze/{id}
        if "/Argument/Analyze/" in page.url:
            print(f"    ✓ Submitted successfully → {page.url}")
            return True
        elif "/Argument/Submit" in page.url:
            # Might have validation error
            validation_error = await page.query_selector(".field-validation-error")
            if validation_error:
                err_text = await validation_error.inner_text()
                print(f"    ✗ Validation error: {err_text.strip()}")
            else:
                print(f"    ✗ Stayed on submit page — unknown error")
            return False
        else:
            print(f"    ? Unexpected redirect to: {page.url}")
            return True  # Count as success if we navigated somewhere

    except Exception as e:
        print(f"    ✗ Exception: {e}")
        return False


async def main():
    parser = argparse.ArgumentParser(description="Submit arguments for all belief systems")
    parser.add_argument("--dry-run", action="store_true", help="Print arguments without submitting")
    parser.add_argument("--start-from", type=int, default=0, help="Start from belief system index N (0-based)")
    parser.add_argument("--for-only", action="store_true", help="Only submit 'for' arguments")
    parser.add_argument("--against-only", action="store_true", help="Only submit 'against' arguments")
    parser.add_argument("--max", type=int, default=0, help="Max number of systems to process (0 = all)")
    args = parser.parse_args()

    systems = load_belief_systems()

    # Apply start-from and max
    if args.start_from > 0:
        systems = systems[args.start_from:]
        print(f"Starting from index {args.start_from}, {len(systems)} systems remaining.")

    if args.max > 0:
        systems = systems[:args.max]
        print(f"Limited to {args.max} systems.")

    if args.dry_run:
        print("\n── DRY RUN — printing arguments without submitting ──\n")
        for i, system in enumerate(systems):
            name = system.get("Name", "Unknown")
            print(f"\n{'='*60}")
            print(f"[{i}] {name} ({system.get('Category', 'N/A')})")
            print(f"{'='*60}")

            if not args.against_only:
                for_arg = generate_argument(system, "for")
                print(f"\nFOR ({len(for_arg)} chars):\n{for_arg}")

            if not args.for_only:
                against_arg = generate_argument(system, "against")
                print(f"\nAGAINST ({len(against_arg)} chars):\n{against_arg}")
        return

    # ── Real submission ──────────────────────────────────────────────────────
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=False)  # Visible browser for debugging
        context = await browser.new_context(**BROWSER_CONTEXT_OPTS)
        page = await context.new_page()

        # Login
        if not await login(page):
            print("Cannot proceed without login.")
            await browser.close()
            return

        total_success = 0
        total_fail = 0

        for i, system in enumerate(systems):
            name = system.get("Name", "Unknown")
            print(f"\n{'─'*50}")
            print(f"[{i+1}/{len(systems)}] {name}")

            # Submit "for" argument
            if not args.against_only:
                for_arg = generate_argument(system, "for")
                success = await submit_argument(page, name, for_arg, "FOR", i + 1)
                if success:
                    total_success += 1
                else:
                    total_fail += 1
                await asyncio.sleep(SUBMIT_DELAY)

            # Submit "against" argument
            if not args.for_only:
                against_arg = generate_argument(system, "against")
                success = await submit_argument(page, name, against_arg, "AGAINST", i + 1)
                if success:
                    total_success += 1
                else:
                    total_fail += 1
                await asyncio.sleep(SUBMIT_DELAY)

        print(f"\n{'='*60}")
        print(f"DONE. Success: {total_success}, Failed: {total_fail}")
        print(f"{'='*60}")

        # Keep browser open for a moment so user can see final state
        await asyncio.sleep(2)
        await browser.close()


if __name__ == "__main__":
    asyncio.run(main())