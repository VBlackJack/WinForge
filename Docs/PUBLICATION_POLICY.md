# Publication Policy

[Français](PUBLICATION_POLICY.fr.md)

## Languages

English is the default language for README files, documentation, changelogs, and release notes. Keep French translations in separate files beside their English sources, using the `.fr.md` suffix, for example `README.md` and `README.fr.md`.

Add reciprocal language links and update both versions together. Keep commands, identifiers, paths, version numbers, and checksums identical across translations. Label links to pages available only in English in the French documentation.

## Release Notes

For each future release, prepare `Docs/releases/<tag>.md` and `Docs/releases/<tag>.fr.md`. Use the English file as the GitHub release body. Attach the French file as a separate release asset and link to that asset from the English body. Do not combine both languages into one release body.

Check both files against the same release tag, changes, artifacts, and checksums before publication. Existing historical release notes are not retroactively translated by this convention.

## Editorial Rules

- Use plain punctuation without em dashes.
- Describe the product and verified behavior without tool attribution, generation credits, or process transcripts.
- Use English commit messages and repository descriptions.
- Keep GitHub topics specific to implemented features and technologies; review them when the project scope changes.

## Review

Before publication, verify language links, relative paths, translation consistency, version references, typography, and release asset links. Documentation changes do not by themselves authorize a release or a rewrite of Git history.
