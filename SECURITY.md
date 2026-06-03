# Security policy

## Supported versions

Security fixes are applied to the latest release on the `main` branch of [SlushFlow/PES3-Disc](https://github.com/SlushFlow/PES3-Disc).

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security-sensitive reports.

Report security issues privately via GitHub Security Advisories on the repository, or contact the maintainer through GitHub.

Include:

- Description of the issue
- Steps to reproduce
- Impact assessment
- Suggested fix (if any)

## Bug report API

The optional bug-report feature sends user-entered text and basic system metadata to a configured HTTPS endpoint. Do not include secrets, save files, or personal data in bug reports. See [PRIVACY.md](PRIVACY.md).

## Operational notes for self-hosting the API

- Set a strong `DEV_API_KEY` on Render (never use the default `dev-change-me` in production).
- Use HTTPS only.
- The free Render tier does not provide durable disk storage; treat the database as non-persistent unless you use a paid disk.
