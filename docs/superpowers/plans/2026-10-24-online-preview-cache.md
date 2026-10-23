# Online Preview Cache Implementation Plan

> **For agentic workers:** Use the existing bounded website agent for this one repair, then independent review. User waived repeated confirmation and explicitly requests online deployment.

**Goal:** Existing visitors receive the approved new screenshots immediately after website deployment.

**Architecture:** One static cache version shared by initial HTML image and dynamically selected PNG requests. Current Caddy already revalidates non-assets; no hosting migration/config changes.

**Tech Stack:** Existing static HTML/JS and Playwright Node tests.

## Global Constraints

- E:/Projects/OrangeLauncher, currentbranchfix/online-showcase-cache, nativecandidatefrozen34e2176 unaffected. No launcher/native/dependency/version/binary/release/feed/security changes. ExistingtenPNGsanddesignunchanged; backgroundonly. No credentialprinting.
- Allowedsourcefiles website/index.html,website/site.js,tests/website.test.cjs. Preserve allstates/themes,keyboard/noJS/lazyinitialsinglefetch, retry/race,motion,zoom anddownloadvalidation. apply_patch.

## Task 1: Version screenshot URLs, verify and deploy

**Files:** Threeallowedfiles above; quality/deploymentreport in artifacts/2026-10-24-online-site-report.md.

- [ ] Add actualbrowser-request test ensuring initial screenshot and alltenstate/themePNGrequests carry same `v=20261024-fluent`, no duplicateinitialfetch and enlargeusesexactselectedURL. Initialfailuremustbeunversionedrequest, notmissingselector.

```js
const url=new URL(await page.locator('[data-feature-image]').getAttribute('src'),base);
assert.equal(url.searchParams.get('v'),'20261024-fluent');
```

- [ ] Run focusedRED, then addquerytoinitialHTMLand dynamicURL; ifexistingtestnetworkrouteglobsassumePNGwithoutquery, adaptmatchertoallowquerywithoutweakeningpath/failureassertions.

```js
nextImage.src='assets/launcher-'+id+'-'+theme.value+'.png?v=20261024-fluent';
```

- [ ] FocusedGREEN thenfullwebsiteonce. ReadactualURLrequestsandimagebytes; independentdiffreviewbeforeactivation. AgentdoesnotmutateGit; mainreviews/integratessource.
- [ ] Deployapprovedstaticwebsitewithsameauthenticatedlivev1.2.13metadataandimmutablebinaries. Remotehashesmustmatchnewsourceexceptrelease.jsonmustremainexistinglivebytes. VerifyHTTPS200/redirects,liveNodewebsitechecks,headlessfourwidthsnapshotsandmotionrecording. No productdistributionchanges.
