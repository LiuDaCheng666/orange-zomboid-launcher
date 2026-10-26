# 1.2.27 publication verification

Release source commit/tag: `ea323ca` / `v1.2.27`.

Final build: `E:\Projects\OrangeLauncher\dist\1.2.27-loading-60fps-20261026-final`.

The preceding `1.2.27-loading-20261026-final` build was superseded locally before any publication, to include the requested 60 FPS idle-draw target. Only the final 60 FPS build was uploaded.

- Final main SHA256: `B3A2A1AF324849CDAC3E15ECF22C6DA9F9E08670168AEA1FA78177FFC39FDE12`.
- Final startup JAR SHA256: `95FAD5172750C94215BF206CF73575C8B9F4FC9013C350CAE55C5F0EDCDA6DD0`.
- Ten immutable release files verified locally; archive/RSA manifest/bootstrap and seven program-icon checks passed. Defender custom scan found no threats. This does not assert Authenticode signing.
- Primary software-only publication returned `published`; all 759 protected filesystem entries matched before/after. Site `current`, website content and SSH/Web protection configuration were unchanged.
- GitHub release `424140780`, `v1.2.27`: all ten uploaded asset names, sizes and SHA256 digests matched before draft activation. Activated only after old-client verification against the primary source.
- The real 1.2.26 update backend, using its production transport in an isolated locator, checked 1.2.27, downloaded and verified the package to `Ready` while GitHub was still a private draft. It did not apply an update, start the launcher entry point or launch a game.
- The real 1.2.27 backend authenticated all 21 local bootstrap entries and reported `Latest`.
- All twenty anonymous full downloads (ten files from each origin) matched local sizes and SHA256. The three primary latest feed files and public `release.json` also matched 1.2.27.
- Post-publication read-only SSH/SFTP check confirmed Ed25519 key authentication, pinned host verification and the same active site.

Evidence: `artifacts/publication-1.2.27/{local-build-proof,release-hashes,primary-receipt,github-draft,github-published,old-client-ready,new-client-latest,public-download-proof,ssh-after}.json`, plus `defender-60fps.txt`. Focused code/render verification and the unreproduced full-launch latency limitation are in `2026-10-26-v127-startup-loading.md`.

Public release: https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/tag/v1.2.27
