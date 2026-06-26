# Dev Team Recommendation: Positioning Pivot for A Common Understanding
**Prepared for:** Development Team  
**Date:** June 25, 2026  
**Context:** Synthesizing consultant positioning assessment against current application state

---

## Executive Summary

The consultant has correctly diagnosed a tension between the application's technical sophistication and its public-facing identity. The current login screen — which leads with "Continue the work of finding durable common ground" and surfaces features like "Structured comparisons," "Live collaborative sessions," and "Private by default" — reads as a *professional facilitation tool*, not a platform people join organically. The backend (Chain Builder, Worldview Composer, Emergent Conclusions, Schwartz Values classifier) already encodes remarkable capability. The pivot required is not architectural — it is experiential and linguistic. The product must stop describing its *mechanics* and start revealing its *output*.

The recommendations below are prioritized for immediate, medium-term, and strategic implementation.

---

## Understanding the Gap: What the App Says vs. What It Is

The live application currently presents itself through a process-first lens. Every piece of copy on the login screen describes what the *user does* ("Track where users and systems align," "Bring multiple participants into one decision space"). This is accurate — but it is the wrong frame for acquisition.

What the application actually *produces* is a living, structured map of what groups of people believe, why they believe it, and where their underlying values converge despite apparent disagreement. That output — the synthesized map — is genuinely novel and has no mainstream equivalent. The consultant is correct that this is the real product, and the real product is not yet visible on the surface.

The Schwartz Values layer deepens this further. The application does not just log what people say; it models *why* they believe it against a validated scientific framework of 20 universal basic values. This is a claim that Wikipedia cannot make, that Reddit cannot make, and that no deliberation platform currently makes credibly.

---

## Priority 1 — Immediate: The "Synthesis-First" Landing Page

**Current State:** The landing page is a login wall. Authenticated users see a social feed of arguments.

**The Problem:** Asking a first-time visitor to authenticate before seeing any output is a trust deficit. The product is asking for a commitment before demonstrating value.

**Recommended Action:** Redesign the unauthenticated landing experience to lead with a *live synthesis panel* — a public-facing, read-only view of a current hot topic showing:
- The number of contributors to date
- Where they agree
- Where they disagree
- The strongest evidence on both sides (sourced from the argument chains in the backend)
- A Schwartz values heatmap showing the underlying value drivers of each position

This single change operationalizes the consultant's "AI Synthesis Test" without requiring any new backend capability — the data already exists. The only work is surfacing it publicly before the authentication gate.

**The Hook Mechanism:** Give visitors the answer first. If the synthesis is genuinely insightful, the visitor's natural next instinct is: *"My view is slightly different — I want it reflected in there."* That is the contribution prompt. The CTA shifts from "Sign In to use this tool" to "Add your perspective to this map."

---

## Priority 2 — Immediate: Language and Navigation Rename

The following renames require minimal development effort and have high positioning leverage. They should be treated as a single sprint item.

| Current Label | Proposed Label | Rationale |
|---|---|---|
| Social Feed | The Live Map | Signals a living document, not a scrollable timeline |
| Debate Room | Viewpoints / The Landscape | Removes adversarial framing; implies navigation rather than combat |
| Chain Builder | Trace the Reasoning | User-facing language; describes the experience, not the mechanism |
| Convergence | Where We Agree | Plain language; emotionally resonant |
| New Argument | Add Your Perspective | Reduces barrier; "argument" implies conflict, "perspective" implies contribution |
| Submit Arguments | Map the Discourse | Aligns with the Wikipedia-for-contested-questions positioning |

**Copy review:** The login screen's existing subheads are solid in tone ("Continue the work of finding durable common ground") but the *feature bullets* beneath them revert to tool language. Recommend replacing the three feature bullets with outcome language:

- ~~"Structured comparisons"~~ → *"See exactly where you agree — and why you don't"*
- ~~"Live collaborative sessions"~~ → *"Think together, in real time"*
- ~~"Private by default"~~ → *"Your worldview, mapped — in a space you control"*

---

## Priority 3 — Near-Term: The Simplification Sprint

The consultant's most important structural recommendation is *aggressive simplification of the visible interface*. The argument graph mechanics — while the engine of the application — should recede from the user's immediate awareness.

### What to Hide

- The graph/node visualization should not be the default view for a new post. It should be accessible via a "Trace the reasoning" expand — a secondary discovery for users who want depth.
- The Schwartz values scoring output should be visible as a result, not as a form field the user fills in. The classifier runs; the user sees the output framed as: *"This perspective draws most strongly on [Security] and [Universalism]."*
- Argument chains should *feel like threads*, not workflow diagrams. When a user reads a synthesis, and taps through to the underlying reasoning, it should feel like reading a well-cited article, not navigating a graph editor.

### What to Emphasize

- The synthesis output: the paragraph-level, AI-written summary of where contributors agree and disagree on a topic.
- The contribution count and recency: social proof that the map is alive and growing.
- The values heatmap: this is genuinely differentiating and should be promoted to a more visible position. No other platform shows *why* people disagree at the values level.

### The Posting Experience

When a user submits a new perspective, the interface should feel as low-friction as composing a tweet: a text field, a topic tag, and a submit button. All the structured reasoning enrichment (chain assignment, Schwartz scoring, convergence calculation) should happen invisibly in the backend and surface after submission as: *"Your perspective has been added to the map. Here's how it connects."*

---

## Priority 4 — Strategic: The "Knowledge Network Effect" Narrative

The consultant's framing of this as a *knowledge accumulation* platform rather than a *better discourse* platform is the correct long-term positioning move. It changes the answer to the question: "Why would I contribute here instead of posting on X?"

The answer the current app implies: *"Because the discourse here is more structured and respectful."* (Low urgency.)

The answer the repositioned app should imply: *"Because here, your contribution becomes part of a permanent, improving record of human understanding on this question — and you can see your impact on the map immediately."* (High urgency; low friction; immediate gratification.)

### Recommended Elevator Pitch (for website, onboarding, and external communications)

> *"Most social media is a conversation where everything disappears. A Common Understanding is the first platform where every discussion builds a living map of what humanity actually believes — and why. Don't just post. Contribute to the record."*

### Homepage Headline Progression

The current copy ("Strategic clarity across beliefs, values, and decisions") is positioning the app for enterprise facilitators and policy analysts. That audience is real, but it is not the audience that generates the network effect. Recommend a two-track headline approach:

- **Public / Discovery Track:** *"See what the world actually believes — and why."*
- **Contributor Track:** *"Your perspective belongs on the map."*

---

## What NOT to Do

- **Do not add more features to Chain Builder.** The backend is sufficiently capable. Every new feature added to the argument graph mechanics widens the gap between what the system can do and what a first-time visitor can understand.
- **Do not rename "Common Understanding" to something less grounded.** The name is strong. It is durable, non-partisan, and descriptive of the actual output.
- **Do not require topic expertise to contribute.** The contribution barrier must remain at the level of "share a perspective" — not "construct a valid argument chain." The enrichment is the system's job.
- **Do not over-invest in the "Debate Room" paradigm.** The adversarial frame will systematically attract a different (and more combative) user base than the knowledge-mapping frame. Every UX choice that signals "winner/loser" works against the retention of good-faith contributors.

---

## Summary of Development Priorities

| Priority | Action | Effort | Leverage |
|---|---|---|---|
| 1 | Live synthesis panel on unauthenticated landing page | Medium | Very High |
| 2 | Navigation and copy renames (see table above) | Low | High |
| 3 | Post submission UX — tweet-like entry, backend enrichment invisible | Medium | High |
| 4 | Surface Schwartz values output as post-submission result card | Low | High |
| 5 | Argument graph → secondary "Trace the reasoning" expand | Medium | Medium |
| 6 | Values heatmap promoted to topic overview page | Low–Medium | High |
| 7 | Public topic pages (read-only, pre-auth) | Medium–High | Very High |

The single highest-leverage change is Priority 1 combined with Priority 2. A visitor who lands on a live synthesis of a topic they care about, written in plain language, with a clear "Add your perspective" CTA, will understand the product in 10 seconds. That understanding is currently unavailable to any unauthenticated visitor.

---

## Closing Note on Positioning

The consultant's instinct — that this application is closer to "Wikipedia for contested questions" than to "Twitter but nicer" — is the right north star. Wikipedia succeeded not because it was a better encyclopedia, but because it made *contributing to a shared record* feel meaningful and permanent. The Schwartz values layer gives A Common Understanding a capability Wikipedia will never have: the ability to explain not just *what* people believe, but the motivational architecture underneath. That is the moat. The product should be built, named, and marketed around that output.
