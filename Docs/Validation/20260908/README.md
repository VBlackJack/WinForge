# Backlog acceptance, 2026-09-08

[Français](README.fr.md) | [Release validation](../../RELEASE_VALIDATION.md)

This acceptance campaign follows published release `v2026090703`
(`84cbc5d1d78698a750132499afd3c1033d49940d`). GUI corrections and the rollback
verification correction are local changes, not a new published release.

Delivery note: the fixes from this campaign are included in `v2026090801`. Unpublished-candidate references below describe the executables at measurement time; their hashes and history remain unchanged.

## Evidence and scope

- [Catalog matrix](catalog-installations.json): all 195 release entries, with a
  separate status for every configured source. `NotTested` is not a success.
- [Catalog receipts](catalog-receipts.json): real disposable VMware installation
  results, independently observed detection/version, rollback outcomes and
  separately identified cleanup. Original receipt hashes are retained.
- [Independent cleanup check](catalog-cleanup.json): all 21 applications absent
  after the first lot; subsequent Firefox and GUI 7-Zip retests have their own
  cleanup receipts.
- [Spoken output](screen-reader.json): audio hashes, timed offline transcripts,
  effective display measurements, keyboard observations and real GUI installation.
- [Candidate identity](candidate.json): matching host/guest executable hash and
  local regression results. [R8 identity](candidate-r8.json) identifies the preceding
  candidate used before the final error-panel placement correction.
- The canonical action register is Datacron `_memory/projects/win11forge.md`,
  section `Backlog canonique après v2026090703 - réconciliation 2026-09-08`.
  This directory contains evidence and does not allocate a second set of actions.

The catalog lot contains 21 trials: 20 installations with independently detected
versions and one incomplete version detection (VLC). The matrix covers 195
applications and 388 configured sources. Alternate sources remain untested.

The trials use Windows 11 Pro 25H2, initially build 26200.8875. A cleanup reboot
applied a pending Windows update to 26200.9168; receipts identify the relevant
build. The Firefox follow-up identifies its locally corrected rollback module.
Package versions are observations from 2026-09-08, not pins.
Raw local transcripts, screenshots and audio are retained in
`Reports/backlog-20260908` and the dedicated disposable VM results share.

Light badge contrast: [measurements](badge-contrast.json), [10 VM cases](badge-visual.json), [visual review](badge-review.json), [final candidate](candidate-badges.json).

## Confirmed findings

| Area | Observed defect | Correction or disposition |
|---|---|---|
| Light badges | Source badges retained dark-theme colors in Folio and Parchment (contrast 1.00 to 1.21). | Apply the light palette, contrast 4.52 to 6.49; select secondary and warning badge text against its own background. |
| High DPI | Apps content became too short; catalog title and critical Apps actions could be clipped at 150%. | Scrollable content and wrapping headers/filter/action bars. |
| High contrast | Light-theme navigation, tabs and input/toggle resources did not all follow high contrast. | Complete semantic brush overrides and restoration when high contrast is disabled. |
| Editor placement | The application editor initially extended beyond the available work area at 150%. | Fit its initial placement to the owner's monitor. |
| Spoken names | NVDA read internal breadcrumb, theme/accent descriptor, settings tab and application model names. | Localized selection captions and explicit tab/action names. |
| Errors and progress | The invalid-history recovery panel was clipped by the scrolling content; progress lacked a descriptive automation name. | Place the complete recovery panel outside scrolling content; provide localized progress names and live-region events. |
| CLI rollback | Firefox and Ditto returned WinGet exit code zero while detection still found the application. | Require fresh absence detection before storing `RolledBack`; retain a retryable result when absence is not verified. |

WinSCP, VS Code and SumatraPDF refused elevated WinGet uninstallation for a user
package. Their separately recorded vendor cleanups do not count as successful
plan rollback. VLC installation succeeded but its configured detection returned
no version, so that trial is `DetectionIncomplete`.

## Validation boundaries

The [visual matrix](visual-matrix.json) passed all 40 combinations: the five
resolution/DPI pairs, Folio and Drakul, with high contrast and reduced motion
on/off. Each case covers seven restored and maximized pages, four critical
keyboard actions and persistence after a real process restart. The
[capture review](visual-review.json) supplements the measurements. The V10 150%
group was excluded after VMware resolution drift; all eight V11 repeats passed
with per-page and final display checks.

The matrix combines 32 V10 cases on the [previous candidate](candidate.json) and
eight V11 cases on the [final candidate](candidate-badges.json). The last delta
only changes badge brushes. The [10 focused repeats](badge-visual.json) cover
Folio and Parchment at all five display pairs on this final candidate.
[Contrast measurements](badge-contrast.json) improve from 1.00–1.21 to 4.52–6.49
for the four source badges; all nine tested badge pairs in each light theme meet
4.5:1. This does not certify every custom accent or every control state.

NVDA 2026.2 with eSpeak NG 1.52.0 produced actual speech for the source and detection
fields, preview, history, invalid-record error and real installation progression
and completion. R8 was measured at 125%, after Windows reset the effective scale;
it is not classified as a 150% result. R9 verified the corrected editor placement,
error speech and complete recovery controls at measured 150%.

The earlier R5 segment affected by an NVDA audio error is excluded. R8/R9 logs
contain no such audio error. The transcripts are automated and may contain
phonetic errors; they are paired with independent UIA observations and the actual
audio hashes. This validates the recorded NVDA workflows, not every control,
language, assistive technology or unrecorded dialog. UIA alone does not prove speech.

![Editor fully inside the work area at 150%](editor-150.png)

![Complete error recovery controls at 150%](error-150.png)

![High-contrast settings at 125%](settings-hc-125.png)

![Focused install action in Folio at 125%](apps-light-125.png)

Local regression gates after the corrections: 790 .NET tests passed; 2,006 Pester
tests passed, zero failed, six skipped. These are local results, distinct from the
published release's CI results.

The progressive catalog lot does not certify all 195 applications or alternate
sources. LDPlayer remains unavailable in the checked WinGet source. Rollback
still covers only newly installed supported packages; it does not restore older
versions or Windows configuration. GUI receipts and CLI plans remain distinct.

[Regression test receipts](regression-tests.json) retain report counters and
hashes, including the two expected failures before the contrast correction.

[VM restoration](vm-restoration.json) confirms the original settings file bytes,
saved DPI preferences and removal of acceptance tasks after a new logon. All 21
trial applications are absent in the final independent check. The Windows update,
acceptance audio device and portable tools/evidence remain in this disposable VM.
PowerShell analysis: 160 files, zero errors, nine warnings in unchanged files;
no findings in either modified PowerShell file.


## Virtualization applications and VM acceptance

The 2026-09-08 VMware campaign does not validate operation of a hypervisor or emulator requiring hardware virtualization inside the guest. Installing and detecting a package does not prove that its engine can start. This environment boundary alone is not a WinForge defect.

Runtime acceptance requires suitable physical hardware or a nested-virtualization configuration explicitly supported by the vendor. No nested-virtualization change or new trial of these applications was performed for this release. See [Microsoft's requirements for nested Hyper-V](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/enable-nested-virtualization). Historical installation statuses remain unchanged; this exclusion does not imply success.
