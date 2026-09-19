---
name: defer-heavy-mvc-detail
description: Keep MVC detail navigation responsive by rendering an authorization-aware shell before loading collection-heavy analysis asynchronously.
triggers:
  - "slow detail page"
  - "defer MVC detail"
  - "detail placeholders"
  - "load analysis asynchronously"
edges:
  - target: "patterns/add-mvc-feature.md"
    condition: "when changing the controller, Razor views, or browser interaction"
  - target: "context/conventions.md"
    condition: "when validating project coding conventions"
last_updated: 2026-09-19
---

# Defer Heavy MVC Detail

## Context
Use this pattern when navigation blocks on relationship graphs, history, votes, analysis, or other collection-heavy EF reads. The initial action must still enforce visibility and return enough scalar data to identify the requested resource.

## Steps
1. Project only the authorized scalar fields needed for title, summary, timestamps, and navigation in the initial controller action.
2. Return a dedicated shell view containing stable skeleton dimensions and independently addressable `aria-live` fragment hosts.
3. Split summary, social context, and detailed analysis into bounded GET actions that each repeat the authorization boundary and return layout-free partials.
4. Start the small summary and social-context fetches together from the shared DOM-ready initialization, then start the larger detailed-analysis fetch after those immediate requests settle.
5. Load collection data with separate bounded queries instead of one sibling-collection include graph. Prefer direct relationship predicates over local key-array `Contains` when either translation is practical.
6. Replace each host independently on success and expose a fragment-local retry state on failure. Pass the request cancellation token through every EF query path.

## Gotchas
- Do not trust authorization established by the shell request; the deferred endpoint is independently callable.
- Keep the shell projection narrow. Including navigation collections defeats the split.
- Neither `AsSplitQuery` nor `AsSingleQuery` makes a broad sibling-collection graph inherently safe. Split includes can serialize slow child commands, while one joined query can amplify rows multiplicatively and spend most of its time in EF materialization.
- SQL Server may translate a local key-array `Contains` predicate to `OPENJSON`. On the SocialView detail path, the first syllogism query using that shape repeatedly took about 25 seconds despite returning two rows; filtering through `Syllogism.Claim.ArgumentId` reduced the cold full fragment to 415 ms. Inspect generated command timings, not only endpoint or raw SQL timings.
- A Razor partial returned over fetch must set `Layout = null` or it will inject a second page shell.
- Keep the loading host in the DOM after replacement if browser code relies on its busy state.
- Browser-check the actual network request. Seeing placeholders only proves the shell rendered.

## Verify
- Build the MVC project.
- Confirm the initial document reaches DOM-ready quickly and shows the real title plus placeholders.
- Confirm each fragment request returns `200`, immediate fragments settle first, and all placeholders become full panels.
- Exercise concurrent detailed-fragment requests; bounded query behavior should not collapse under overlap.
- Confirm missing or unauthorized IDs return the same not-found behavior from both actions.
- Confirm failure state and Retry are keyboard-accessible.

## Debug
- If placeholders remain, inspect the loaded function body and check each DOM guard before changing server code.
- If no request starts, call the initializer manually and verify timer or animation-frame throttling is not involved.
- If the request succeeds but replacement fails, inspect whether the response contains a full layout or malformed partial markup.

## Update Scaffold
- [ ] Update `.mex/ROUTER.md` Current Project State if detail-loading behavior changed
- [ ] Update relevant `.mex/context/` facts if ownership or boundaries changed
- [ ] Add newly discovered browser or EF gotchas here
