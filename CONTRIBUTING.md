# Contributing to FileManagerNet

Thanks for taking the time to contribute. This project is a Razor Class Library with a
vanilla-JavaScript front end and an xUnit + Playwright test suite.

## Prerequisites

- .NET 10 SDK
- Google Chrome (the browser tests drive the installed Chrome, so `playwright install` is not needed)
- LibreOffice (optional; only for Office preview or the Office-related manual checks)

## Build

```bash
dotnet build FileManagerNet.slnx
```

## Run the demo

```bash
dotnet run --project demo/FileManagerNet.Demo
# http://localhost:5099
```

The demo serves `demo/FileManagerNet.Demo/App_Data/demo-files`, so it works on Windows, Linux
and macOS without changing paths.

## Tests

```bash
# full suite
dotnet test tests/FileManagerNet.E2ETests

# one area, e.g. Markdown rendering and path compatibility
dotnet test tests/FileManagerNet.E2ETests --filter "FullyQualifiedName~MarkdownRendererTests|FullyQualifiedName~PathCompatibilityTests"
```

`FileManagerNet.TestHost` creates throw-away roots (from `FMNET_TEST_ROOT` / `FMNET_TEST_ROOT2`),
so the tests never touch your own folders. The browser tests run sequentially because they share
the same file tree.

## Project layout

| Path | Contents |
|---|---|
| `src/FileManagerNet/` | The control: endpoints, services, models, tag helper, `wwwroot` assets |
| `demo/FileManagerNet.Demo/` | Runnable Razor Pages sample |
| `tests/FileManagerNet.TestHost/` | Minimal host started by the test fixture |
| `tests/FileManagerNet.E2ETests/` | Browser tests and path/ACL unit tests |
| `docs/` | Configuration manual and screenshots |

## Coding guidelines

- Keep nullable reference types enabled and add XML documentation to new public API.
- **Enforce permissions on the server.** Hiding a button in the UI is never enough; every
  operation must either go through `EnsureCanWrite` / `EnsureAccess` or an equivalent check.
- Treat values from the browser as untrusted: resolve virtual paths only through
  `FileSystemService` and never concatenate a client path into a physical path.
- The front end stays dependency-free: vanilla JS, no jQuery, no npm packages. Put new assets
  under `src/FileManagerNet/wwwroot/` so they ship with the package.
- JSON endpoints use the `{ ok, error, data }` envelope; file endpoints stream bytes.
  Keep error messages specific enough to name the missing permission or limit.
- Preserve the performance invariants described in the README: concurrent listings of one folder
  are collapsed, mutations invalidate the listing cache, and `stats` stays shallow by default.
- Add or update tests for behavior changes. Documentation-only changes do not need a version bump.

## Documentation

- `README.md` is **English**; `docs/configuration.zh-CN.md` is **Chinese**. Keep each file in one
  language and link to the other manual instead of duplicating content.
- When you change a default, a tag attribute, an endpoint or a command, update the matching table
  in both documents (or in the relevant one, if the feature is described there).
- Keep code samples copy-pasteable: use real property names and valid C#.

## Commits and pull requests

- Use conventional commit prefixes: `feat:`, `fix:`, `docs:`, `test:`, `chore:`.
- Do not bump the version in a feature or fix PR; the version is changed in a dedicated release
  commit by a maintainer.
- Describe what changed and why, and list the test commands you ran.
- Keep unrelated formatting changes out of functional PRs.

## Release process (maintainers)

1. Update the version in `Directory.Build.props`.
2. Update `CHANGELOG.md` and any changed documentation.
3. Commit as `chore: release X.Y.Z`.
4. Create and push the tag:

   ```bash
   git tag -a vX.Y.Z -m "vX.Y.Z"
   git push origin vX.Y.Z
   ```

   The `publish.yml` workflow packs the library and publishes it to NuGet.org through
   NuGet trusted publishing.

## Security

Please do not report vulnerabilities in public issues. See [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions are licensed under the MIT License
(see [LICENSE](LICENSE)).
