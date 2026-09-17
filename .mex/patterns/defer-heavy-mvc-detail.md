---
name: defer-heavy-mvc-detail
description: Keep MVC detail navigation responsive by rendering an authorization-aware shell before loading collection-heavy analysis asynchronously.
triggers:
  - "slow detail page"
  - "defer MVC detail"
  - "detail placeholders"
  - "load analysis asynchronously"
edges:
  - target: "add-mvc-feature.md"
    condition: "when changing the controller, Razor views, or browser interaction"
  - target: "../context/conventions.md"
    condition: "when validating project coding conventions"
last_updated: 2026-09-16
---

# Defer Heavy MVC Detail

## Context
Use this pattern when navigation blocks on relationship graphs, history, votes, analysis, or other collection-heavy EF reads. The initial action must still enforce visibility and return enough scalar data to identify the requested resource.

## Steps
1. Project only the authorized scalar fields needed for title, summary, timestamps, and navigation in the initial controller action.
2. Return a dedicated shell view containing stable skeleton dimensions and an `aria-live` host with the deferred endpoint URL in a data attribute.
3. Move the existing complete detail projection into a separate GET action that repeats the same authorization boundary and returns a layout-free partial.
4. Start `fetch` directly from the shared DOM-ready initialization. Fetch is non-blocking; avoid `requestAnimationFrame` or timers because inactive tabs and automated browser contexts may throttle them indefinitely.
5. Replace the host HTML on success and expose an inline retry state on failure. Pass the request cancellation token through both EF query paths.

## Gotchas
- Do not trust authorization established by the shell request; the deferred endpoint is independently callable.
- Keep the shell projection narrow. Including navigation collections defeats the split.
- A Razor partial returned over fetch must set `Layout = null` or it will inject a second page shell.
- Keep the loading host in the DOM after replacement if browser code relies on its busy state.
- Browser-check the actual network request. Seeing placeholders only proves the shell rendered.

## Verify
- Build the MVC project.
- Confirm the initial document reaches DOM-ready quickly and shows the real title plus placeholders.
- Confirm exactly one deferred request returns `200` and the placeholders become full panels.
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
