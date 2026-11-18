# Third-Party Notices

FileManagerNet itself is licensed under the MIT License (see [LICENSE](LICENSE)).

The following third-party components are used or redistributed. Their licenses apply to those
components, not to the project as a whole.

| Component | Use | License | Link |
|---|---|---|---|
| Material Icon Theme | File-type SVG icons in `src/FileManagerNet/wwwroot/icons/` | MIT | <https://github.com/PKief/vscode-material-icon-theme> |
| SharpCompress | Archive creation and extraction | MIT | <https://github.com/adamhathcock/sharpcompress> |
| System.Drawing.Common | Windows system icon extraction | MIT | <https://github.com/dotnet/runtime> |
| .NET / ASP.NET Core | Runtime and framework reference | MIT | <https://github.com/dotnet/aspnetcore> |

The original license text for the icon set is kept next to the assets in
`src/FileManagerNet/wwwroot/icons/LICENSE-material-icon-theme.txt`.

No third-party JavaScript libraries are bundled; the browser code under
`src/FileManagerNet/wwwroot/js/` is part of this project.

Optional runtime dependency: **LibreOffice** is invoked as a separate process when Office preview
is enabled. It is not redistributed with this package and is covered by the Mozilla Public
License 2.0 and its own third-party notices.
