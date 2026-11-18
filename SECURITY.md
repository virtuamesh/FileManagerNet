# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 1.0.x | Yes |
| Earlier | No |

## Reporting a vulnerability

Please report security issues **privately** through GitHub Security Advisories:

<https://github.com/virtuamesh/FileManagerNet/security/advisories/new>

Do not open a public issue for a suspected vulnerability. Include:

- the affected version,
- the configuration needed to reproduce it (tag attributes, ACL rules, `FileManagerConfig`),
- a minimal reproduction (request sequence or file names),
- the impact you expect.

You will get an acknowledgement as soon as possible; fixes are released as a new patch version.

## Security model

FileManagerNet is a library, not a complete application. It assumes the host application:

- authenticates and authorizes users before they reach the mapped endpoints
  (`RequireAuthorization()` on `MapFileManagerNet` is the intended hook),
- decides which physical folders are exposed,
- runs with an account whose file-system permissions are appropriate for those folders.

Within that model the library enforces:

- **Virtual paths only.** The browser sends an instance id and a root-relative path. A physical
  path is never emitted to the client, and an unknown/forged instance id is rejected.
- **Path validation.** `..` as a whole segment, NUL, `//`, and (on Windows) `:` and `\` are
  rejected. Names that merely contain dots or colons are valid on Linux/macOS and stay inside the
  configured root.
- **Server-side permissions.** ACL rules and `FileManagerConfig` switches are checked for every
  operation; a hand-written HTTP request gets the same `403` as the UI.
- **Archive safety.** Extraction rejects entries that would escape the destination (zip-slip).
- **Upload limits.** `MaxUploadBytes` is enforced at session creation, not only in the browser.
- **Preview safety.** Markdown is rendered with HTML escaped; links and images with
  `javascript:`, `data:` or `vbscript:` schemes are dropped. Images, PDFs and media are streamed
  from the configured folders only.
- **Office conversion.** LibreOffice runs as a separate headless process with a per-conversion
  profile, a timeout and a concurrency limit.

## Deployment checklist

- Put the endpoints behind authentication and HTTPS.
- Run the application under a least-privilege account.
- Keep `OfficeCacheFolder` outside the served root.
- Set `MaxUploadBytes` and matching reverse-proxy body limits (nginx `client_max_body_size`,
  IIS request filtering).
- Disable Office preview (`OfficePreviewEnabled = false`) if you do not trust the documents
  users can upload.
- Review ACL rules before exposing a folder; an omitted rule allows everything the instance
  switches permit.
- Keep the package and its dependencies up to date.

## Out of scope

- Missing authentication in a host application that did not add any.
- Reading files that the application's OS account can read, when the configured ACL allows the
  operation.
- Resource exhaustion caused by deliberately disabled limits (`MaxUploadBytes = 0`, no quota).
- Issues in LibreOffice, SharpCompress or the host's reverse proxy; report those upstream.
