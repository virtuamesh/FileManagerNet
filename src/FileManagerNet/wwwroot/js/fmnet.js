/* FileManagerNet front-end — vanilla JS, no dependencies.
 * Windows-Explorer-style UI: ribbon toolbar, folder tree, breadcrumb,
 * 6 view layouts, context menu, drag & drop upload, toasts. */
(function () {
  "use strict";
  /* The tag helper emits this script once per instance, so a page with several
   * file managers loads it more than once. Re-running it would build a fresh
   * prototype and strand the instances created by the first copy. */
  if (window.FileManagerNet && window.FileManagerNet.proto) return;

  /* ------------------------------------------------------------------ icons
   * Colored, Windows-Explorer-style icon set (desktop explorer look & feel).
   * All icons are inline SVGs with explicit fills — no external assets. */
  function sheet(extra) {
    return '<svg viewBox="0 0 24 24">' +
      '<path fill="#FFFFFF" stroke="#AFB8C2" stroke-width="1.2" d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z"/>' +
      '<path fill="#E3E8ED" stroke="#AFB8C2" stroke-width="1.2" d="M14 2v6h6z"/>' + (extra || "") + "</svg>";
  }
  var folderBack = '<path fill="#E6A735" d="M3 6a2 2 0 0 1 2-2h4.2l2 2H19a2 2 0 0 1 2 2v2H3z"/>';
  var folderFront = '<path fill="#FFC94A" d="M3 9h18v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>';
  var folderEdge = '<path fill="#F0B243" d="M3 9h18v1.2H3z"/>';
  var ICONS = {
    folder: '<svg viewBox="0 0 24 24">' + folderBack + folderFront + folderEdge + "</svg>",
    open: '<svg viewBox="0 0 24 24">' + folderBack +
      '<path fill="#FFD977" d="M5 9h16.5l-2 9.2a2 2 0 0 1-2 1.8H4.2a1.6 1.6 0 0 1-1.6-2z"/></svg>',
    file: sheet(),
    text: sheet('<path stroke="#98A2AC" stroke-width="1.5" stroke-linecap="round" d="M8 13h8M8 16.5h5.5"/>'),
    code: sheet('<path fill="none" stroke="#0F9D77" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" d="m11 11.6-2.4 2.4 2.4 2.4M13.6 11.6l2.4 2.4-2.4 2.4"/>'),
    image: sheet('<rect x="5.4" y="10" width="9.6" height="7" rx="1" fill="#A5D6A7"/>' +
      '<circle cx="8.3" cy="12.3" r="1.15" fill="#FFCA28"/>' +
      '<path fill="#2E7D32" d="m6.4 17 2.1-2.5 1.7 1.9 1.8-2.1 2.3 2.7z"/>'),
    video: sheet('<circle cx="12" cy="14.8" r="3.6" fill="#1E88E5"/><path fill="#FFFFFF" d="m10.7 13 3.1 1.8-3.1 1.8z"/>'),
    audio: sheet('<path fill="#AB47BC" d="M9.4 18.4v-7.2l6.2-1.3v6.3"/>' +
      '<circle cx="8" cy="18.4" r="1.7" fill="#AB47BC"/><circle cx="14.2" cy="16.2" r="1.7" fill="#AB47BC"/>'),
    pdf: sheet('<rect x="4.8" y="13" width="9.6" height="6" rx="1.2" fill="#E53935"/>' +
      '<text x="9.6" y="17.6" font-size="4.6" font-weight="bold" fill="#FFFFFF" text-anchor="middle" font-family="Arial,sans-serif">PDF</text>'),
    archive: sheet('<path stroke="#8D6E63" stroke-width="1.6" stroke-linecap="round" d="M12 9.5v2.6"/>' +
      '<path fill="#8D6E63" d="M10.9 12.6h2.2l-.6 2.4h-1z"/>' +
      '<path stroke="#8D6E63" stroke-width="1.4" stroke-linecap="round" d="M8 18.4h4"/>'),
    /* -------- toolbar -------- */
    search: '<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="6.5" fill="#E3F2FD" stroke="#1E88E5" stroke-width="1.8"/>' +
      '<path stroke="#1E88E5" stroke-width="2.2" stroke-linecap="round" d="m19.6 19.6-4.2-4.2"/></svg>',
    refresh: '<svg viewBox="0 0 24 24"><path fill="none" stroke="#1E88E5" stroke-width="2.1" stroke-linecap="round" d="M4.5 12a7.5 7.5 0 0 1 13-5.1"/>' +
      '<path fill="#1E88E5" d="M18.6 2.9v5.2h-5.2z"/>' +
      '<path fill="none" stroke="#1E88E5" stroke-width="2.1" stroke-linecap="round" d="M19.5 12a7.5 7.5 0 0 1-13 5.1"/>' +
      '<path fill="#1E88E5" d="M5.4 21.1v-5.2h5.2z"/></svg>',
    upload: '<svg viewBox="0 0 24 24"><path stroke="#43A047" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" fill="none" d="M12 15.5V4.5m0 0-4.2 4.2M12 4.5l4.2 4.2"/>' +
      '<rect x="4" y="17.6" width="16" height="3.2" rx="1" fill="#78909C"/></svg>',
    uploadFolder: '<svg viewBox="0 0 24 24"><g transform="translate(1,3) scale(.78)">' + folderBack + folderFront + folderEdge + '</g>' +
      '<path stroke="#43A047" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" d="M18 13.5V4.2m0 0-2.6 2.6M18 4.2l2.6 2.6"/></svg>',
    download: '<svg viewBox="0 0 24 24"><path stroke="#1E88E5" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" fill="none" d="M12 4.5v11m0 0-4.2-4.2M12 15.5l4.2-4.2"/>' +
      '<rect x="4" y="17.6" width="16" height="3.2" rx="1" fill="#78909C"/></svg>',
    cut: '<svg viewBox="0 0 24 24"><circle cx="6" cy="18" r="2.4" fill="none" stroke="#546E7A" stroke-width="1.8"/>' +
      '<circle cx="18" cy="18" r="2.4" fill="none" stroke="#546E7A" stroke-width="1.8"/>' +
      '<path stroke="#78909C" stroke-width="1.8" stroke-linecap="round" d="m7.7 16.2 9-11.6M16.3 16.2l-9-11.6"/>' +
      '<circle cx="12" cy="9.6" r="1.1" fill="#E53935"/></svg>',
    copy: '<svg viewBox="0 0 24 24"><rect x="8.5" y="8.5" width="11" height="12" rx="1.6" fill="#FFFFFF" stroke="#607D8B" stroke-width="1.4"/>' +
      '<path fill="none" stroke="#90A4AE" stroke-width="1.6" d="M6 15.5H5.6A1.6 1.6 0 0 1 4 13.9V5.1A1.6 1.6 0 0 1 5.6 3.5h8.8A1.6 1.6 0 0 1 16 5.1V6"/></svg>',
    paste: '<svg viewBox="0 0 24 24"><rect x="5" y="4" width="14" height="17" rx="2" fill="#A1887F"/>' +
      '<rect x="9" y="2.6" width="6" height="3.2" rx="1" fill="#8D6E63"/>' +
      '<rect x="7" y="7.4" width="10" height="11" rx="1" fill="#FFFFFF" stroke="#C9BFB5"/>' +
      '<path stroke="#B0ABA4" stroke-width="1.3" stroke-linecap="round" d="M9 10.5h6M9 13.5h6M9 16h4"/></svg>',
    rename: '<svg viewBox="0 0 24 24"><path fill="#FFB300" stroke="#B07B00" stroke-width="1" d="m5.2 18.8.8-3.3 9.3-9.3 2.5 2.5-9.3 9.3z"/>' +
      '<path fill="#4FC3F7" stroke="#0288D1" stroke-width="1" d="m15.3 6.2 2.5 2.5 1.1-1.1a1.8 1.8 0 0 0-2.5-2.5z"/>' +
      '<path stroke="#90A4AE" stroke-width="1.6" stroke-linecap="round" d="M4 21h16"/></svg>',
    del: '<svg viewBox="0 0 24 24"><path fill="#E53935" d="M9.2 3h5.6l.8 2H19a.9.9 0 0 1 0 1.8H5A.9.9 0 0 1 5 5h3.4z"/>' +
      '<path fill="#EF5350" d="M6.2 8.4h11.6l-.9 11a2 2 0 0 1-2 1.8H9.1a2 2 0 0 1-2-1.8z"/>' +
      '<path stroke="#B71C1C" stroke-width="1.4" stroke-linecap="round" d="M10 11.2v6.6M14 11.2v6.6"/></svg>',
    zip: '<svg viewBox="0 0 24 24"><rect x="4.5" y="8" width="15" height="12.5" rx="1.6" fill="#FFE0B2" stroke="#8D6E63" stroke-width="1.3"/>' +
      '<path stroke="#8D6E63" stroke-width="1.4" stroke-linecap="round" d="M4.5 11.5h15"/>' +
      '<path stroke="#5D4037" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" d="M12 2.5v6m0 0-2.4-2.4M12 8.5l2.4-2.4"/></svg>',
    extract: '<svg viewBox="0 0 24 24"><rect x="4.5" y="12" width="15" height="8.5" rx="1.6" fill="#FFE0B2" stroke="#8D6E63" stroke-width="1.3"/>' +
      '<path stroke="#5D4037" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none" d="M12 12V3.5m0 0L9.6 5.9M12 3.5l2.4 2.4"/></svg>',
    newFolder: '<svg viewBox="0 0 24 24"><g transform="translate(0,-1) scale(.92)">' + folderBack + folderFront + folderEdge + "</g>" +
      '<circle cx="17.5" cy="17.5" r="4.6" fill="#FFFFFF" stroke="#2E7D32" stroke-width="1.4"/>' +
      '<path stroke="#2E7D32" stroke-width="1.8" stroke-linecap="round" d="M17.5 15.3v4.4M15.3 17.5h4.4"/></svg>',
    newFile: '<svg viewBox="0 0 24 24"><g transform="translate(-1,0) scale(.92)">' +
      '<path fill="#FFFFFF" stroke="#AFB8C2" stroke-width="1.3" d="M6 2h9l5 5v13a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z"/>' +
      '<path fill="#E3E8ED" stroke="#AFB8C2" stroke-width="1.3" d="M14 2v6h6z"/></g>' +
      '<circle cx="17.5" cy="17.5" r="4.6" fill="#FFFFFF" stroke="#2E7D32" stroke-width="1.4"/>' +
      '<path stroke="#2E7D32" stroke-width="1.8" stroke-linecap="round" d="M17.5 15.3v4.4M15.3 17.5h4.4"/></svg>',
    view: '<svg viewBox="0 0 24 24"><rect x="3.5" y="3.5" width="7.4" height="7.4" rx="1.2" fill="#546E7A"/>' +
      '<rect x="13.1" y="3.5" width="7.4" height="7.4" rx="1.2" fill="#90A4AE"/>' +
      '<rect x="3.5" y="13.1" width="7.4" height="7.4" rx="1.2" fill="#90A4AE"/>' +
      '<rect x="13.1" y="13.1" width="7.4" height="7.4" rx="1.2" fill="#1E88E5"/></svg>',
    preview: '<svg viewBox="0 0 24 24"><path fill="#FFFFFF" stroke="#1E88E5" stroke-width="1.6" d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z"/>' +
      '<circle cx="12" cy="12" r="3.2" fill="#1E88E5"/></svg>',
    selectAll: '<svg viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="2.5" fill="#E8F5E9" stroke="#2E7D32" stroke-width="1.6"/>' +
      '<path fill="none" stroke="#2E7D32" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" d="m8.5 12.3 2.5 2.5 4.6-5"/></svg>',
    selectNone: '<svg viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="2.5" fill="#FFEBEE" stroke="#C62828" stroke-width="1.6"/>' +
      '<path stroke="#C62828" stroke-width="2" stroke-linecap="round" d="m9 15 6-6"/></svg>',
    invert: '<svg viewBox="0 0 24 24"><rect x="4" y="4" width="9.5" height="9.5" rx="1.5" fill="#1E88E5"/>' +
      '<rect x="10.5" y="10.5" width="9.5" height="9.5" rx="1.5" fill="#FFFFFF" stroke="#546E7A" stroke-width="1.6"/></svg>',
    sort: '<svg viewBox="0 0 24 24"><path stroke="#546E7A" stroke-width="1.8" stroke-linecap="round" d="M4 6h10M4 12h7M4 18h4"/>' +
      '<path fill="#1E88E5" d="m17 3.5 4.2 5.5h-8.4z"/><path fill="#90A4AE" d="m17 20.5-4.2-5.5h8.4z"/></svg>',
    /* -------- View tab: layouts -------- */
    viewXL: '<svg viewBox="0 0 24 24"><rect x="5" y="5" width="14" height="14" rx="1.5" fill="#1E88E5"/></svg>',
    viewL: '<svg viewBox="0 0 24 24"><rect x="4" y="4" width="7" height="7" rx="1" fill="#1E88E5"/>' +
      '<rect x="13" y="4" width="7" height="7" rx="1" fill="#90A4AE"/>' +
      '<rect x="4" y="13" width="7" height="7" rx="1" fill="#90A4AE"/>' +
      '<rect x="13" y="13" width="7" height="7" rx="1" fill="#90A4AE"/></svg>',
    viewM: '<svg viewBox="0 0 24 24"><g fill="#1E88E5">' +
      '<rect x="3.5" y="3.5" width="4.6" height="4.6" rx=".8"/><rect x="9.7" y="3.5" width="4.6" height="4.6" rx=".8"/><rect x="15.9" y="3.5" width="4.6" height="4.6" rx=".8"/>' +
      '<rect x="3.5" y="9.7" width="4.6" height="4.6" rx=".8"/><rect x="9.7" y="9.7" width="4.6" height="4.6" rx=".8"/><rect x="15.9" y="9.7" width="4.6" height="4.6" rx=".8"/>' +
      '<rect x="3.5" y="15.9" width="4.6" height="4.6" rx=".8"/><rect x="9.7" y="15.9" width="4.6" height="4.6" rx=".8"/><rect x="15.9" y="15.9" width="4.6" height="4.6" rx=".8"/></g></svg>',
    viewS: '<svg viewBox="0 0 24 24"><g fill="#546E7A">' +
      '<rect x="3.5" y="3.5" width="3.4" height="3.4" rx=".6"/><rect x="8.7" y="3.5" width="3.4" height="3.4" rx=".6"/><rect x="13.9" y="3.5" width="3.4" height="3.4" rx=".6"/><rect x="19.1" y="3.5" width="3.4" height="3.4" rx=".6"/>' +
      '<rect x="3.5" y="8.7" width="3.4" height="3.4" rx=".6"/><rect x="8.7" y="8.7" width="3.4" height="3.4" rx=".6"/><rect x="13.9" y="8.7" width="3.4" height="3.4" rx=".6"/><rect x="19.1" y="8.7" width="3.4" height="3.4" rx=".6"/>' +
      '<rect x="3.5" y="13.9" width="3.4" height="3.4" rx=".6"/><rect x="8.7" y="13.9" width="3.4" height="3.4" rx=".6"/><rect x="13.9" y="13.9" width="3.4" height="3.4" rx=".6"/><rect x="19.1" y="13.9" width="3.4" height="3.4" rx=".6"/>' +
      '<rect x="3.5" y="19.1" width="3.4" height="3.4" rx=".6"/><rect x="8.7" y="19.1" width="3.4" height="3.4" rx=".6"/><rect x="13.9" y="19.1" width="3.4" height="3.4" rx=".6"/><rect x="19.1" y="19.1" width="3.4" height="3.4" rx=".6"/></g></svg>',
    viewList: '<svg viewBox="0 0 24 24"><g fill="#546E7A">' +
      '<rect x="3.5" y="4.5" width="3" height="3" rx=".6"/><rect x="8.5" y="5.2" width="12" height="1.6" rx=".8"/>' +
      '<rect x="3.5" y="10.5" width="3" height="3" rx=".6"/><rect x="8.5" y="11.2" width="12" height="1.6" rx=".8"/>' +
      '<rect x="3.5" y="16.5" width="3" height="3" rx=".6"/><rect x="8.5" y="17.2" width="12" height="1.6" rx=".8"/></g></svg>',
    viewDetails: '<svg viewBox="0 0 24 24"><rect x="3.5" y="5" width="17" height="2.4" rx="1" fill="#1E88E5"/>' +
      '<g fill="#90A4AE"><rect x="3.5" y="9.6" width="8" height="1.6" rx=".8"/><rect x="13.5" y="9.6" width="7" height="1.6" rx=".8"/>' +
      '<rect x="3.5" y="13.2" width="8" height="1.6" rx=".8"/><rect x="13.5" y="13.2" width="7" height="1.6" rx=".8"/>' +
      '<rect x="3.5" y="16.8" width="8" height="1.6" rx=".8"/><rect x="13.5" y="16.8" width="7" height="1.6" rx=".8"/></g></svg>',
    viewTiles: '<svg viewBox="0 0 24 24"><g fill="#1E88E5"><rect x="3.5" y="4.5" width="7.5" height="6" rx="1"/><rect x="13" y="4.5" width="7.5" height="6" rx="1" fill="#90A4AE"/></g>' +
      '<g fill="#90A4AE"><rect x="3.5" y="13" width="7.5" height="6" rx="1"/><rect x="13" y="13" width="7.5" height="6" rx="1"/></g></svg>',
    /* -------- View tab: panes & show/hide -------- */
    /* path-bar navigation */
    up: '<svg viewBox="0 0 24 24">' +
      '<path fill="none" stroke="#607D8B" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" d="M12 19.5V6M6.4 11.6 12 6l5.6 5.6"/></svg>',
    home: '<svg viewBox="0 0 24 24">' +
      '<path fill="#FFE0A3" stroke="#E39A2B" stroke-width="1.6" stroke-linejoin="round" d="M3.8 10.6 12 3.9l8.2 6.7V19a1.4 1.4 0 0 1-1.4 1.4H5.2A1.4 1.4 0 0 1 3.8 19z"/>' +
      '<path fill="#FFF6E5" stroke="#E39A2B" stroke-width="1.5" stroke-linejoin="round" d="M9.6 20.4v-5.2h4.8v5.2z"/></svg>',
    navPane: '<svg viewBox="0 0 24 24"><rect x="3" y="4.5" width="18" height="15" rx="1.8" fill="#FFFFFF" stroke="#607D8B" stroke-width="1.5"/>' +
      '<rect x="3" y="4.5" width="6.5" height="15" rx="1.6" fill="#BBDEFB"/><path stroke="#607D8B" stroke-width="1.3" d="M9.5 4.5v15"/></svg>',
    detailsIcon: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="8.5" fill="#E3F2FD" stroke="#1E88E5" stroke-width="1.6"/>' +
      '<path stroke="#1E88E5" stroke-width="1.9" stroke-linecap="round" d="M12 11v5.6"/><circle cx="12" cy="8" r="1.15" fill="#1E88E5"/></svg>',
    ext: '<svg viewBox="0 0 24 24">' + sheet() +
      '<text x="9" y="18" font-size="6.4" font-weight="bold" fill="#1E88E5" text-anchor="middle" font-family="Arial,sans-serif">E</text></svg>',
    hiddenIcon: '<svg viewBox="0 0 24 24"><path fill="#FFFFFF" stroke="#78909C" stroke-width="1.6" d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z"/>' +
      '<circle cx="12" cy="12" r="3.1" fill="#B0BEC5"/><path stroke="#E53935" stroke-width="2" stroke-linecap="round" d="m4.5 19.5 15-15"/></svg>',
    caret: '<svg viewBox="0 0 24 24"><path fill="#78909C" d="m6.5 9.5 5.5 5.5 5.5-5.5z"/></svg>',
    /* ribbon collapse / expand / pin */
    upShort: '<svg viewBox="0 0 24 24"><path fill="none" stroke="#546E7A" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="m6 15 6-6 6 6"/></svg>',
    downShort: '<svg viewBox="0 0 24 24"><path fill="none" stroke="#546E7A" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="m6 9 6 6 6-6"/></svg>',
    /* pushpin (clear at 16px): round head, tapered body, needle */
    pin: '<svg viewBox="0 0 24 24">' +
      '<circle cx="12" cy="7.2" r="4.3" fill="#7D8A96"/>' +
      '<path fill="#95A2AE" d="M9.6 10.3h4.8l-1.2 5.3h-2.4z"/>' +
      '<path stroke="#6E7B87" stroke-width="1.9" stroke-linecap="round" d="M12 15.6v4.6"/></svg>'
  };
  function icon(name) { return ICONS[name] || ICONS.file; }

  /* ------------------------------------------------- ribbon icons (32 px)
   * The 16 px set above stays legible in the tree, breadcrumb and list rows.
   * Ribbon buttons get their own, larger artwork in the Windows-ribbon idiom
   * (flat shapes, strong silhouettes, saturated blue/red/yellow) so the toolbar
   * reads like Explorer's — drawn for this project, no third-party bitmaps. */
  var RIBBON_ICONS = {
    copy: '<svg viewBox="0 0 32 32">' +
      '<path fill="#eef2f6" stroke="#9aa9b6" stroke-width="1.2" d="M12.5 4.5h11a2 2 0 0 1 2 2v15h-13z"/>' +
      '<path fill="#dfe6ed" d="M12.5 4.5h6.2v17h-6.2z" opacity=".7"/>' +
      '<path fill="#ffffff" stroke="#7f8f9d" stroke-width="1.3" d="M6.5 9.5h12a2 2 0 0 1 2 2v16h-14z"/>' +
      '<path stroke="#aab8c4" stroke-width="1.3" stroke-linecap="round" d="M9.5 14.5h8M9.5 18h8M9.5 21.5h5.5"/>' +
      '</svg>',
    paste: '<svg viewBox="0 0 32 32">' +
      '<rect x="7" y="5" width="18" height="24" rx="2" fill="#c08a5e" stroke="#96683f" stroke-width="1.2"/>' +
      '<rect x="9.5" y="3" width="13" height="5" rx="1.5" fill="#b9c6d2" stroke="#8794a0" stroke-width="1.2"/>' +
      '<rect x="9.5" y="10.5" width="13" height="15" fill="#ffffff" stroke="#d9c6b6"/>' +
      '<path stroke="#b9c6d1" stroke-width="1.3" stroke-linecap="round" d="M12 14.5h8M12 18h8M12 21.5h5"/>' +
      '</svg>',
    cut: '<svg viewBox="0 0 32 32">' +
      '<path stroke="#607d8b" stroke-width="3.2" stroke-linecap="round" d="m11.6 5 7.4 15.2M20.4 5l-7.4 15.2"/>' +
      '<circle cx="9.2" cy="24.6" r="4.1" fill="#eceff1" stroke="#78909c" stroke-width="2"/>' +
      '<circle cx="22.8" cy="24.6" r="4.1" fill="#eceff1" stroke="#78909c" stroke-width="2"/>' +
      '<circle cx="16" cy="13.6" r="1.7" fill="#e53935"/>' +
      '</svg>',
    del: '<svg viewBox="0 0 32 32">' +
      '<path stroke="#b71c1c" stroke-width="8.4" stroke-linecap="round" d="M9.4 9.4 22.6 22.6M22.6 9.4 9.4 22.6"/>' +
      '<path stroke="#ef5350" stroke-width="5" stroke-linecap="round" d="M9.4 9.4 22.6 22.6M22.6 9.4 9.4 22.6"/>' +
      '<path stroke="#ffb4ae" stroke-width="1.5" stroke-linecap="round" d="M10.4 10.4 17.4 17.4" opacity=".85"/>' +
      '</svg>',
    rename: '<svg viewBox="0 0 32 32">' +
      '<rect x="3.5" y="8" width="19" height="14" rx="1.8" fill="#ffffff" stroke="#7f8f9d" stroke-width="1.3"/>' +
      '<path fill="#c3d2e0" d="M3.5 8h19v3.8h-19z"/>' +
      '<path stroke="#9fb0bf" stroke-width="1.3" stroke-linecap="round" d="M6.5 15.4h7M6.5 18.6h5"/>' +
      '<path fill="#ffca28" stroke="#c98b00" stroke-width="1" d="m18 23.2 9-9 2.4 2.4-9 9-3.2.8z"/>' +
      '<path fill="#fff59d" d="m18 23.2 1.5-3 2.4 2.4z"/>' +
      '</svg>',
    newFolder: '<svg viewBox="0 0 32 32">' +
      '<path fill="#e0a52e" d="M3 8.5h8.4l2.6 3H29v15a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>' +
      '<path fill="#ffcf4d" d="M3 12.6h26v13.8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>' +
      '<path fill="#f3b73f" d="M3 12.6h26v1.8H3z"/>' +
      '<path fill="#42a5f5" stroke="#1e88e5" stroke-width="1" d="m24.6 2.4 1.5 3.7 3.7 1.5-3.7 1.5-1.5 3.7-1.5-3.7-3.7-1.5 3.7-1.5z"/>' +
      '</svg>',
    newFile: '<svg viewBox="0 0 32 32">' +
      '<path fill="#ffffff" stroke="#9aa9b6" stroke-width="1.3" d="M7 3.5h12l6 6v19H7z"/>' +
      '<path fill="#e7edf3" stroke="#9aa9b6" stroke-width="1.2" d="M19 3.5v6h6"/>' +
      '<circle cx="23.5" cy="23.5" r="6.4" fill="#e8f5e9" stroke="#2e7d32" stroke-width="1.7"/>' +
      '<path stroke="#2e7d32" stroke-width="2.3" stroke-linecap="round" d="M23.5 20.2v6.6M20.2 23.5h6.6"/>' +
      '</svg>',
    download: '<svg viewBox="0 0 32 32">' +
      '<path fill="#2196f3" d="M12.6 4h6.8v10.4H25L16 25.6 7 14.4h5.6z"/>' +
      '<path fill="#0d47a1" opacity=".18" d="M16 25.6 7 14.4h5.6L16 19.2z"/>' +
      '<rect x="6" y="26.4" width="20" height="3.2" rx="1.4" fill="#1565c0"/>' +
      '</svg>',
    upload: '<svg viewBox="0 0 32 32">' +
      '<path fill="#2196f3" d="M12.6 26h6.8V15.6H25L16 4.2 7 15.6h5.6z"/>' +
      '<path fill="#0d47a1" opacity=".18" d="M16 4.2 7 15.6h5.6L16 10.8z"/>' +
      '<rect x="6" y="26.4" width="20" height="3.2" rx="1.4" fill="#1565c0"/>' +
      '</svg>',
    uploadFolder: '<svg viewBox="0 0 32 32">' +
      '<path fill="#e0a52e" d="M2.5 11.5h7.2l2.2 2.6h8.6v10.4a1.8 1.8 0 0 1-1.8 1.8H4.3a1.8 1.8 0 0 1-1.8-1.8z"/>' +
      '<path fill="#ffcf4d" d="M2.5 14.4h18v10.1a1.8 1.8 0 0 1-1.8 1.8H4.3a1.8 1.8 0 0 1-1.8-1.8z"/>' +
      '<path fill="#2196f3" d="M23.2 26h5.2V16.6h4L24.2 6.4l-8.2 10.2h4z"/>' +
      '<rect x="20" y="27.4" width="12.4" height="2.6" rx="1.2" fill="#1565c0"/>' +
      '</svg>',
    open: '<svg viewBox="0 0 32 32">' +
      '<path fill="#e0a52e" d="M3 8h8.4l2.6 3H28v13.4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>' +
      '<path fill="#ffcf4d" d="M3 12h25v12.4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>' +
      '<path fill="#1e88e5" stroke="#1565c0" stroke-width=".8" d="M14.5 14.4h6.6v-3l6.6 6.3-6.6 6.3v-3h-6.6z"/>' +
      '</svg>',
    preview: '<svg viewBox="0 0 32 32">' +
      '<path fill="#ffffff" stroke="#9aa9b6" stroke-width="1.3" d="M6 3.5h13l5 5v13.5H6z"/>' +
      '<path fill="#e7edf3" stroke="#9aa9b6" stroke-width="1.2" d="M19 3.5v5h5"/>' +
      '<path stroke="#b7c4d0" stroke-width="1.2" stroke-linecap="round" d="M9 13.5h9M9 16.5h7"/>' +
      '<circle cx="21" cy="21" r="6.2" fill="#e3f2fd" stroke="#1e88e5" stroke-width="2.3"/>' +
      '<path stroke="#1e88e5" stroke-width="2.8" stroke-linecap="round" d="m25.6 25.6 4 4"/>' +
      '</svg>',
    zip: '<svg viewBox="0 0 32 32">' +
      '<rect x="4.5" y="7" width="23" height="21" rx="2.2" fill="#ffe0b2" stroke="#8d6e63" stroke-width="1.4"/>' +
      '<path fill="#ffcc80" stroke="#8d6e63" stroke-width="1.4" d="M4.5 9.2A2.2 2.2 0 0 1 6.7 7h18.6a2.2 2.2 0 0 1 2.2 2.2v3.6h-23z"/>' +
      '<path stroke="#8d6e63" stroke-width="1.4" d="M4.5 12.8h23"/>' +
      '<path stroke="#6d4c41" stroke-width="2" stroke-dasharray="2.4 2.2" d="M16 13v11"/>' +
      '<rect x="14" y="22.4" width="4" height="5.4" rx="1.6" fill="#8d6e63" stroke="#5d4037" stroke-width="1"/>' +
      '</svg>',
    extract: '<svg viewBox="0 0 32 32">' +
      '<rect x="3" y="12.5" width="20" height="16" rx="2.2" fill="#ffe0b2" stroke="#8d6e63" stroke-width="1.4"/>' +
      '<path stroke="#8d6e63" stroke-width="1.4" d="M3 17.5h20"/>' +
      '<path fill="#43a047" stroke="#2e7d32" stroke-width=".8" d="M20 20.5h4.6v-9h4.4L23.3 3l-5.7 8.5H20z"/>' +
      '</svg>',
    selectAll: '<svg viewBox="0 0 32 32">' +
      '<rect x="3.5" y="3.5" width="25" height="25" rx="4" fill="#e8f5e9" stroke="#43a047" stroke-width="2"/>' +
      '<path stroke="#2e7d32" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round" fill="none" d="m9.5 16.4 4.6 4.6 8-9.4"/>' +
      '</svg>',
    selectNone: '<svg viewBox="0 0 32 32">' +
      '<rect x="3.5" y="3.5" width="25" height="25" rx="4" fill="#f4f6f8" stroke="#90a4ae" stroke-width="2"/>' +
      '<path stroke="#b0bec5" stroke-width="3.4" stroke-linecap="round" d="M9.6 16h12.8"/>' +
      '</svg>',
    invert: '<svg viewBox="0 0 32 32">' +
      '<rect x="3" y="3" width="18" height="18" rx="3" fill="#1e88e5" stroke="#1565c0" stroke-width="1.5"/>' +
      '<path stroke="#ffffff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" fill="none" d="m7 12 3 3 5.6-6.2"/>' +
      '<rect x="12" y="12" width="18" height="18" rx="3" fill="#ffffff" stroke="#90a4ae" stroke-width="2"/>' +
      '</svg>',
    sort: '<svg viewBox="0 0 32 32">' +
      '<text x="6" y="14" font-size="11.5" font-weight="bold" fill="#455a64" font-family="Arial,Helvetica,sans-serif">A</text>' +
      '<text x="6" y="26" font-size="11.5" font-weight="bold" fill="#b0bec5" font-family="Arial,Helvetica,sans-serif">Z</text>' +
      '<path fill="#1e88e5" d="M20 4.5h3.4v16h4.2L22.4 28 17 20.5h3z"/>' +
      '</svg>',
    refresh: '<svg viewBox="0 0 32 32">' +
      '<path fill="none" stroke="#1e88e5" stroke-width="3" stroke-linecap="round" d="M6.5 16a9.5 9.5 0 0 1 16.4-6.5"/>' +
      '<path fill="#1e88e5" d="M24.4 3.4v7.2h-7.2z"/>' +
      '<path fill="none" stroke="#1e88e5" stroke-width="3" stroke-linecap="round" d="M25.5 16a9.5 9.5 0 0 1-16.4 6.5"/>' +
      '<path fill="#1e88e5" d="M7.6 28.6v-7.2h7.2z"/>' +
      '</svg>',
    navPane: '<svg viewBox="0 0 32 32">' +
      '<rect x="3" y="5" width="26" height="22" rx="2.2" fill="#ffffff" stroke="#78909c" stroke-width="1.6"/>' +
      '<path fill="#bbdefb" d="M4.5 6.5h8.6v19H4.5z"/>' +
      '<path stroke="#78909c" stroke-width="1.4" d="M13.1 6.5v19M16 11h9M16 15h9M16 19h7"/>' +
      '</svg>',
    detailsIcon: '<svg viewBox="0 0 32 32">' +
      '<rect x="3" y="5" width="26" height="22" rx="2.2" fill="#ffffff" stroke="#78909c" stroke-width="1.6"/>' +
      '<path fill="#e3f2fd" d="M18.4 6.5H27.5v19H18.4z"/>' +
      '<path stroke="#78909c" stroke-width="1.4" d="M5 10.5h11M5 15h11M5 19.5h8M18.4 6.5v19"/>' +
      '</svg>',
    ext: '<svg viewBox="0 0 32 32">' +
      '<path fill="#ffffff" stroke="#9aa9b6" stroke-width="1.3" d="M6 3.5h13l5 5v19H6z"/>' +
      '<path fill="#e7edf3" stroke="#9aa9b6" stroke-width="1.2" d="M19 3.5v5h5"/>' +
      '<text x="16" y="23" font-size="7" font-weight="bold" fill="#1e88e5" text-anchor="middle" font-family="Arial,Helvetica,sans-serif">EXT</text>' +
      '</svg>',
    hiddenIcon: '<svg viewBox="0 0 32 32">' +
      '<path fill="#e3f2fd" stroke="#607d8b" stroke-width="1.8" d="M3 16S8 7.5 16 7.5 29 16 29 16s-5 8.5-13 8.5S3 16 3 16z"/>' +
      '<circle cx="16" cy="16" r="4.4" fill="#90a4ae"/>' +
      '<path stroke="#e53935" stroke-width="3" stroke-linecap="round" d="m6 26 20-20"/>' +
      '</svg>',
    /* View tab: the seven layouts, drawn at ribbon weight */
    viewXL: '<svg viewBox="0 0 32 32">' +
      '<rect x="4.5" y="4.5" width="23" height="23" rx="2.4" fill="#1e88e5" stroke="#1565c0" stroke-width="1.4"/>' +
      '<path stroke="#90caf9" stroke-width="1.6" d="M8.5 8.5h15v15h-15z" opacity=".75"/>' +
      '</svg>',
    viewL: '<svg viewBox="0 0 32 32">' +
      '<rect x="3.5" y="3.5" width="11.5" height="11.5" rx="1.6" fill="#1e88e5"/>' +
      '<rect x="17" y="3.5" width="11.5" height="11.5" rx="1.6" fill="#90a4ae"/>' +
      '<rect x="3.5" y="17" width="11.5" height="11.5" rx="1.6" fill="#90a4ae"/>' +
      '<rect x="17" y="17" width="11.5" height="11.5" rx="1.6" fill="#b0bec5"/>' +
      '</svg>',
    viewM: '<svg viewBox="0 0 32 32">' +
      '<g fill="#1e88e5"><rect x="3.2" y="3.2" width="7.4" height="7.4" rx="1.1"/>' +
      '<rect x="12.3" y="3.2" width="7.4" height="7.4" rx="1.1"/><rect x="21.4" y="3.2" width="7.4" height="7.4" rx="1.1"/></g>' +
      '<g fill="#90a4ae"><rect x="3.2" y="12.3" width="7.4" height="7.4" rx="1.1"/>' +
      '<rect x="12.3" y="12.3" width="7.4" height="7.4" rx="1.1"/><rect x="21.4" y="12.3" width="7.4" height="7.4" rx="1.1"/>' +
      '<rect x="3.2" y="21.4" width="7.4" height="7.4" rx="1.1"/><rect x="12.3" y="21.4" width="7.4" height="7.4" rx="1.1"/>' +
      '<rect x="21.4" y="21.4" width="7.4" height="7.4" rx="1.1"/></g>' +
      '</svg>',
    viewS: '<svg viewBox="0 0 32 32">' +
      '<g fill="#546e7a"><rect x="3" y="3" width="5.4" height="5.4" rx="1"/><rect x="10.2" y="3" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="17.4" y="3" width="5.4" height="5.4" rx="1"/><rect x="24.6" y="3" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="3" y="10.2" width="5.4" height="5.4" rx="1"/><rect x="10.2" y="10.2" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="17.4" y="10.2" width="5.4" height="5.4" rx="1"/><rect x="24.6" y="10.2" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="3" y="17.4" width="5.4" height="5.4" rx="1"/><rect x="10.2" y="17.4" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="17.4" y="17.4" width="5.4" height="5.4" rx="1"/><rect x="24.6" y="17.4" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="3" y="24.6" width="5.4" height="5.4" rx="1"/><rect x="10.2" y="24.6" width="5.4" height="5.4" rx="1"/>' +
      '<rect x="17.4" y="24.6" width="5.4" height="5.4" rx="1"/><rect x="24.6" y="24.6" width="5.4" height="5.4" rx="1"/></g>' +
      '</svg>',
    viewList: '<svg viewBox="0 0 32 32">' +
      '<g fill="#90a4ae"><rect x="3.5" y="5" width="5" height="5" rx="1"/><rect x="3.5" y="13.5" width="5" height="5" rx="1"/>' +
      '<rect x="3.5" y="22" width="5" height="5" rx="1"/></g>' +
      '<g fill="#1e88e5"><rect x="11" y="6.6" width="17.5" height="2.6" rx="1.3"/>' +
      '<rect x="11" y="15.1" width="17.5" height="2.6" rx="1.3"/><rect x="11" y="23.6" width="17.5" height="2.6" rx="1.3"/></g>' +
      '</svg>',
    viewDetails: '<svg viewBox="0 0 32 32">' +
      '<rect x="3" y="4.5" width="26" height="5" rx="1.2" fill="#1e88e5"/>' +
      '<g fill="#90a4ae"><rect x="3" y="12.4" width="9" height="2.6" rx="1.3"/><rect x="14" y="12.4" width="15" height="2.6" rx="1.3"/>' +
      '<rect x="3" y="18.2" width="9" height="2.6" rx="1.3"/><rect x="14" y="18.2" width="15" height="2.6" rx="1.3"/>' +
      '<rect x="3" y="24" width="9" height="2.6" rx="1.3"/><rect x="14" y="24" width="15" height="2.6" rx="1.3"/></g>' +
      '</svg>',
    viewTiles: '<svg viewBox="0 0 32 32">' +
      '<g fill="#1e88e5"><rect x="3" y="4.5" width="12" height="10" rx="1.6"/><rect x="17" y="4.5" width="12" height="10" rx="1.6"/></g>' +
      '<g fill="#90a4ae"><rect x="3" y="17" width="12" height="10" rx="1.6"/><rect x="17" y="17" width="12" height="10" rx="1.6"/></g>' +
      '</svg>'
  };
  function ribbonIcon(name) { return RIBBON_ICONS[name] || ICONS[name] || ICONS.file; }

  /* --------------------------------------------------- format icons
   * Format icons: a white sheet with either a large
   * coloured letter (Office) or a coloured plate carrying the extension.
   * ["L", letter, color] = large letter, ["P", label, color, fg?] = plate. */
  var FORMAT_ICONS = {
    /* Office — big letter, like the official W / X / P icons */
    doc: ["L", "W", "#2B579A"], docx: ["L", "W", "#2B579A"], docm: ["L", "W", "#2B579A"],
    dot: ["L", "W", "#2B579A"], dotx: ["L", "W", "#2B579A"], wps: ["L", "W", "#2B579A"],
    rtf: ["P", "RTF", "#2B579A"], odt: ["P", "ODT", "#2B579A"],
    xls: ["L", "X", "#217346"], xlsx: ["L", "X", "#217346"], xlsm: ["L", "X", "#217346"],
    xlsb: ["L", "X", "#217346"], et: ["L", "X", "#217346"],
    csv: ["P", "CSV", "#217346"], ods: ["P", "ODS", "#217346"],
    ppt: ["L", "P", "#D24726"], pptx: ["L", "P", "#D24726"], pps: ["L", "P", "#D24726"],
    ppsx: ["L", "P", "#D24726"], pot: ["L", "P", "#D24726"], potx: ["L", "P", "#D24726"], dps: ["L", "P", "#D24726"],
    odp: ["P", "ODP", "#D24726"],
    vsd: ["P", "VSD", "#3955A3"], vsdx: ["P", "VSD", "#3955A3"], vsdx2: ["P", "VSD", "#3955A3"],
    mpp: ["P", "MPP", "#31752F"], one: ["P", "ONE", "#7719AA"], onetoc2: ["P", "ONE", "#7719AA"],
    xps: ["P", "XPS", "#5C6BC0"],
    /* web / code */
    html: ["P", "HTML", "#E44D26"], htm: ["P", "HTML", "#E44D26"],
    css: ["P", "CSS", "#2965F1"], scss: ["P", "CSS", "#CF649A"], less: ["P", "LESS", "#1D365D"],
    js: ["P", "JS", "#E8B400", "#3B3000"], jsx: ["P", "JS", "#E8B400", "#3B3000"],
    mjs: ["P", "JS", "#E8B400", "#3B3000"],
    ts: ["P", "TS", "#3178C6"], tsx: ["P", "TS", "#3178C6"],
    json: ["P", "JSON", "#B58900"], jsonc: ["P", "JSON", "#B58900"],
    xml: ["P", "XML", "#E65100"], yml: ["P", "YML", "#CB171E"], yaml: ["P", "YML", "#CB171E"],
    cs: ["P", "C#", "#68217A"], java: ["P", "JAVA", "#E76F00"], py: ["P", "PY", "#3776AB"],
    go: ["P", "GO", "#00ADD8"], rs: ["P", "RS", "#B7410E"], php: ["P", "PHP", "#777BB4"],
    rb: ["P", "RB", "#CC342D"], sh: ["P", "SH", "#4EAA25"], bat: ["P", "BAT", "#4EAA25"],
    ps1: ["P", "PS1", "#012456"], sql: ["P", "SQL", "#00758F"],
    vue: ["P", "VUE", "#41B883"], svelte: ["P", "SVE", "#FF3E00"],
    /* design / CAD */
    psd: ["P", "PS", "#001E36", "#31A8FF"], ai: ["P", "AI", "#FF9A00"], eps: ["P", "EPS", "#FF9A00"],
    indd: ["P", "ID", "#FF3366"], dwg: ["P", "DWG", "#0A66B7"], dxf: ["P", "DXF", "#17A2B8"],
    /* packages / system / fonts / mail */
    exe: ["P", "EXE", "#455A64"], msi: ["P", "MSI", "#455A64"], apk: ["P", "APK", "#3DDC84"],
    iso: ["P", "ISO", "#607D8B"], ttf: ["P", "TTF", "#F06292"], otf: ["P", "OTF", "#F06292"],
    woff: ["P", "WOFF", "#F06292"], woff2: ["P", "WOFF", "#F06292"],
    eml: ["P", "EML", "#607D8B"], msg: ["P", "MSG", "#607D8B"]
  };
  var FORMAT_CACHE = {};

  function letterSheet(letter, color) {
    return sheet('<text x="12" y="17.2" font-size="11.5" font-weight="bold" fill="' + color +
      '" text-anchor="middle" font-family="Arial,Helvetica,sans-serif">' + letter + '</text>');
  }
  function plateSheet(label, color, fg) {
    var fs = label.length >= 4 ? 3.5 : label.length === 3 ? 4.1 : 4.9;
    return sheet('<rect x="4.3" y="12.4" width="11" height="6.9" rx="1.4" fill="' + color + '"/>' +
      '<text x="9.8" y="17.5" font-size="' + fs + '" font-weight="bold" fill="' + (fg || "#FFFFFF") +
      '" text-anchor="middle" font-family="Arial,Helvetica,sans-serif">' + label + '</text>');
  }
  function formatIcon(ext) {
    if (FORMAT_CACHE[ext]) return FORMAT_CACHE[ext];
    var f = FORMAT_ICONS[ext];
    if (!f) return null;
    var svg = f[0] === "L" ? letterSheet(f[1], f[2]) : plateSheet(f[1], f[2], f[3]);
    var key = "f:" + ext;
    ICONS[key] = svg;
    FORMAT_CACHE[ext] = key;
    return key;
  }

  function fileIcon(entry) {
    if (entry.isDirectory) return "folder";
    var ext = (entry.extension || "").toLowerCase();
    var fmt = formatIcon(ext);
    if (fmt) return fmt;
    if (["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico", "tif", "tiff", "raw", "heic"].indexOf(ext) >= 0) return "image";
    if (["mp4", "webm", "mov", "avi", "mkv", "m4v", "wmv", "flv", "mpg", "mpeg"].indexOf(ext) >= 0) return "video";
    if (["mp3", "wav", "ogg", "m4a", "flac", "aac", "wma", "opus"].indexOf(ext) >= 0) return "audio";
    if (ext === "pdf") return "pdf";
    if (["zip", "rar", "7z", "tar", "gz", "tgz", "bz2", "tbz2", "xz", "txz", "zst", "lzma", "lz", "arj", "ace", "cab"].indexOf(ext) >= 0) return "archive";
    if (["txt", "log", "nfo", "md", "markdown"].indexOf(ext) >= 0) return "text";
    if (["ini", "conf", "cfg", "toml", "properties", "env"].indexOf(ext) >= 0) return "code";
    return "file";
  }
  var IMAGE_EXTS = ["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico"];

  /* ------------------------------------------------------------------ utils */
  function fmtSize(bytes) {
    if (bytes === 0 || bytes == null) return "0 B";
    var u = ["B", "KB", "MB", "GB", "TB"], i = Math.floor(Math.log(bytes) / Math.log(1024));
    return (bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1) + " " + u[i];
  }
  function fmtDate(ms) {
    if (!ms) return "";
    var d = new Date(ms);
    return d.toLocaleDateString() + " " + d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  }
  function fmtType(e, t) {
    if (e.isDirectory) return t("typeFolder");
    var ext = (e.extension || "").toLowerCase();
    return ext ? t("typeExt", ext.toUpperCase()) : t("typeFile");
  }
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function parentPath(p) {
    if (p === "/") return "/";
    var i = p.lastIndexOf("/");
    return i <= 0 ? "/" : p.slice(0, i);
  }
  function baseName(p) { return p === "/" ? "/" : p.slice(p.lastIndexOf("/") + 1); }
  function joinPath(dir, name) { return dir === "/" ? "/" + name : dir + "/" + name; }

  /* ------------------------------------------------------------- component */
  /* view-name → locale key map lives in VIEW_KEY (see i18n section) */

  /* ---------------------------------------------------------- i18n */
  var VIEW_KEY = { ExtraLargeIcons: "vXL", LargeIcons: "vL", MediumIcons: "vM",
                   SmallIcons: "vS", List: "vList", Details: "vDetails", Tiles: "vTiles" };
  /* private drag MIME so internal moves can be told apart from OS file drops */
  var FM_DRAG_TYPE = "application/x-fmnet-paths";
  /* Material Icon Theme (MIT, see wwwroot/icons/LICENSE-material-icon-theme.txt)
   * maps a file extension to the icon file shipped under wwwroot/icons. */
  /* derived from this script's own URL so a PathBase / custom mount still works */
  var FMNET_ICON_BASE = (function () {
    var el = document.querySelector('script[src*="fmnet.js"]');
    var m = el && (el.getAttribute("src") || "").match(/^(.*?)\/js\/fmnet\.js/);
    return (m ? m[1] : "/_content/FileManagerNet") + "/icons";
  })();
  var EXT_ICONS = {
    "3ds": "3d",
    "3gp": "video",
    "7z": "zip",
    "aab": "android",
    "aac": "audio",
    "accdb": "database",
    "ai": "adobe-illustrator",
    "apk": "android",
    "arj": "zip",
    "asc": "key",
    "astro": "astro",
    "avi": "video",
    "avif": "image",
    "awk": "console",
    "bash": "console",
    "bat": "console",
    "bib": "tex",
    "blend": "blender",
    "bmp": "image",
    "bz2": "zip",
    "c": "c",
    "cab": "zip",
    "cc": "cpp",
    "cer": "certificate",
    "cfg": "settings",
    "cjs": "javascript",
    "class": "java",
    "cls": "tex",
    "cmd": "console",
    "com": "exe",
    "conf": "settings",
    "containerfile": "docker",
    "cpp": "cpp",
    "crt": "certificate",
    "cs": "csharp",
    "cshtml": "csharp",
    "css": "css",
    "csv": "table",
    "csx": "csharp",
    "cts": "typescript",
    "cxx": "cpp",
    "dae": "3d",
    "dart": "dart",
    "db": "database",
    "dbf": "database",
    "dll": "exe",
    "dmg": "zip",
    "doc": "word",
    "dockerfile": "docker",
    "docm": "word",
    "docx": "word",
    "dot": "word",
    "dotx": "word",
    "dps": "powerpoint",
    "dwg": "3d",
    "dxf": "3d",
    "dylib": "exe",
    "editorconfig": "settings",
    "eml": "email",
    "env": "settings",
    "eot": "font",
    "eps": "adobe-illustrator",
    "erb": "ruby",
    "et": "excel",
    "exe": "exe",
    "fbx": "3d",
    "fish": "console",
    "flac": "audio",
    "flv": "video",
    "fon": "font",
    "gemspec": "ruby",
    "gif": "image",
    "gitattributes": "git",
    "gitconfig": "settings",
    "gitignore": "git",
    "gitmodules": "git",
    "glb": "3d",
    "gltf": "3d",
    "go": "go",
    "gql": "graphql",
    "graphql": "graphql",
    "gz": "zip",
    "h": "c",
    "heic": "image",
    "hpp": "cpp",
    "htm": "html",
    "html": "html",
    "hxx": "cpp",
    "ico": "image",
    "iges": "3d",
    "igs": "3d",
    "img": "zip",
    "ini": "settings",
    "ipp": "cpp",
    "ipynb": "python",
    "iso": "zip",
    "jar": "java",
    "java": "java",
    "jfif": "image",
    "jpeg": "image",
    "jpg": "image",
    "js": "javascript",
    "json": "json",
    "json5": "json",
    "jsonc": "json",
    "jsx": "react",
    "key": "key",
    "kt": "kotlin",
    "kts": "kotlin",
    "latex": "tex",
    "less": "sass",
    "license": "document",
    "lock": "lock",
    "log": "document",
    "lua": "lua",
    "lz": "zip",
    "lzma": "zip",
    "m4a": "audio",
    "m4v": "video",
    "markdown": "markdown",
    "mbox": "email",
    "md": "markdown",
    "mdb": "database",
    "mdx": "markdown",
    "mid": "audio",
    "midi": "audio",
    "mjs": "javascript",
    "mkv": "video",
    "mod": "go",
    "mov": "video",
    "mp3": "audio",
    "mp4": "video",
    "mpeg": "video",
    "mpg": "video",
    "msg": "email",
    "msi": "exe",
    "mts": "typescript",
    "nfo": "document",
    "nginx": "nginx",
    "numbers": "table",
    "obj": "3d",
    "odp": "powerpoint",
    "ods": "excel",
    "odt": "word",
    "oga": "audio",
    "ogg": "audio",
    "opus": "audio",
    "ost": "email",
    "otf": "font",
    "p12": "certificate",
    "p7b": "certificate",
    "pages": "word",
    "pdf": "pdf",
    "pem": "certificate",
    "pfx": "certificate",
    "pgp": "key",
    "php": "php",
    "phtml": "php",
    "plist": "xml",
    "ply": "3d",
    "png": "image",
    "pot": "powerpoint",
    "potx": "powerpoint",
    "ppk": "key",
    "pps": "powerpoint",
    "ppsx": "powerpoint",
    "ppt": "powerpoint",
    "pptx": "powerpoint",
    "properties": "settings",
    "ps1": "console",
    "psb": "adobe-photoshop",
    "psd": "adobe-photoshop",
    "psd1": "console",
    "psm1": "console",
    "pst": "email",
    "py": "python",
    "pyi": "python",
    "pyw": "python",
    "r": "r",
    "rar": "zip",
    "raw": "image",
    "razor": "csharp",
    "rb": "ruby",
    "readme": "document",
    "resx": "xml",
    "rmd": "r",
    "rs": "rust",
    "rtf": "word",
    "sass": "sass",
    "scr": "exe",
    "scss": "sass",
    "sh": "console",
    "skp": "3d",
    "so": "exe",
    "sql": "database",
    "sqlite": "database",
    "sqlite3": "database",
    "step": "3d",
    "stl": "3d",
    "stp": "3d",
    "sty": "tex",
    "styl": "sass",
    "sum": "go",
    "svelte": "svelte",
    "svg": "svg",
    "swift": "swift",
    "sys": "exe",
    "tar": "zip",
    "tbz2": "zip",
    "tex": "tex",
    "text": "document",
    "tf": "terraform",
    "tfvars": "terraform",
    "tgz": "zip",
    "tif": "image",
    "tiff": "image",
    "toml": "settings",
    "ts": "typescript",
    "tsv": "table",
    "tsx": "react",
    "ttf": "font",
    "txt": "document",
    "vue": "vue",
    "war": "java",
    "wav": "audio",
    "webm": "video",
    "webp": "image",
    "wma": "audio",
    "wmv": "video",
    "woff": "font",
    "woff2": "font",
    "wps": "word",
    "xhtml": "html",
    "xls": "excel",
    "xlsb": "excel",
    "xlsm": "excel",
    "xlsx": "excel",
    "xml": "xml",
    "xsl": "xml",
    "xslt": "xml",
    "xz": "zip",
    "yaml": "yaml",
    "yml": "yaml",
    "zip": "zip",
    "zsh": "console",
    "zst": "zip"
  };
  var VIEW_ICON = { ExtraLargeIcons: "viewXL", LargeIcons: "viewL", MediumIcons: "viewM",
                    SmallIcons: "viewS", List: "viewList", Details: "viewDetails", Tiles: "viewTiles" };

  /* Locales ship as one file per language under js/locales/ and are loaded
   * on demand (only the resolved language + the "en" fallback are fetched).
   * Custom languages: assign a dictionary to window.FMNetLocales["<id>"]
   * from any script that runs before initialization, then pass
   * locale="<id>" — no file in this package is needed. */
  var BUNDLED_LOCALES = ["en", "zh-CN", "zh-TW", "ja", "ko", "de", "fr", "es", "ru"];
  var _localeBase = (function () {
    var s = document.querySelector('script[src*="fmnet.js"]');
    if (!s) return "";
    return s.getAttribute("src").slice(0, s.getAttribute("src").lastIndexOf("/") + 1) + "locales/";
  })();
  var _localeVersion = (function () {
    var s = document.querySelector('script[src*="fmnet.js"]');
    var m = s && /[?&]v=([^&]+)/.exec(s.getAttribute("src") || "");
    return m ? m[1] : "";
  })();
  var _localeLoads = {};
  function loadLocale(id) {
    var dict = window.FMNetLocales || {};
    if (dict[id]) return Promise.resolve();          /* already provided (custom or cached) */
    if (_localeLoads[id]) return _localeLoads[id];
    if (!/^[A-Za-z]{2,3}(-[A-Za-z0-9]+)*$/.test(id)) return Promise.resolve();
    _localeLoads[id] = new Promise(function (resolve) {
      var s = document.createElement("script");
      s.src = _localeBase + "fmnet.locale." + id + ".js" + (_localeVersion ? "?v=" + _localeVersion : "");
      s.onload = function () { resolve(); };
      s.onerror = function () { resolve(); };        /* missing file → fall back to en */
      document.head.appendChild(s);
    });
    return _localeLoads[id];
  }

  FileManager.prototype._resolveLocale = function () {
    var dict = window.FMNetLocales || {};
    function avail(id) { return !!dict[id] || BUNDLED_LOCALES.indexOf(id) >= 0; }
    var forced = (this.config && this.config.locale) || this.wrap.getAttribute("data-locale");
    if (forced && avail(forced)) return forced;
    var candidates = (navigator.languages && navigator.languages.length)
      ? navigator.languages : [navigator.language || "en"];
    for (var i = 0; i < candidates.length; i++) {
      var l = candidates[i];
      if (avail(l)) return l;
      var base = (l.split("-")[0] || "").toLowerCase();
      for (var k = 0; k < BUNDLED_LOCALES.length; k++)
        if ((BUNDLED_LOCALES[k].split("-")[0] || "").toLowerCase() === base) return BUNDLED_LOCALES[k];
      for (var key in dict)
        if ((key.split("-")[0] || "").toLowerCase() === base) return key;
    }
    return "en";
  };

  /* translate: t("key") or t("key", arg0, arg1) — {0}/{1} placeholders */
  FileManager.prototype.t = function (key) {
    var dict = window.FMNetLocales || {};
    var pack = dict[this.locale] || dict.en || {};
    var s = pack[key] != null ? pack[key] : (dict.en ? dict.en[key] : null);
    if (s == null) s = key;
    for (var i = 1; i < arguments.length; i++)
      s = s.split("{" + (i - 1) + "}").join(String(arguments[i]));
    return s;
  };

  function FileManager(container) {    this.wrap = container;                      // outer wrapper (persists)
    this.el = container.querySelector(":scope > .fmnet-root") || container;  // mount target
    /* mounting without the tag helper leaves the flex layout without its root
     * class, which used to collapse the list area to its content height */
    if (!this.el.classList.contains("fmnet-root")) this.el.classList.add("fmnet-root");
    this.id = container.getAttribute("data-instance");
    this.api = container.getAttribute("data-api");
    this.rootName = container.getAttribute("data-root-name") || "Home";
    /* several root folders mounted: "/" only holds those folders and cannot be
     * written to (create / paste / upload / drop are disabled there) */
    this.multiRoot = container.getAttribute("data-multi-root") === "1";
    try {
      var raw = container.getAttribute("data-config");
      this.config = raw ? JSON.parse(raw) : {};
    } catch (e) { this.config = {}; }
    /* the configured flags are the ceiling: a folder can only take permissions
     * away, never add what the host disabled */
    try { this._baseConfig = JSON.parse(JSON.stringify(this.config || {})); }
    catch (e2) { this._baseConfig = {}; }
    var cfg = this.config || {};
    this.locale = this._resolveLocale();
    this.path = "/";
    this.entries = [];
    this.selection = new Set();       // paths
    this.clipboard = null;            // {mode:'copy'|'cut', paths:[]}
    this.view = localStorage.getItem("fmnet-view-" + this.id) || cfg.defaultView || "Details";
    this.sortKey = "name"; this.sortDir = 1;
    this.searchMode = false;
    this.expanded = new Set(["/"]);
    this.treeChildren = { "/": [] };
    /* ribbon / panes state (persisted per instance) */
    var saved = {};
    try { saved = JSON.parse(localStorage.getItem("fmnet-ui-" + this.id) || "{}") || {}; } catch (e) {}
    this.activeTab = saved.tab === "view" ? "view" : "home";
    /* ribbon can be collapsed to the tab strip */
    this.ribbonCollapsed = !!saved.ribbonCollapsed;
    this.ribbonFloating = false;      /* transient: body shown over content */
    this.showTree = saved.tree != null ? !!saved.tree : cfg.showFoldersPane !== false;
    this.drawerOpen = false;   /* compact mode: tree slides over the list */
    this.showPreview = !!saved.preview;
    this.showDetails = !!saved.details;
    this.showExtensions = saved.ext != null ? !!saved.ext : !!cfg.showFileExtensions;
    this.showHidden = saved.hidden != null ? !!saved.hidden : !!cfg.showHiddenFiles;
    this._buildUI();
    this.refreshTree();
    this.navigate("/");
  }

  FileManager.prototype.post = function (command, payload) {
    var body = Object.assign({ command: command }, payload || {});
    return fetch(this.api + "?instance=" + encodeURIComponent(this.id), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    }).then(function (r) { return r.json(); }).then(function (res) {
      if (!res.ok) {
        var err = new Error(res.error || "Request failed");
        if (res.conflicts) err.conflicts = res.conflicts;
        throw err;
      }
      return res.data;
    });
  };

  /* Starts a long-running command in the background; resolves with the job id
   * so the caller can poll `getJob` and render a progress bar. */
  FileManager.prototype.postJob = function (command, payload) {
    var body = Object.assign({ command: command, async: true }, payload || {});
    return fetch(this.api + "?instance=" + encodeURIComponent(this.id), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    }).then(function (r) { return r.json(); }).then(function (res) {
      if (!res.ok) throw new Error(res.error || "Request failed");
      if (!res.data || !res.data.jobId) throw new Error("Job was not started");
      return res.data.jobId;
    });
  };

  FileManager.prototype.getJob = function (jobId) {
    return fetch(this.api + "/job?instance=" + encodeURIComponent(this.id) +
                 "&id=" + encodeURIComponent(jobId))
      .then(function (r) { return r.json(); })
      .then(function (res) {
        if (!res.ok) throw new Error(res.error || "Job not found");
        return res.data;
      });
  };


  FileManager.prototype._buildUI = function () {
    var c = this.config, self = this;
    var html = '';

    /* Ribbon toolbar — Home / View tabs, grouped buttons with captions */
    var homeGroups = [
      ["grpClipboard", [
        ["copy", "copy", this.t("copy"), c.allowCopyMove !== false],
        ["paste", "paste", this.t("paste"), c.allowCopyMove !== false],
        ["cut", "cut", this.t("cut"), c.allowCopyMove !== false]
      ]],
      ["grpOrganize", [
        ["delete", "del", this.t("delete"), c.allowDelete !== false],
        ["rename", "rename", this.t("rename"), c.allowRename !== false]
      ]],
      ["grpNew", [
        ["newFolder", "newFolder", this.t("newFolder"), c.allowCreateFolder !== false],
        ["newFile", "newFile", this.t("newFile"), c.allowCreateFile !== false]
      ]],
      ["grpTransfer", [
        ["download", "download", this.t("download"), c.allowDownload !== false],
        ["upload", "upload", this.t("upload"), c.allowUpload !== false],
        ["uploadFolder", "uploadFolder", this.t("uploadFolder"), c.allowUpload !== false]
      ]],
      ["grpOpen", [
        ["open", "open", this.t("open"), true],
        ["preview", "preview", this.t("preview"), true]
      ]],
      ["grpCompression", [
        ["zip", "zip", this.t("zip"), c.allowArchive !== false],
        ["extract", "extract", this.t("extract"), c.allowArchive !== false]
      ]],
      ["grpSelect", [
        ["selectAll", "selectAll", this.t("selectAll"), true],
        ["selectNone", "selectNone", this.t("selectNone"), true],
        ["invertSelection", "invert", this.t("invertSelection"), true]
      ]]
    ];
    var group = function (cap, btnsHtml) {
      return '<div class="fm-rgroup"><div class="fm-rgroup-btns">' + btnsHtml +
             '</div><div class="fm-rgroup-cap">' + esc(self.t(cap)) + '</div></div>';
    };
    var homeHtml = '';
    homeGroups.forEach(function (g) {
      homeHtml += group(g[0], g[1].map(function (b) {
        return self._ribbonButton(b[0], b[1], b[2], b[3]);
      }).join(""));
    });

    /* View tab: panes / layout / sort / show-hide */
    var layoutBtns = ["ExtraLargeIcons", "LargeIcons", "MediumIcons", "SmallIcons",
                      "List", "Details", "Tiles"].map(function (v) {
      return self._viewButton(v);
    }).join("");
    var viewHtml =
      group("grpPanes",
        self._ribbonToggle("toggleNav", "navPane", self.t("navPane"), self.showTree) +
        self._ribbonToggle("togglePreview", "preview", self.t("previewPane"), self.showPreview) +
        self._ribbonToggle("toggleDetails", "detailsIcon", self.t("detailsPane"), self.showDetails)) +
      group("grpLayout", layoutBtns) +
      group("grpSort", self._ribbonButton("sortBy", "sort", self.t("sortBy"), true)) +
      group("grpShowHide",
        self._ribbonToggle("toggleExtensions", "ext", self.t("showExt"), self.showExtensions) +
        self._ribbonToggle("toggleHidden", "hiddenIcon", self.t("showHidden"), self.showHidden));

    html += '<div class="fm-ribbon">' +
      '<div class="fm-ribbon-tabs">' +
        '<button class="fm-tab' + (this.activeTab === "home" ? " active" : "") + '" data-tab="home">' + esc(this.t("tabHome")) + '</button>' +
        '<button class="fm-tab' + (this.activeTab === "view" ? " active" : "") + '" data-tab="view">' + esc(this.t("tabView")) + '</button>' +
        '<button class="fm-ribbon-collapse" type="button"></button>' +
      '</div>' +
      '<div class="fm-ribbon-body" data-body="home"' + (this.activeTab === "home" ? '' : ' hidden') + '>' + homeHtml + '</div>' +
      '<div class="fm-ribbon-body" data-body="view"' + (this.activeTab === "view" ? '' : ' hidden') + '>' + viewHtml + '</div>' +
      '<input type="file" class="fm-fileinput" data-role="fileInput" multiple>' +
      '<input type="file" class="fm-fileinput" data-role="folderInput" webkitdirectory directory multiple>' +
      '</div>';

    /* path bar: single row — nav toggle (compact) + breadcrumb + refresh
     * + view selector + search */
    html += '<div class="fm-pathbar">' +
            '<button class="fm-navbtn fm-navbtn-nav" data-act="toggleNav" title="' + esc(this.t("navPane")) +
            '" aria-label="' + esc(this.t("navPane")) + '">' + icon("navPane") + '</button>' +
            '<button class="fm-navbtn" data-act="up" title="' + esc(this.t("upLevel")) +
            '" aria-label="' + esc(this.t("upLevel")) + '">' + icon("up") + '</button>' +
            '<button class="fm-navbtn" data-act="home" title="' + esc(this.t("home")) +
            '" aria-label="' + esc(this.t("home")) + '">' + icon("home") + '</button>' +
            '<div class="fm-crumbs" data-role="crumbs"></div>' +
            '<span class="fm-pathsep"></span>' +
            '<button class="fm-navbtn" data-act="refresh" title="' + esc(this.t("refresh")) +
            '" aria-label="' + esc(this.t("refresh")) + '">' + icon("refresh") + '</button>' +
            /* the search box follows the same switch as the API, so a disabled
             * host does not leave a box that always fails */
            (this.config.allowSearch === false ? '' :
              '<label class="fm-search">' +
              '<input type="text" placeholder="' + esc(this.t("searchPh")) + '" data-role="searchBox">' +
              icon("search") + '</label>') +
            '</div>';
    html += '<div class="fm-main" data-role="main">' +
            '<div class="fm-tree" data-role="tree"></div>' +
            '<div class="fm-tree-backdrop" data-role="treeBackdrop"></div>' +
            '<div class="fm-listwrap" data-role="listwrap">' +
            '<div class="fm-dropzone" data-role="dropzone">' + esc(this.t("dropHere")) + '</div>' +
            '<div data-role="list"></div>' +
            '</div>' +
            '<div class="fm-pane" data-role="pane" hidden></div>' +
            '</div>';
    html += '<div class="fm-statusbar" data-role="statusbar"></div>';
    this.el.innerHTML = html;

    this.$ = {};
    var self2 = this;
    this.el.querySelectorAll("[data-role]").forEach(function (n) { self2.$[n.getAttribute("data-role")] = n; });

    /* ribbon tab switching (while collapsed, a tab pops the body out) */
    this.el.querySelector(".fm-ribbon-tabs").addEventListener("click", function (e) {
      var tab = e.target.closest("[data-tab]");
      if (!tab) return;
      self2.activeTab = tab.getAttribute("data-tab");
      if (self2.ribbonCollapsed) self2.ribbonFloating = true;
      self2._applyUIState();
      self2._saveUI();
    });

    /* collapse / expand / pin the ribbon */
    var collapseBtn = this.el.querySelector(".fm-ribbon-collapse");
    if (collapseBtn) collapseBtn.addEventListener("click", function (e) {
      e.stopPropagation();
      if (self2.ribbonFloating) {          /* pin → keep it expanded */
        self2.ribbonFloating = false;
        self2.ribbonCollapsed = false;
      } else {
        self2.ribbonCollapsed = !self2.ribbonCollapsed;
      }
      self2._applyUIState();
      self2._saveUI();
    });
    /* clicking outside a popped-out ribbon closes it again */
    document.addEventListener("mousedown", function (e) {
      if (!self2.ribbonFloating) return;
      var rb = self2.el.querySelector(".fm-ribbon");
      if (rb && rb.contains(e.target)) return;
      self2.ribbonFloating = false;
      self2._applyUIState();
    });

    /* ribbon clicks: view-layout buttons first, then commands */
    this.el.querySelector(".fm-ribbon").addEventListener("click", function (e) {
      var vb = e.target.closest("[data-view]");
      if (vb) {
        self2.setView(vb.getAttribute("data-view"));
        if (self2.ribbonFloating) { self2.ribbonFloating = false; self2._applyUIState(); }
        return;
      }
      var btn = e.target.closest("[data-act]");
      if (!btn || btn.disabled) return;
      self2.act(btn.getAttribute("data-act"), e);
      if (self2.ribbonFloating) { self2.ribbonFloating = false; self2._applyUIState(); }
    });
    /* view dropdown now lives in the status bar, which renderStatus rebuilds */
    if (this.$.statusbar) {
      this.$.statusbar.addEventListener("click", function (e) {
        var sel = e.target.closest(".fm-view-select");
        if (!sel) return;
        e.stopPropagation();
        self2._showViewMenu(sel);
      });
    }

    /* compact navigation button + drawer backdrop */
    if (this.$.treeBackdrop) this.$.treeBackdrop.addEventListener("click", function () {
      self2.drawerOpen = false;
      self2._applyUIState();
    });
    /* refresh button sits in the path bar (outside the crumbs container) */
    this.el.querySelector(".fm-pathbar").addEventListener("click", function (e) {
      var b = e.target.closest("[data-act]");
      if (!b || b.disabled) return;
      e.stopPropagation();
      self2.act(b.getAttribute("data-act"), e);
    });

    /* search box (absent when the host disabled search) */
    if (this.$.searchBox) this.$.searchBox.addEventListener("keydown", function (e) {
      if (e.key === "Enter") self2.runSearch(self2.$.searchBox.value);
      if (e.key === "Escape") self2.runSearch("");
    });

    /* file input */
    this.$.fileInput.addEventListener("change", function () {
      if (self2.$.fileInput.files.length) self2.uploadFiles(self2.$.fileInput.files);
      self2.$.fileInput.value = "";
    });
    /* folder input: every File carries webkitRelativePath, so the server can
     * recreate the tree */
    this.$.folderInput.addEventListener("change", function () {
      if (self2.$.folderInput.files.length) self2.uploadFiles(self2.$.folderInput.files);
      self2.$.folderInput.value = "";
    });

    /* list interactions */
    var list = this.$.listwrap;
    var lastClick = { path: null, t: 0 };

    /* details header: click a column to sort, click again to reverse */
    this.$.list.addEventListener("click", function (e) {
      var th = e.target.closest("th[data-sort]");
      if (!th) return;
      var key = th.getAttribute("data-sort");
      if (self2.sortKey === key) self2.sortDir = -self2.sortDir;
      else { self2.sortKey = key; self2.sortDir = 1; }
      self2.renderList();
    });

    /* virtual scrolling: only the visible rows exist in the DOM, so scrolling
     * re-renders the window — coalesced into one render per animation frame */
    var scrollTick = false;
    list.addEventListener("scroll", function () {
      if (!self2._virtual || scrollTick) return;
      scrollTick = true;
      requestAnimationFrame(function () { scrollTick = false; self2._renderWindow(); });
    });

    /* ---- rubber-band selection: drag across empty space to select many ---- */
    var band = null, bandStart = null, bandBase = [], bandMoved = false;

    function rectOf(x1, y1, x2, y2) {
      return { left: Math.min(x1, x2), top: Math.min(y1, y2),
               right: Math.max(x1, x2), bottom: Math.max(y1, y2) };
    }

    function paintBand(r) {
      var nodes = self2.$.list.querySelectorAll("[data-path]");
      self2.selection = new Set(bandBase);
      for (var i = 0; i < nodes.length; i++) {
        var b = nodes[i].getBoundingClientRect();
        var hit = b.left < r.right && b.right > r.left && b.top < r.bottom && b.bottom > r.top;
        if (hit) self2.selection.add(nodes[i].getAttribute("data-path"));
        nodes[i].classList.toggle("selected", hit);
      }
      self2.renderStatus();
    }

    function onBandMove(e) {
      if (!band) return;
      var dx = e.clientX - bandStart.x, dy = e.clientY - bandStart.y;
      if (!bandMoved && Math.abs(dx) < 4 && Math.abs(dy) < 4) return;
      bandMoved = true;
      var r = rectOf(bandStart.x, bandStart.y, e.clientX, e.clientY);
      band.style.left = r.left + "px";
      band.style.top = r.top + "px";
      band.style.width = (r.right - r.left) + "px";
      band.style.height = (r.bottom - r.top) + "px";
      paintBand(r);
    }

    function onBandUp() {
      document.removeEventListener("mousemove", onBandMove);
      document.removeEventListener("mouseup", onBandUp);
      if (band) { band.remove(); band = null; }
      if (!bandMoved) {          /* a plain click on empty space clears the selection */
        self2.selection.clear();
        self2.renderList();
      }
      bandStart = null;
    }

    list.addEventListener("mousedown", function (e) {
      if (e.button !== 0) return;
      if (e.target.closest("[data-path]") || e.target.closest("th") ||
          e.target.closest(".fm-searchbanner")) return;
      e.preventDefault();
      bandStart = { x: e.clientX, y: e.clientY };
      bandBase = (e.ctrlKey || e.metaKey) ? Array.from(self2.selection) : [];
      bandMoved = false;
      band = document.createElement("div");
      band.className = "fm-band";
      document.body.appendChild(band);
      document.addEventListener("mousemove", onBandMove);
      document.addEventListener("mouseup", onBandUp);
    });
    list.addEventListener("click", function (e) {
      var row = e.target.closest("[data-path]");
      if (!row) return;
      var p = row.getAttribute("data-path");
      var now = Date.now();
      self2._selectClick(p, e, row);
      /* manual double-click detection: the selection re-render replaces the
       * DOM node between clicks, which kills the native dblclick event */
      if (!e.ctrlKey && !e.shiftKey && lastClick.path === p && now - lastClick.t < 400) {
        lastClick = { path: null, t: 0 };
        self2._open(p);
      } else {
        lastClick = { path: p, t: now };
      }
    });
    list.addEventListener("contextmenu", function (e) {
      e.preventDefault();
      var row = e.target.closest("[data-path]");
      if (row && !self2.selection.has(row.getAttribute("data-path")))
        self2.selection = new Set([row.getAttribute("data-path")]);
      if (!row) self2._clearSelection();
      self2.renderList();
      self2._showContextMenu(e.clientX, e.clientY, !!row);
    });
    /* ---- internal drag & drop: move items onto a folder ---- */
    var dropHint = null;
    function clearHint() {
      if (dropHint) { dropHint.classList.remove("drop-target"); dropHint = null; }
    }
    function setHint(el) {
      if (dropHint === el) return;
      clearHint();
      dropHint = el;
      el.classList.add("drop-target");
    }

    list.addEventListener("dragstart", function (e) {
      var row = e.target.closest("[data-path]");
      if (!row) return;
      /* dragging an unselected row drags just that row */
      var p = row.getAttribute("data-path");
      if (!self2.selection.has(p)) {
        self2.selection = new Set([p]);
        var nodes = self2.$.list.querySelectorAll("[data-path]");
        for (var i = 0; i < nodes.length; i++)
          nodes[i].classList.toggle("selected", nodes[i].getAttribute("data-path") === p);
        self2.renderStatus();
      }
      self2._dragPaths = Array.from(self2.selection);
      e.dataTransfer.effectAllowed = "copyMove";
      e.dataTransfer.setData(FM_DRAG_TYPE, JSON.stringify(self2._dragPaths));
      e.dataTransfer.setData("text/plain", self2._dragPaths.join("\n"));
      list.classList.add("dragging");
    });

    list.addEventListener("dragend", function () {
      list.classList.remove("dragging");
      clearHint();
      self2._dragPaths = null;
    });

    list.addEventListener("dragover", function (e) {
      if (!self2._dragPaths) return;
      var row = e.target.closest("[data-path]");
      if (!row || row.getAttribute("data-dir") !== "1" ||
          !self2._canDropTo(row.getAttribute("data-path"))) {
        clearHint();
        return;
      }
      e.preventDefault();
      e.dataTransfer.dropEffect = "move";
      setHint(row);
    });

    list.addEventListener("dragleave", function (e) {
      var row = e.target.closest("[data-path]");
      if (row && dropHint === row) clearHint();
    });

    list.addEventListener("drop", function (e) {
      if (!self2._dragPaths) return;
      var row = e.target.closest("[data-path]");
      if (!row || row.getAttribute("data-dir") !== "1") return;
      e.preventDefault();
      e.stopPropagation();          /* keep the upload handler out of it */
      clearHint();
      self2._moveTo(row.getAttribute("data-path"));
    });

    /* breadcrumb segments accept drops too, so items can be moved up a level */
    if (this.$.crumbs) {
      this.$.crumbs.addEventListener("dragover", function (e) {
        if (!self2._dragPaths) return;
        var c = e.target.closest("[data-path]");
        if (!c || !self2._canDropTo(c.getAttribute("data-path"))) { clearHint(); return; }
        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
        setHint(c);
      });
      this.$.crumbs.addEventListener("dragleave", function (e) {
        var c = e.target.closest("[data-path]");
        if (c && dropHint === c) clearHint();
      });
      this.$.crumbs.addEventListener("drop", function (e) {
        if (!self2._dragPaths) return;
        var c = e.target.closest("[data-path]");
        if (!c) return;
        e.preventDefault();
        e.stopPropagation();
        clearHint();
        self2._moveTo(c.getAttribute("data-path"));
      });
    }

    /* a thumbnail that fails to load falls back to the format icon */
    list.addEventListener("error", function (e) {
      var img = e.target;
      if (!img || img.tagName !== "IMG" || !img.parentNode) return;
      var row = img.closest("[data-path]");
      var p = row && row.getAttribute("data-path");
      var entry = self2.entries.filter(function (x) { return x.path === p; })[0]
               || { isDirectory: false, extension: "" };
      var tmp = document.createElement("span");
      tmp.innerHTML = icon(fileIcon(entry));
      if (tmp.firstChild) img.parentNode.replaceChild(tmp.firstChild, img);
    }, true);

    /* tree interactions */
    this.$.tree.addEventListener("click", function (e) {
      var tw = e.target.closest("[data-twist]");
      if (tw) { e.stopPropagation(); self2._toggleNode(tw.getAttribute("data-twist")); return; }
      var row = e.target.closest("[data-path]");
      if (row) {
        self2.navigate(row.getAttribute("data-path"));
        if (self2._isCompact() && self2.drawerOpen) {
          self2.drawerOpen = false;
          self2._applyUIState();
        }
      }
    });

    /* folder tree accepts drops from the file list */
    this.$.tree.addEventListener("dragover", function (e) {
      if (!self2._dragPaths) return;
      var row = e.target.closest("[data-path]");
      if (!row || !self2._canDropTo(row.getAttribute("data-path"))) { clearHint(); return; }
      e.preventDefault();
      e.dataTransfer.dropEffect = "move";
      setHint(row);
    });
    this.$.tree.addEventListener("dragleave", function (e) {
      var row = e.target.closest("[data-path]");
      if (row && dropHint === row) clearHint();
    });
    this.$.tree.addEventListener("drop", function (e) {
      if (!self2._dragPaths) return;
      var row = e.target.closest("[data-path]");
      if (!row) return;
      e.preventDefault();
      e.stopPropagation();
      clearHint();
      self2._moveTo(row.getAttribute("data-path"));
    });

    /* drag & drop upload — ignored while an internal drag is in flight */
    if (c.allowUpload !== false) {
      var dz = this.$.dropzone, wrap = this.$.listwrap, dragDepth = 0;
      var internal = function (e) {
        var t = e.dataTransfer && e.dataTransfer.types;
        return !!t && Array.prototype.indexOf.call(t, FM_DRAG_TYPE) >= 0;
      };
      wrap.addEventListener("dragenter", function (e) {
        if (internal(e)) return;
        e.preventDefault(); dragDepth++; dz.classList.add("active");
      });
      wrap.addEventListener("dragover", function (e) {
        if (internal(e)) return;
        e.preventDefault();
      });
      wrap.addEventListener("dragleave", function (e) {
        if (internal(e)) return;
        if (--dragDepth <= 0) { dragDepth = 0; dz.classList.remove("active"); }
      });
      wrap.addEventListener("drop", function (e) {
        if (internal(e)) return;                 /* the list drop handler owns it */
        e.preventDefault(); dragDepth = 0; dz.classList.remove("active");
        if (self2.multiRoot && self2.path === "/") { self2._warnRootReadOnly(); return; }
        /* entries must be read synchronously — the DataTransfer is cleared once
         * the event handler returns; the tree walk itself is async */
        var entries = self2._dropEntries(e.dataTransfer);
        entries.then(function (list) { if (list.length) self2.uploadFiles(list); });
      });
    }

    /* keyboard — with several instances on one page only the one that was
     * clicked last reacts, otherwise Ctrl+A / Delete would hit all of them */
    document.addEventListener("keydown", function (e) {
      if (window.__fmnetActive && window.__fmnetActive !== self2) return;
      if (!window.__fmnetActive && document.querySelectorAll("[data-fmnet]").length > 1) return;
      if (document.querySelector(".fm-overlay")) return;
      if (!self2.el.contains(document.activeElement) && document.activeElement !== document.body) return;
      if (e.key === "Delete" && self2.selection.size) self2.act("delete");
      if (e.key === "F2" && self2.selection.size === 1) self2.act("rename");
      if (e.ctrlKey && e.key === "a") { e.preventDefault(); self2._selectAll(); }
      if (e.ctrlKey && e.key === "c") self2.act("copy");
      if (e.ctrlKey && e.key === "x") self2.act("cut");
      if (e.ctrlKey && e.key === "v") self2.act("paste");
      if (e.key === "Escape") self2._closeMenus();
    });
    /* one shared pointer tracker decides which instance owns the keyboard */
    if (!window.__fmnetPointerBound) {
      window.__fmnetPointerBound = true;
      document.addEventListener("pointerdown", function (e) {
        var host = e.target.closest ? e.target.closest("[data-fmnet]") : null;
        window.__fmnetActive = (host && host.fmnet) ? host.fmnet : null;
      }, true);
    }

    /* keep compact drawer state in sync when the viewport is resized */
    var wasCompact = this._isCompact();
    window.addEventListener("resize", function () {
      var now = self2._isCompact();
      if (wasCompact !== now) { wasCompact = now; if (!now) self2.drawerOpen = false; }
      self2._applyUIState();
      /* the column count of the grid views depends on the width */
      self2._win = null;
      if (self2._virtual) self2._renderWindow();
    });

    this._applyUIState();
  };

  FileManager.prototype._ribbonButton = function (act, iconName, label, enabled) {
    return '<button class="fm-btn" data-act="' + act + '"' + (enabled ? '' : ' disabled') + '>' +
           ribbonIcon(iconName) + '<span>' + label + '</span></button>';
  };

  /* toggle button for the View tab (panes / show-hide) */
  FileManager.prototype._ribbonToggle = function (act, iconName, label, on) {
    return '<button class="fm-btn fm-toggle' + (on ? " on" : "") + '" data-act="' + act + '"' +
           ' title="' + esc(label) + '" aria-pressed="' + (on ? "true" : "false") + '">' +
           ribbonIcon(iconName) + '<span>' + label + '</span></button>';
  };

  /* layout button for the View tab */
  FileManager.prototype._viewButton = function (v) {
    var label = this.t(VIEW_KEY[v] || "vDetails");
    return '<button class="fm-btn fm-view-btn' + (this.view === v ? " on" : "") + '" data-view="' + v + '"' +
           ' title="' + esc(label) + '">' + ribbonIcon(VIEW_ICON[v] || "view") +
           '<span>' + esc(label) + '</span></button>';
  };

  /* ------------------------------------------------- ribbon / panes state */
  FileManager.prototype._saveUI = function () {
    try {
      localStorage.setItem("fmnet-ui-" + this.id, JSON.stringify({
        tab: this.activeTab, tree: this.showTree, preview: this.showPreview,
        details: this.showDetails, ext: this.showExtensions, hidden: this.showHidden,
        ribbonCollapsed: this.ribbonCollapsed
      }));
    } catch (e) { /* private mode — ignore */ }
  };

  FileManager.prototype._applyUIState = function () {
    var self = this;
    var bh = this.el.querySelector('[data-body="home"]');
    var bv = this.el.querySelector('[data-body="view"]');
    var ribbon = this.el.querySelector(".fm-ribbon");
    var showBody = !this.ribbonCollapsed || this.ribbonFloating;
    if (ribbon) {
      ribbon.classList.toggle("collapsed", this.ribbonCollapsed);
      ribbon.classList.toggle("floating", this.ribbonCollapsed && this.ribbonFloating);
    }
    if (bh) bh.hidden = !(showBody && this.activeTab === "home");
    if (bv) bv.hidden = !(showBody && this.activeTab === "view");
    this.el.querySelectorAll(".fm-tab").forEach(function (t) {
      t.classList.toggle("active", t.getAttribute("data-tab") === self.activeTab);
    });
    /* collapse button: ∧ collapse · ∨ expand · 📌 pin (when popped out) */
    var cb = this.el.querySelector(".fm-ribbon-collapse");
    if (cb) {
      var st = this.ribbonFloating ? "pin" : this.ribbonCollapsed ? "downShort" : "upShort";
      if (cb.getAttribute("data-icon") !== st) {
        cb.innerHTML = icon(st);
        cb.setAttribute("data-icon", st);
      }
      var title = this.ribbonFloating ? this.t("pinRibbon")
                : this.ribbonCollapsed ? this.t("expandRibbon") : this.t("collapseRibbon");
      cb.title = title;
      cb.setAttribute("aria-label", title);
      cb.setAttribute("aria-expanded", showBody ? "true" : "false");
    }
    var compact = this._isCompact();
    if (this.$.main) {
      this.$.main.classList.toggle("compact", compact);
      this.$.main.classList.toggle("tree-open", compact && this.drawerOpen);
      this.$.main.classList.toggle("no-tree", !compact && !this.showTree);
      this.$.main.classList.toggle("has-pane", !!(this.showPreview || this.showDetails));
    }
    if (this.$.pane) this.$.pane.hidden = !(this.showPreview || this.showDetails);
    this.el.querySelectorAll(".fm-toggle").forEach(function (b) {
      var act = b.getAttribute("data-act"), on = false;
      if (act === "toggleNav") on = self._isCompact() ? self.drawerOpen : self.showTree;
      else if (act === "togglePreview") on = self.showPreview;
      else if (act === "toggleDetails") on = self.showDetails;
      else if (act === "toggleExtensions") on = self.showExtensions;
      else if (act === "toggleHidden") on = self.showHidden;
      b.classList.toggle("on", on);
      b.setAttribute("aria-pressed", on ? "true" : "false");
    });
    var navBtn = this.el.querySelector(".fm-navbtn");
    if (navBtn) {
      navBtn.classList.toggle("on", compact && this.drawerOpen);
      navBtn.setAttribute("aria-expanded", (compact && this.drawerOpen) ? "true" : "false");
    }
    this._syncViewButtons();
    this._truncateCrumbs();
    this._updatePanes();
  };

  /* compact = narrow viewport where the tree becomes a slide-in drawer */
  FileManager.prototype._isCompact = function () {
    return window.matchMedia("(max-width: 700px)").matches;
  };

  FileManager.prototype._syncViewButtons = function () {
    var self = this;
    this.el.querySelectorAll("[data-view]").forEach(function (b) {
      b.classList.toggle("on", b.getAttribute("data-view") === self.view);
    });
  };

  FileManager.prototype.setView = function (v) {
    this.view = v;
    try { localStorage.setItem("fmnet-view-" + this.id, v); } catch (e) {}
    var lbl = this.el.querySelector("[data-role=viewLabel]");
    if (lbl) lbl.textContent = this.t(VIEW_KEY[v] || "vDetails");
    this._syncViewButtons();
    this.renderList();
  };

  /* toggle a View-tab switch: nav | preview | details | ext | hidden */
  FileManager.prototype._toggle = function (name) {
    if (name === "nav") {
      if (this._isCompact()) this.drawerOpen = !this.drawerOpen;
      else this.showTree = !this.showTree;
    } else if (name === "preview") {
      this.showPreview = !this.showPreview;
      if (this.showPreview) this.showDetails = false;
    } else if (name === "details") {
      this.showDetails = !this.showDetails;
      if (this.showDetails) this.showPreview = false;
    } else if (name === "ext") this.showExtensions = !this.showExtensions;
    else if (name === "hidden") this.showHidden = !this.showHidden;
    this._applyUIState();
    this._saveUI();
    if (name === "ext" || name === "hidden") this.renderList();
  };

  /* --------------------------------------------------------------- panes */
  FileManager.prototype._updatePanes = function () {
    var pane = this.$.pane;
    if (!pane || pane.hidden) return;
    var sel = this._selectedEntries();
    var head = this.showDetails ? this.t("detailsPane") : this.t("previewPane");
    var h = '<div class="fm-pane-head">' + esc(head) + '</div><div class="fm-pane-body">';
    if (!sel.length) {
      h += '<div class="fm-pane-empty">' + esc(this.t("paneNoSelection")) + '</div>';
    } else {
      var e = sel[0];
      var ext = (e.extension || "").toLowerCase().replace(/^\./, "");
      if (this.showPreview) {
        if (!e.isDirectory && IMAGE_EXTS.indexOf(ext) >= 0 && this.config.allowDownload !== false) {
          h += '<div class="fm-pane-thumb"><img src="' + this.api + '/thumb?instance=' + encodeURIComponent(this.id) +
               '&path=' + encodeURIComponent(e.path) + '" alt=""></div>';
        } else {
          h += '<div class="fm-pane-thumb">' + self._iconHtml(e) + '</div>';
        }
      }
      h += '<dl class="fm-pane-meta">' +
        '<dt>' + esc(this.t("name")) + '</dt><dd>' + esc(this._displayName(e)) + '</dd>' +
        '<dt>' + esc(this.t("type")) + '</dt><dd>' + esc(fmtType(e, this.t.bind(this))) + '</dd>' +
        (e.isDirectory ? '' : '<dt>' + esc(this.t("size")) + '</dt><dd>' + fmtSize(e.size) + '</dd>') +
        '<dt>' + esc(this.t("dateModified")) + '</dt><dd>' + fmtDate(e.lastModified) + '</dd>' +
        '<dt>' + esc(this.t("location")) + '</dt><dd class="fm-pane-path">' + esc(parentPath(e.path)) + '</dd>' +
        '</dl>';
      if (sel.length > 1)
        h += '<div class="fm-pane-more">' + esc(this.t("itemsSelected", sel.length)) + '</div>';
    }
    pane.innerHTML = h + '</div>';
  };

  /* name shown in the list — extension hidden unless toggled on */
  FileManager.prototype._displayName = function (e) {
    if (this.showExtensions || e.isDirectory) return e.name;
    var i = e.name.lastIndexOf(".");
    return i > 0 ? e.name.slice(0, i) : e.name;
  };

  /* sort items shared by the View-tab dropdown and the context-menu submenu */
  FileManager.prototype._buildSortItems = function (box) {
    var self = this;
    function add(label, active, fn) {
      var it = document.createElement("div");
      it.className = "fm-ctx-item" + (active ? " active" : "");
      it.textContent = label;
      it.onclick = fn;
      box.appendChild(it);
    }
    [["name", "name"], ["size", "size"], ["type", "type"], ["lastModified", "dateModified"]]
      .forEach(function (k) {
        add(self.t(k[1]), self.sortKey === k[0], function () {
          self.sortKey = k[0]; self.renderList(); self._closeMenus();
        });
      });
    var sep = document.createElement("div");
    sep.className = "fm-ctx-sep";
    box.appendChild(sep);
    add(self.t("sortAsc"), self.sortDir === 1, function () {
      self.sortDir = 1; self.renderList(); self._closeMenus();
    });
    add(self.t("sortDesc"), self.sortDir === -1, function () {
      self.sortDir = -1; self.renderList(); self._closeMenus();
    });
  };

  /* ------------------------------------------------------------- tree pane */
  FileManager.prototype.refreshTree = function () {
    var self = this;
    /* reload the root plus every expanded (and current) node so newly
     * created folders show up everywhere, not just under "/" */
    var paths = ["/", this.path];
    this.expanded.forEach(function (p) { if (paths.indexOf(p) < 0) paths.push(p); });
    return Promise.all(paths.map(function (p) {
      return self.post("tree", { path: p }).then(function (nodes) { self.treeChildren[p] = nodes; });
    })).then(function () { self.renderTree(); })
      .catch(function (err) { self.toast(err.message, "error"); });
  };

  FileManager.prototype._loadChildren = function (path) {
    var self = this;
    return this.post("tree", { path: path }).then(function (nodes) {
      self.treeChildren[path] = nodes;
      self.renderTree();
    });
  };

  FileManager.prototype._toggleNode = function (path) {
    if (this.expanded.has(path)) this.expanded.delete(path);
    else { this.expanded.add(path); if (!this.treeChildren[path]) this._loadChildren(path); }
    this.renderTree();
  };

  FileManager.prototype.renderTree = function () {
    var self = this;
    function nodeHtml(path, name, level) {
      var kids = self.treeChildren[path];
      var expanded = self.expanded.has(path);
      var twist = expanded ? "▾" : "▸";
      var sel = self.path === path ? " selected" : "";
      var h = '<div class="fm-tree-node">' +
        '<div class="fm-tree-row' + sel + '" data-path="' + esc(path) + '" data-name="' + esc(name) +
        '" style="padding-left:' + (level * 12 + 6) + 'px">' +
        '<span class="fm-tree-twist" data-twist="' + esc(path) + '">' + twist + '</span>' +
        icon(path === "/" ? "open" : "folder") + '<span>' + esc(name) + '</span></div>';
      if (expanded && kids) {
        h += '<div class="fm-tree-children">';
        kids.forEach(function (n) { h += nodeHtml(n.path, n.name, level + 1); });
        h += '</div>';
      }
      return h + '</div>';
    }
    this.$.tree.innerHTML = nodeHtml("/", this.rootName || this.t("home"), 0);
  };

  /* --------------------------------------------------------- navigation */
  FileManager.prototype.navigate = function (path) {
    var self = this;
    this.searchMode = false;
    this.path = path;
    this.selection.clear();
    /* opening another folder always starts at the top (the window renderer
     * would otherwise keep the previous scroll offset) */
    if (this.$.listwrap) this.$.listwrap.scrollTop = 0;
    this._ensureTreeExpandedTo(path);
    this.renderBreadcrumb();
    this.renderTree();
    return this.post("list", { path: path }).then(function (data) {
      self.entries = data.entries;
      self._applyPermissions(data.permissions);
      self.renderList();
      self._syncTreeNode(path);
    }).catch(function (err) { self.toast(err.message, "error"); });
  };

  FileManager.prototype._ensureTreeExpandedTo = function (path) {
    var parts = path.split("/").filter(Boolean), cur = "";
    for (var i = 0; i < parts.length; i++) {
      cur += "/" + parts[i];
      this.expanded.add(cur);
      if (!this.treeChildren[cur]) this._loadChildren(cur);
    }
  };
  FileManager.prototype._syncTreeNode = function (path) {
    if (!this.treeChildren[path]) this._loadChildren(path);
    this.renderTree();
  };

  FileManager.prototype.renderBreadcrumb = function () {
    var self = this, parts = this.path.split("/").filter(Boolean);
    var h = '<span class="fm-crumb' + (parts.length ? '' : ' current') + '" data-path="/">' +
            esc(this.rootName || this.t("home")) + '</span>' +
            '<span class="fm-crumb-sep fm-crumb-msep" hidden>›</span>' +
            '<span class="fm-crumb-more" hidden title="…">…</span>';
    var cur = "";
    parts.forEach(function (p, i) {
      cur += "/" + p;
      h += '<span class="fm-crumb-sep">›</span>' +
           '<span class="fm-crumb' + (i === parts.length - 1 ? ' current' : '') +
           '" data-path="' + esc(cur) + '">' + esc(p) + '</span>';
    });
    this.$.crumbs.innerHTML = h;
    this._hiddenCrumbs = [];
    this.$.crumbs.onclick = function (e) {
      var more = e.target.closest(".fm-crumb-more");
      if (more) { self._showCrumbsMenu(more); return; }
      var c = e.target.closest("[data-path]");
      if (c) self.navigate(c.getAttribute("data-path"));
    };
    /* the "up one level" button is meaningless at the root */
    var upBtn = this.el.querySelector('[data-act="up"]');
    if (upBtn) upBtn.disabled = parts.length === 0;
    this._truncateCrumbs();
  };

  /* collapse long breadcrumbs like Explorer: root › … › tail */
  FileManager.prototype._truncateCrumbs = function () {
    var crumbs = this.$.crumbs;
    if (!crumbs) return;
    var more = crumbs.querySelector(".fm-crumb-more");
    var msep = crumbs.querySelector(".fm-crumb-msep");
    var items = Array.prototype.slice.call(crumbs.querySelectorAll(".fm-crumb"));
    var seps = Array.prototype.slice.call(crumbs.querySelectorAll(".fm-crumb-sep:not(.fm-crumb-msep)"));
    items.forEach(function (n) { n.hidden = false; });
    seps.forEach(function (n) { n.hidden = false; });
    if (more) more.hidden = true;
    if (msep) msep.hidden = true;
    this._hiddenCrumbs = [];
    if (items.length < 3 || crumbs.scrollWidth <= crumbs.clientWidth + 1) return;
    for (var i = 1; i < items.length - 1; i++) {
      items[i].hidden = true;
      if (seps[i - 1]) seps[i - 1].hidden = true;
      this._hiddenCrumbs.push({
        path: items[i].getAttribute("data-path"),
        label: items[i].textContent
      });
      if (more) { more.hidden = false; if (msep) msep.hidden = false; }
      if (crumbs.scrollWidth <= crumbs.clientWidth + 1) break;
    }
  };

  /* ------------------------------------------------------------- list pane */
  FileManager.prototype._visibleEntries = function () {
    var hidden = (this.config.hiddenPatterns || "").split(";").filter(Boolean);
    var self = this;
    var e = this.entries.filter(function (en) {
      return !hidden.some(function (pat) {
        var rx = new RegExp("^" + pat.trim().replace(/[.+^${}()|[\]\\]/g, "\\$&")
          .replace(/\*/g, ".*").replace(/\?/g, ".") + "$", "i");
        return rx.test(en.name);
      });
    });
    if (!this.showHidden)
      e = e.filter(function (en) { return en.name.charAt(0) !== "."; });
    e.sort(function (a, b) {
      if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;
      var k = self.sortKey, va, vb;
      if (k === "size") { va = a.size; vb = b.size; }
      else if (k === "lastModified") { va = a.lastModified; vb = b.lastModified; }
      else if (k === "type") { va = fmtType(a, self.t.bind(self)); vb = fmtType(b, self.t.bind(self)); }
      else { va = a.name.toLowerCase(); vb = b.name.toLowerCase(); }
      return (va < vb ? -1 : va > vb ? 1 : 0) * self.sortDir;
    });
    return e;
  };

  /* -------------------------------------------------- list rendering (virtual)
   * A folder with tens of thousands of entries used to create one DOM node per
   * entry and froze the browser. Above VIRTUAL_MIN entries only the rows in
   * (and just around) the viewport are rendered; spacer rows for the details
   * table and a translated window for the grids reproduce the full scroll
   * height, so the scrollbar behaves exactly as if everything were there. */
  var VIRTUAL_MIN = 300;
  var VIRTUAL_OVERSCAN = 4;

  /* track sizes mirror the CSS grid templates of the same name */
  var GRID_METRICS = {
    ExtraLargeIcons: { w: 120, h: 130, gx: 4, gy: 4 },
    LargeIcons: { w: 96, h: 104, gx: 4, gy: 4 },
    MediumIcons: { w: 76, h: 86, gx: 4, gy: 4 },
    SmallIcons: { w: 260, h: 26, gx: 4, gy: 4 },
    Tiles: { w: 240, h: 48, gx: 4, gy: 4 },
    List: { w: 230, h: 26, gx: 6, gy: 2 }
  };

  FileManager.prototype.renderList = function () {
    var self = this, entries = this._visibleEntries();
    this._viewEntries = entries;
    this._win = null;
    var wrap = this.$.list;
    /* rebuilding the DOM clamps the scroll container back to 0, which would
     * jump to the top on every selection change — keep the position and redo
     * the window for it (navigate() explicitly resets to 0 first) */
    var prevTop = this.$.listwrap ? this.$.listwrap.scrollTop : 0;
    var banner = "";
    if (this.searchMode) {
      var term = this._searchTerm || "";
      banner = '<div class="fm-searchbanner">' + this.t("searchResults", esc(term), entries.length) +
        '<button data-act="clearSearch">' + this.t("clear") + '</button></div>';
    }
    wrap.innerHTML = banner + '<div class="fm-listinner"></div>';
    this._body = wrap.querySelector(".fm-listinner");
    if (this.searchMode) {
      wrap.querySelector('[data-act="clearSearch"]').onclick = function () { self.navigate(self.path); };
    }
    this._renderWindow();
    if (prevTop > 0 && this.$.listwrap) {
      this.$.listwrap.scrollTop = prevTop;
      this._win = null;
      this._renderWindow();
    }
    this._updatePanes();
    this.renderStatus();
  };

  /* Renders either the whole list (small folders) or the current window. */
  FileManager.prototype._renderWindow = function () {
    var entries = this._viewEntries || [];
    var view = this.view, body = this._body;
    if (!body) return;

    if (entries.length <= VIRTUAL_MIN) {
      this._virtual = false;
      body.innerHTML = this._listHtml(entries, view);
      return;
    }
    this._virtual = true;

    var wrap = this.$.listwrap;
    var padTop = 8, viewport = wrap.clientHeight || 400, top = wrap.scrollTop;

    if (view === "Details") {
      var rowH = this._detailsRowH || 27;
      var first = Math.max(0, Math.floor((top - padTop) / rowH) - VIRTUAL_OVERSCAN);
      var last = Math.min(entries.length, Math.ceil((top + viewport - padTop) / rowH) + VIRTUAL_OVERSCAN);
      if (this._win && this._win.first === first && this._win.last === last) return;
      this._win = { first: first, last: last };

      body.innerHTML = this._tableHtml(entries, view, first, last, rowH);
      /* the real row height depends on theme / media query / zoom — measure it
       * once and redo the window when the guess was off, otherwise the scroll
       * height (and therefore the scrollbar) drifts */
      var probe = body.querySelector("tbody tr[data-path]");
      if (probe) {
        var h = probe.getBoundingClientRect().height;
        if (h > 8 && Math.abs(h - rowH) > 1) {
          this._detailsRowH = h;
          this._win = null;
          this._renderWindow();
        }
      }
      return;
    }

    var m = GRID_METRICS[view] || GRID_METRICS.MediumIcons;
    var avail = (this.$.list.clientWidth || 600) - 16;
    var cols = Math.max(1, Math.floor((avail + m.gx) / (m.w + m.gx)));
    var totalRows = Math.ceil(entries.length / cols);
    var rowH2 = m.h + m.gy;
    var firstRow = Math.max(0, Math.floor((top - padTop) / rowH2) - VIRTUAL_OVERSCAN);
    var lastRow = Math.min(totalRows, Math.ceil((top + viewport - padTop) / rowH2) + VIRTUAL_OVERSCAN);
    if (this._win && this._win.cols === cols &&
        this._win.firstRow === firstRow && this._win.lastRow === lastRow) return;
    this._win = { cols: cols, firstRow: firstRow, lastRow: lastRow };

    var start = firstRow * cols;
    var end = Math.min(entries.length, lastRow * cols);
    var totalH = totalRows * m.h + Math.max(0, totalRows - 1) * m.gy;
    body.innerHTML = '<div class="fm-vbody" style="height:' + totalH + 'px">' +
      '<div class="fm-vwin" style="transform:translateY(' + (firstRow * rowH2) + 'px)">' +
      this._itemsHtml(entries.slice(start, end), view, true) + '</div></div>';
  };

  FileManager.prototype._listHtml = function (entries, view) {
    if (!entries.length)
      return '<div class="fm-empty">' + (this.searchMode ? this.t("noMatch") : this.t("emptyFolder")) + '</div>';
    if (view === "Details") return this._tableHtml(entries, view, 0, entries.length, 0);
    return this._itemsHtml(entries, view, false);
  };

  /* details table; row height 0 = no spacer rows (whole list is rendered) */
  FileManager.prototype._tableHtml = function (entries, view, first, last, rowH) {
    var self = this;
    var th = function (key, label) {
      var arrow = self.sortKey === key ? (self.sortDir > 0 ? " ▲" : " ▼") : "";
      return '<th data-sort="' + key + '">' + label + arrow + '</th>';
    };
    var h = '<table class="fm-table"><thead><tr>' +
      th("name", this.t("name")) + th("size", this.t("size")) +
      th("type", this.t("type")) + th("lastModified", this.t("dateModified")) +
      '</tr></thead><tbody>';
    var spacer = function (rows) {
      if (!rowH || rows <= 0) return "";
      var px = rows * rowH;
      return '<tr class="fm-vspacer" style="height:' + px + 'px">' +
             '<td colspan="4" style="height:' + px + 'px"></td></tr>';
    };
    h += spacer(first);
    for (var i = first; i < last; i++) h += this._rowHtml(entries[i], i);
    h += spacer(entries.length - last);
    return h + '</tbody></table>';
  };

  FileManager.prototype._rowHtml = function (e, index) {
    return '<tr data-path="' + esc(e.path) + '" data-name="' + esc(e.name) + '"' +
      (e.isDirectory ? ' data-dir="1"' : '') + ' draggable="true" class="' +
      (this.selection.has(e.path) ? "selected" : "") + (index % 2 ? " alt" : "") + '">' +
      '<td><div class="fm-namecell">' + this._iconHtml(e) + '<span class="fm-name">' +
      esc(this._displayName(e)) + '</span></div></td>' +
      '<td class="fm-dim">' + (e.isDirectory ? "" : fmtSize(e.size)) + '</td>' +
      '<td class="fm-dim">' + fmtType(e, this.t.bind(this)) + '</td>' +
      '<td class="fm-dim">' + fmtDate(e.lastModified) + '</td></tr>';
  };

  FileManager.prototype._itemsHtml = function (entries, view, virtual) {
    var self = this, cls;
    if (view === "List") cls = "fm-listview";
    else cls = "fm-grid " + ({ ExtraLargeIcons: "view-xlarge", LargeIcons: "view-large",
      MediumIcons: "view-medium", SmallIcons: "view-small", Tiles: "view-tiles" }[view] || "view-medium");
    var h = '<div class="' + cls + (virtual ? " vgrid" : "") + '">';
    entries.forEach(function (e) {
      h += view === "List" ? self._lrowHtml(e) : self._tileHtml(e, view);
    });
    return h + '</div>';
  };

  FileManager.prototype._tileHtml = function (e, view) {
    var sel = this.selection.has(e.path) ? " selected" : "";
    var sub = e.isDirectory ? "" : fmtSize(e.size);
    return '<div class="fm-tile' + sel + '" data-path="' + esc(e.path) + '" data-name="' + esc(e.name) + '"' +
      (e.isDirectory ? ' data-dir="1"' : '') + ' draggable="true" title="' + esc(e.name) + '">' +
      '<div class="fm-thumb">' + this._thumbHtml(e) + '</div>' +
      (view === "Tiles"
        ? '<div class="fm-tileinfo"><span class="fm-label">' + esc(this._displayName(e)) +
          '</span><span class="fm-sub">' + sub + '</span></div>'
        : '<span class="fm-label">' + esc(this._displayName(e)) + '</span>') +
      '</div>';
  };

  FileManager.prototype._lrowHtml = function (e) {
    return '<div class="fm-lrow' + (this.selection.has(e.path) ? " selected" : "") +
      '" data-path="' + esc(e.path) + '" data-name="' + esc(e.name) + '"' +
      (e.isDirectory ? ' data-dir="1"' : '') +
      ' draggable="true" title="' + esc(e.name) + '">' +
      this._iconHtml(e) + '<span>' + esc(this._displayName(e)) + '</span></div>';
  };

  /* Icon markup for one entry. Material Icon Theme SVGs are vectors, so they
   * stay sharp at every zoom level; the built-in SVG sits underneath and shows
   * through when there is no mapping or the request fails. */
  FileManager.prototype._iconHtml = function (e) {
    var fallback = icon(fileIcon(e));
    if (e.isDirectory) return fallback;
    var ext = (e.extension || "").toLowerCase();
    if (!ext) return fallback;

    var src = null;
    if (this.config.useSystemIcons === true) {
      /* opt-in: the icon Windows associates with the extension */
      src = this.api + "/icon?instance=" + encodeURIComponent(this.id) +
            "&ext=" + encodeURIComponent(ext) + "&size=32";
    } else if (EXT_ICONS[ext]) {
      src = FMNET_ICON_BASE + "/" + EXT_ICONS[ext] + ".svg";
    }
    if (!src) return fallback;
    return '<span class="fm-ic">' + fallback +
      '<img src="' + src + '" alt="" draggable="false" onerror="this.remove()"></span>';
  };

  FileManager.prototype._thumbHtml = function (e) {
    if (e.isDirectory) return icon("folder");
    var ext = (e.extension || "").toLowerCase();
    if (IMAGE_EXTS.indexOf(ext) >= 0 && this.config.allowDownload !== false) {
      return '<img loading="lazy" draggable="false" src="' + this.api + '/thumb?instance=' + encodeURIComponent(this.id) +
             '&path=' + encodeURIComponent(e.path) + '" alt="">';
    }
    return this._iconHtml(e);
  };

  /* Volume statistics are no longer shown in the status bar, so nothing calls
   * this implicitly — every folder change used to cost a second request whose
   * only consumer was the "x free of y" label. Kept for hosts that want it. */
  FileManager.prototype._refreshStats = function (path, deep) {
    var self = this;
    return this.post("stats", { path: path, deep: !!deep })
      .then(function (s) { self._stats = s; self.renderStatus(); });
  };

  /* Applies the permissions the server sent for the current folder. Folders
   * can differ (one read-only department folder next to a writable one), so
   * this runs after every listing. Right-click menus read this.config when they
   * are built, so they follow automatically; ribbon buttons are toggled here. */
  FileManager.prototype._applyPermissions = function (perm) {
    if (!perm) return;
    var base = this._baseConfig || {};
    var next = {};
    for (var k in base) if (Object.prototype.hasOwnProperty.call(base, k)) next[k] = base[k];
    for (var p in perm) {
      if (!Object.prototype.hasOwnProperty.call(perm, p)) continue;
      next[p] = base[p] !== false && perm[p] !== false;
    }
    this.config = next;
    this._applyPermissionUi();
  };

  FileManager.prototype._applyPermissionUi = function () {
    var self = this, root = this.el, c = this.config || {};
    var map = {
      download: c.allowDownload, upload: c.allowUpload, uploadFolder: c.allowUpload,
      delete: c.allowDelete, rename: c.allowRename,
      newFolder: c.allowCreateFolder, newFile: c.allowCreateFile,
      copy: c.allowCopyMove, cut: c.allowCopyMove, paste: c.allowCopyMove,
      zip: c.allowArchive, extract: c.allowArchive
    };
    /* clear what a previous folder denied; nothing else disables these */
    root.querySelectorAll(".fm-btn[data-perm-denied]").forEach(function (b) {
      b.removeAttribute("data-perm-denied");
      b.disabled = false;
      b.removeAttribute("title");
    });
    Object.keys(map).forEach(function (act) {
      if (map[act] !== false) return;
      root.querySelectorAll('.fm-btn[data-act="' + act + '"]').forEach(function (b) {
        b.disabled = true;
        b.setAttribute("data-perm-denied", "1");
        b.title = self.t("noPermission");
      });
    });
  };

  FileManager.prototype.renderStatus = function () {
    var files = 0, folders = 0, selSize = 0, self = this;
    this.entries.forEach(function (e) {
      if (e.isDirectory) folders++; else files++;
      if (self.selection.has(e.path) && !e.isDirectory) selSize += e.size;
    });
    var sel = this.selection.size;
    var h = '<span>' + this.t("statusSummary", folders, files) + '</span>';
    if (sel) h += '<span>' + this.t("itemsSelected", sel) +
                  (selSize ? ' (' + fmtSize(selSize) + ')' : '') + '</span>';
    h += '<span class="fm-spacer"></span>';
    /* view switcher sits at the right end of the status bar */
    h += '<div class="fm-view-select" tabindex="0" title="' + esc(this.t("view")) + '">' +
         icon("view") + '<span data-role="viewLabel">' + this.t(VIEW_KEY[this.view] || "vDetails") + '</span>' +
         icon("caret") + '</div>';
    this.$.statusbar.innerHTML = h;
  };

  /* ------------------------------------------------------------ selection */
  FileManager.prototype._selectedEntries = function () {
    var sel = this.selection;
    var list = this.searchMode ? (this._searchResults || []) : this.entries;
    return list.filter(function (e) { return sel.has(e.path); });
  };
  FileManager.prototype._clearSelection = function () { this.selection.clear(); this.renderList(); this.renderStatus(); };

  /* true when the given folder can receive the items being dragged */
  FileManager.prototype._canDropTo = function (dest, single) {
    if (!dest) return false;
    if (this.multiRoot && dest === "/") return false;       // mounts cannot be replaced
    if (dest === this.path) return false;                  // already here
    var paths = single ? [single] : (this._dragPaths || []);
    if (!paths.length) return false;
    for (var i = 0; i < paths.length; i++) {
      var p = paths[i];
      if (dest === p) return false;                        // onto itself
      if (dest.indexOf(p + "/") === 0) return false;       // into its own subtree
      if (p.indexOf(dest + "/") === 0) return false;       // already inside the target
    }
    return true;
  };

  /* moves the items currently being dragged into `dest` */
  FileManager.prototype._moveTo = function (dest) {
    var self = this, paths = this._dragPaths || [];
    var usable = paths.filter(function (p) { return self._canDropTo(dest, p); });
    this._dragPaths = null;
    if (!usable.length) return Promise.resolve();

    function attempt(overwrite, rename) {
      return self.post("move", {
        paths: usable, destination: dest,
        overwrite: !!overwrite, renameOnConflict: !!rename
      }).then(function () {
        self.selection.clear();
        self.refresh();
        self.refreshTree();
        var name = dest.split("/").filter(Boolean).pop() || self.rootName;
        self.toast(self.t("movedTo", usable.length, name), "ok",
          rename ? self.t("autoRenamed") : null);
      }).catch(function (err) {
        if (err.conflicts && self._conflictDialog) {
          return self._conflictDialog(err.conflicts, dest).then(function (choice) {
            if (choice === "overwrite") return attempt(true, false);
            if (choice === "rename") return attempt(false, true);
          });
        }
        self.toast(self.t("actionFailed"), "error", err.message);
      });
    }
    return attempt(false, false);
  };

  FileManager.prototype._selectAll = function () {
    var self = this; this._visibleEntries().forEach(function (e) { self.selection.add(e.path); });
    this.renderList(); this.renderStatus();
  };
  FileManager.prototype._selectNone = function () {
    this.selection.clear(); this.renderList(); this.renderStatus();
  };
  FileManager.prototype._invertSelection = function () {
    var self = this;
    this._visibleEntries().forEach(function (e) {
      if (self.selection.has(e.path)) self.selection.delete(e.path);
      else self.selection.add(e.path);
    });
    this.renderList(); this.renderStatus();
  };
  FileManager.prototype._selectClick = function (path, e, row) {
    if (e.ctrlKey || e.metaKey) {
      if (this.selection.has(path)) this.selection.delete(path); else this.selection.add(path);
    } else if (e.shiftKey && this.selection.size) {
      var entries = this._visibleEntries().map(function (x) { return x.path; });
      var last = null;
      for (var i = entries.length - 1; i >= 0; i--) if (this.selection.has(entries[i])) { last = entries[i]; break; }
      var a = entries.indexOf(last), b = entries.indexOf(path);
      var lo = Math.min(a, b), hi = Math.max(a, b);
      for (var j = lo; j <= hi; j++) this.selection.add(entries[j]);
    } else {
      this.selection = new Set([path]);
    }
    this.renderList(); this.renderStatus();
  };
  FileManager.prototype._open = function (path) {
    var e = this.entries.find(function (x) { return x.path === path; });
    if (!e) return;
    if (e.isDirectory) this.navigate(path);
    else this._preview(e);
  };

  /* --------------------------------------------------------------- export */
  window.FileManagerNet = {
    create: function (el) { return new FileManager(el); },
    proto: FileManager.prototype,
    icon: icon,
    icons: ICONS,
    esc: esc,
    VIEW_KEY: VIEW_KEY
  };

  function boot() {
    var els = Array.prototype.filter.call(
      document.querySelectorAll("[data-fmnet]"), function (el) { return !el.fmnet; });
    if (!els.length) return;
    /* resolve each instance's language first, then load only those
     * dictionaries (plus the "en" fallback) before constructing */
    var need = { en: 1 }, tmp = { wrap: null, config: null };
    els.forEach(function (el) {
      tmp.wrap = el;
      try { tmp.config = JSON.parse(el.getAttribute("data-config") || "{}"); }
      catch (e) { tmp.config = {}; }
      need[FileManager.prototype._resolveLocale.call(tmp)] = 1;
    });
    Promise.all(Object.keys(need).map(loadLocale)).then(function () {
      els.forEach(function (el) { el.fmnet = window.FileManagerNet.create(el); });
    });
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
  else boot();
})();
