# Windows signing and installer repair

User authorized fixing the save workflows, publishing the resulting update and repairing recurring Defender/install failures. Authenticode is separate from the existing RSA update signatures; neither signing nor one sample review guarantees that future versions can never be detected.

1. Preserve the verified save-workflow fixes and unrelated website edits. Keep the chosen-directory installer, Chinese shortcuts and portable build.
2. Extract an installer-only source closure from existing implementations. Remove game/Steam/legacy deployment code from this installer's compilation, without rewriting path ownership, bootstrap authentication or installation behavior. Include the supplied version in PE metadata. Test a unique application identity and independent directories.
3. Add exact-certificate Windows signing with SHA-256 and RFC3161 timestamps. Sign the main executable before freezing its RSA bootstrap. During Velopack packaging, preserve the six already frozen application PE files while signing generated updater, execution stub and official Setup. Sign the final chooser after embedding that Setup.
4. Verify expected publisher identity, trusted signatures and timestamps in final archive positions, plus the existing complete RSA/hash inventories, before writing publication metadata. Do not re-sign third-party components. Keep certificate keys outside the repository and retain the existing external RSA key.
5. Test relevant signing/version/packaging failures and actual isolated installer behavior. Record exactly which checks used fixtures and which used a real trusted certificate. Do not call unsigned or self-signed development outputs a signed release.
6. Obtain an actually available trusted signing identity. The October 24 audit records that the earlier SignPath Foundation application was not approved. A paid certificate requires the user's purchase/identity choice; no purchase or identity submission is authorized implicitly by this plan.
7. Freeze one final, signed candidate and test its actual installation/update/start under enabled protection. Publish only release files and metadata to the existing server and GitHub, then patch live changes.html with publish_changelog.py. Never deploy the whole website or change its configuration.

Current blockers: no signing certificate in the Windows stores; no approved signing account recorded; Microsoft submission df47e309-95c1-45d4-8cf3-d5859eb45c3b is still in progress. The exact Defender classification cause remains undetermined.
