# Website Features and Motion Implementation Plan

> **For agentic workers:** Use executing-plans inline. The user requested background execution without repeated confirmation. Work in the existing directory on `fix/website-features-motion`; no extra checkout. Request a bounded read-only review before deployment.

**Goal:** Make the public OZL site explain its useful functions clearly and add restrained, accessible motion.

**Architecture:** Keep the existing static HTML/CSS/JS and five real screenshot pairs. Replace the bare tab strip with a feature navigator, visible summaries and selected detail bullets. Reuse the current validated release data and SSH deployment pipeline.

**Tech Stack:** HTML, CSS, browser Web Animations/IntersectionObserver, Node test runner, headless Playwright, existing Python/Paramiko deploy helper.

## Global Constraints

- Website only; no launcher build, release, updater changes, real game or server operations.
- Original icon and cover; no generated artwork, external animation dependencies or analytics.
- No internal review/de-identification/version-comparison notices in the homepage feature section.
- Version and platform metadata remain accurate in the download section; public downloads remain 1.2.13.
- Background browser only; responsive layouts and `prefers-reduced-motion` required.

### Task 1: Feature presentation and accessible motion

**Files:** `website/index.html`, `website/site.css`, `website/site.js`, `website/help.html`, `website/changes.html`, `tests/website.test.cjs`.

**Interfaces:** Preserve `data-feature`, `data-feature-image`, the five screenshot IDs and current download metadata attributes. Add `data-feature-points` to the selected detail list and `.feature-summary` to each tab. Accessible names must match the visible headings through `aria-labelledby`; summaries use `aria-describedby`. Update keyboard and screenshot tests to those names.

- [x] Add browser tests requiring all five feature summaries to be visible and nonempty, selected detail bullets to update, and a real panel animation after tab selection.
- [x] Run `node --test tests/website.test.cjs`; observe the new tests fail while baseline tests pass.
- [x] Replace the feature layout and public copy. Make `selectFeature(id)` populate title, description, three bullet items and the matching screenshot, using textContent and existing local assets.
- [x] Animate each user selection with `panel.animate([{opacity:.45,transform:'translateY(8px)'},{opacity:1,transform:'translateY(0)'}],{duration:260,easing:'ease-out'})`; cancel previous transition on rapid selections; bypass on reduced-motion preference.
- [x] Add one-shot IntersectionObserver entrances using visible-by-default content. Update shared navigation to the descriptive “功能介绍”. Keep release data in downloads only.
- [x] Run the entire website suite, inspect full-page desktop/mobile and selected feature screenshots, and correct observed overflow, whitespace or image proportions.

### Task 2: Deploy the existing site and verify

**Files:** `deploy/README.md`, `docs/releases/2026-10-23-website-features-motion.md`.

**Interfaces:** Existing `deploy/deploy_site.py` CLI, existing mirror v1.2.13 and fixed SSH host key; no new published assets or setup files.

- [x] Review the source diff and run `git diff --check`, website suite and deployment tests.
- [x] Commit only the reviewed website, tests and docs.
- [x] Run existing `deploy/deploy_site.py --site website --mirror artifacts/website-mirror/v1.2.13 --release-metadata artifacts/publication-1.2.13/github-published.json --handover <existing external document>` without printing credentials.
- [x] Run `OZL_SITE_URL=https://orangezl.com node --test tests/website.test.cjs`; confirm real HTTPS, no-store metadata, unchanged download version/hash and all feature interactions.
- [ ] Record actual site ID, results and screenshot paths; sync source to the authorized GitHub repository and report the deployed URL.
