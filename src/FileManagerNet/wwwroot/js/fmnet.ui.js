/* FileManagerNet UI behaviors: ribbon actions, dialogs, context menu,
 * upload with progress, preview, toasts. Extends the core prototype. */
(function () {
  "use strict";
  var core = window.FileManagerNet;
  var P = core && core.proto;
  if (!P) return;
  if (core.uiReady) return;   /* emitted once per instance — apply the mixin once */
  /* shared helpers from the core module */
  var esc = window.FileManagerNet.esc;
  var VIEW_KEY = window.FileManagerNet.VIEW_KEY;
  /* context menu uses the shared colored icon set from the core module */
  function iconOf(name) {
    var icons = {
      folder: "folder", del: "del", refresh: "refresh", download: "download", upload: "upload",
      uploadFolder: "uploadFolder",
      cut: "cut", copy: "copy", paste: "paste", rename: "rename", zip: "zip", extract: "extract",
      newFolder: "newFolder", newFile: "newFile", search: "search", file: "file", open: "open",
      preview: "preview", view: "view", sort: "sort", selectAll: "selectAll",
      selectNone: "selectNone", invert: "invert"
    };
    return window.FileManagerNet.icon(icons[name] || "file");
  }

  /* -------------------------------------------------------------- actions */
  P.act = function (act, e) {
    var self = this;
    try {
    switch (act) {
      case "refresh": this.refresh(); break;
      case "up": {
        var seg = this.path.split("/").filter(Boolean);
        seg.pop();
        this.navigate(seg.length ? "/" + seg.join("/") : "/");
        break;
      }
      case "home": this.navigate("/"); break;
      case "newFolder": this._newDialog("folder"); break;
      case "newFile": this._newDialog("file"); break;
      case "upload":
        if (this._warnRootReadOnly()) break;
        this.$.fileInput.click(); break;
      case "uploadFolder":
        if (this._warnRootReadOnly()) break;
        this.$.folderInput.click(); break;
      case "download": this._download(); break;
      case "cut": this._clipboard("cut"); break;
      case "copy": this._clipboard("copy"); break;
      case "paste": this._paste(); break;
      case "rename": this._rename(); break;
      case "delete": this._delete(); break;
      case "zip": this._zip(); break;
      case "extract": this._extract(); break;
      case "open": {
        var so = this._selectedEntries();
        if (!so.length) { this.toast(this.t("selectToOpen"), "info"); break; }
        this._open(so[0].path);
        break;
      }
      case "preview": {
        var sp = this._selectedEntries().filter(function (e) { return !e.isDirectory; });
        if (!sp.length) { this.toast(this.t("selectToPreview"), "info"); break; }
        this._preview(sp[0]);
        break;
      }
      case "selectAll": this._selectAll(); break;
      case "selectNone": this._selectNone(); break;
      case "invertSelection": this._invertSelection(); break;
      /* View tab */
      case "toggleNav": this._toggle("nav"); break;
      case "togglePreview": this._toggle("preview"); break;
      case "toggleDetails": this._toggle("details"); break;
      case "toggleExtensions": this._toggle("ext"); break;
      case "toggleHidden": this._toggle("hidden"); break;
      case "sortBy": {
        var anchor = (e && e.target.closest("[data-act]")) || this.el.querySelector(".fm-pathbar");
        this._showSortMenu(anchor);
        break;
      }
      case "view": break;
    }
    } catch (err) {
      /* never fail silently: a broken handler used to look like "nothing happens" */
      this.toast(this.t("actionFailed"), "error", err && err.message ? err.message : String(err));
    }
  };

  P.refresh = function () {
    if (this.searchMode) { this.navigate(this.path); return; }
    var self = this;
    /* an explicit refresh must show the disk as it is now, not a listing that
     * another user's request happened to produce a moment ago */
    this.post("list", { path: this.path, noCache: true }).then(function (d) {
      self.entries = d.entries;
      self._applyPermissions(d.permissions);
      self.renderList();
    }).catch(function (err) { self.toast(err.message, "error"); });
    this.refreshTree();
  };

  /* With several root folders the virtual root only lists those folders, so
   * creating, pasting or uploading there makes no sense — tell the user
   * instead of letting the server reject the call. */
  P._atVirtualRoot = function () {
    return !!this.multiRoot && this.path === "/";
  };

  P._warnRootReadOnly = function () {
    if (!this._atVirtualRoot()) return false;
    this.toast(this.t("chooseFolderFirst"), "info");
    return true;
  };

  P._newDialog = function (kind) {
    if (this._warnRootReadOnly()) return;
    var self = this;
    this._prompt(kind === "folder" ? this.t("newFolder") : this.t("newFile"),
      kind === "folder" ? this.t("folderNameLbl") : this.t("fileNameLbl"),
      kind === "folder" ? this.t("newFolder") : this.t("newFileDefault")).then(function (name) {
        if (!name) return;
        var target = (self.searchMode ? self.path : self.path);
        var path = target === "/" ? "/" + name : target + "/" + name;
        return self.post(kind === "folder" ? "createFolder" : "createFile", { path: path })
          .then(function () { self.refresh(); self.toast(self.t("created", name), "ok"); })
          .catch(function (err) { self.toast(err.message, "error"); });
      });
  };

  P._rename = function () {
    if (this.config.allowRename === false) return;
    var sel = this._selectedEntries();
    if (sel.length !== 1) { this.toast(this.t("selectOneRename"), "info"); return; }
    var self = this, entry = sel[0];
    this._prompt(this.t("renameTitle"), this.t("fileNameLbl"), entry.name).then(function (name) {
      if (!name || name === entry.name) return;
      self.post("rename", { path: entry.path, newName: name })
        .then(function () { self.selection.clear(); self.refresh(); self.toast(self.t("renamedTo", name), "ok"); })
        .catch(function (err) { self.toast(err.message, "error"); });
    });
  };

  P._delete = function () {
    if (this.config.allowDelete === false) return;
    var sel = this._selectedEntries();
    if (!sel.length) return;
    var self = this;
    var label = sel.length === 1 ? '“' + sel[0].name + '”' : String(sel.length);
    this._confirm(this.t("deleteTitle"), this.t("deleteConfirm", label))
      .then(function (yes) {
        if (!yes) return;
        self.post("delete", { paths: sel.map(function (e) { return e.path; }) })
          .then(function () { self.selection.clear(); self.refresh(); self.toast(self.t("deleted"), "ok"); })
          .catch(function (err) { self.toast(err.message, "error"); });
      });
  };

  P._clipboard = function (mode) {
    if (this.config.allowCopyMove === false) return;
    var sel = this._selectedEntries();
    if (!sel.length) return;
    this.clipboard = { mode: mode, paths: sel.map(function (e) { return e.path; }) };
    this.toast(this.t(mode === "cut" ? "cutItems" : "copiedItems", sel.length), "info");
  };

  P._paste = function () {
    if (this.config.allowCopyMove === false || !this.clipboard) return;
    if (this._warnRootReadOnly()) return;
    var self = this, cb = this.clipboard, dest = this.path, paths = cb.paths.slice();
    /* "cut" is the clipboard mode, the command is "move" */
    var command = cb.mode === "cut" ? "move" : "copy";

    function attempt(overwrite, rename) {
      return self.post(command, {
        paths: paths, destination: dest,
        overwrite: !!overwrite, renameOnConflict: !!rename
      }).then(function () {
        if (cb.mode === "cut") self.clipboard = null;
        self.selection.clear();
        self.refresh();
        self.refreshTree();
        self.toast(self.t("pasted"), "ok", rename ? self.t("autoRenamed") : null);
      }).catch(function (err) {
        if (err.conflicts) {
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

  P._zip = function () {
    if (this.config.allowArchive === false) return;
    var sel = this._selectedEntries();
    if (!sel.length) { this.toast(this.t("selectToCompress"), "info"); return; }
    if (this._archiveJob) { this.toast(this.t("archiveBusy"), "info"); return; }
    var self = this;
    this._archiveDialog().then(function (res) {
      if (!res) return;
      var base = res.base, ext = res.ext;
      // keep a format the user typed explicitly (e.g. "photos.tar.gz")
      var lower = base.toLowerCase();
      var known = ARCHIVE_SUFFIXES.filter(function (s) { return lower.length > s.length && lower.slice(-s.length) === s; })[0];
      if (known) { ext = known; base = base.slice(0, base.length - known.length); }
      var name = base + ext;
      var zipPath = self.path === "/" ? "/" + name : self.path + "/" + name;
      self.postJob("zip", { path: zipPath, paths: sel.map(function (e) { return e.path; }) })
        .then(function (jobId) { self._runArchiveJob("zip", jobId, name); })
        .catch(function (err) { self.toast(err.message, "error"); });
    });
  };

  P._extract = function () {
    if (this.config.allowArchive === false) return;
    var archives = this._selectedEntries().filter(function (e) {
      return !e.isDirectory && isArchiveName(e.name);
    });
    if (!archives.length) { this.toast(this.t("selectOneArchive"), "info"); return; }
    if (this._archiveJob) { this.toast(this.t("archiveBusy"), "info"); return; }
    var self = this;
    var msg = archives.length === 1
      ? this.t("extractConfirm", archives[0].name)
      : this.t("extractConfirmMany", archives.length);
    this._confirm(this.t("extractTitle"), msg)
      .then(function (yes) { if (yes) self._extractNext(archives, 0); })
      .catch(function (err) { self.toast(self.t("actionFailed"), "error", err.message); });
  };

  /* extracts the selected archives one after another ("Extract all") */
  P._extractNext = function (list, i) {
    if (i >= list.length) return;
    var self = this;
    this._startExtract(list[i], false, function () { self._extractNext(list, i + 1); });
  };

  /* starts one extraction job; on a name clash it asks for confirmation and
   * re-runs with overwrite instead of failing outright */
  P._startExtract = function (entry, overwrite, next) {
    var self = this;
    this.postJob("extract", { path: entry.path, destination: this.path, overwrite: overwrite })
      .then(function (jobId) {
        self._runArchiveJob("extract", jobId, entry.name, function (err) {
          if (!overwrite && /already exists/i.test(err)) {
            self._confirm(self.t("overwriteTitle"), err + " " + self.t("overwriteFiles"))
              .then(function (again) {
                if (again) self._startExtract(entry, true, next);
                else if (next) next();
              });
            return true;
          }
          return false;
        }, next);
      })
      .catch(function (err) {
        self.toast(self.t("actionFailed"), "error", err.message);
        if (next) next();
      });
  };

  P._download = function () {
    if (this.config.allowDownload === false) return;
    var sel = this._selectedEntries();
    if (!sel.length) { this.toast(this.t("selectToDownload"), "info"); return; }
    if (sel.length === 1 && !sel[0].isDirectory) {
      var url = this.api + "/download?instance=" + encodeURIComponent(this.id) +
                "&path=" + encodeURIComponent(sel[0].path);
      this._triggerDownload(url);
    } else {
      var url2 = this.api + "/download?instance=" + encodeURIComponent(this.id) +
                 "&paths=" + encodeURIComponent(sel.map(function (e) { return e.path; }).join("|"));
      this._triggerDownload(url2);
    }
  };
  P._triggerDownload = function (url) {
    var a = document.createElement("a");
    a.href = url; a.download = ""; a.style.display = "none";
    document.body.appendChild(a);
    a.click();
    /* removing the anchor in the same tick cancels the download in Chromium */
    setTimeout(function () { a.remove(); }, 1000);
  };

  /* ------------------------------------------------------- archive helpers */
  /* suffixes SharpCompress can extract */
  var ARCHIVE_SUFFIXES = [".zip", ".7z", ".rar", ".tar", ".tar.gz", ".tgz", ".tar.bz2", ".tbz2",
    ".gz", ".bz2", ".xz", ".txz", ".tar.xz", ".zst", ".tar.zst", ".lz", ".lzma",
    ".arj", ".ace", ".arc", ".lzw"];
  /* formats the server can create (RAR is proprietary, 7z needs 7-Zip) */
  var CREATE_FORMATS = [
    [".zip", "ZIP"], [".tar.gz", "TAR.GZ"], [".tar", "TAR"], [".tar.bz2", "TAR.BZ2"]
  ];
  function isArchiveName(name) {
    var n = (name || "").toLowerCase();
    for (var i = 0; i < ARCHIVE_SUFFIXES.length; i++)
      if (n.length > ARCHIVE_SUFFIXES[i].length && n.lastIndexOf(ARCHIVE_SUFFIXES[i]) === n.length - ARCHIVE_SUFFIXES[i].length)
        return true;
    return false;
  }

  /* dialog: archive base name + format */
  P._archiveDialog = function () {
    var self = this;
    return new Promise(function (resolve) {
      var ov = document.createElement("div");
      ov.className = "fm-overlay";
      var opts = CREATE_FORMATS.map(function (f) {
        return '<option value="' + f[0] + '">' + esc(f[1]) + " (" + esc(f[0]) + ")</option>";
      }).join("");
      ov.innerHTML =
        '<div class="fm-dialog"><header>' + esc(self.t("compressTitle")) + '</header>' +
        '<div class="fm-dialog-body">' +
        '<p class="fm-dialog-msg">' + esc(self.t("archiveNameLbl")) + '</p>' +
        '<div class="fm-name-row"><input type="text" value="' + esc(self.t("archiveBase")) + '">' +
        '<select class="fm-fmt-select" title="' + esc(self.t("archiveFormat")) + '">' + opts + '</select></div>' +
        '<p class="fm-dialog-note">' + esc(self.t("compressHint")) + '</p>' +
        '</div>' +
        '<footer><button class="fm-btn-secondary">' + esc(self.t("cancel")) + '</button>' +
        '<button class="fm-btn-primary">' + esc(self.t("ok")) + '</button></footer></div>';
      document.body.appendChild(ov);
      var input = ov.querySelector("input");
      var sel = ov.querySelector("select");
      input.focus(); input.select();
      function done(v) { ov.remove(); resolve(v); }
      ov.querySelector(".fm-btn-secondary").onclick = function () { done(null); };
      ov.querySelector(".fm-btn-primary").onclick = function () {
        var base = input.value.trim();
        if (!base) { done(null); return; }
        done({ base: base, ext: sel.value });
      };
      input.addEventListener("keydown", function (e) {
        if (e.key === "Enter") ov.querySelector(".fm-btn-primary").click();
        if (e.key === "Escape") done(null);
      });
    });
  };
  /* ---------------------------------------------------- archive progress */
  /* Progress dialog shared by compress and extract. Returning a controller
   * lets the polling loop refresh it and lets the user send it to background. */
  P._progressDialog = function (title, subtitle) {
    var self = this;
    var ov = document.createElement("div");
    ov.className = "fm-overlay";
    ov.innerHTML =
      '<div class="fm-dialog fm-progress">' +
      '<header><span class="fm-progress-title">' + esc(title) + '</span>' +
      '<span class="fm-progress-pct" data-role="pct"></span></header>' +
      '<div class="fm-dialog-body">' +
      '<div class="fm-progress-track"><div class="fm-progress-bar" data-role="bar"></div></div>' +
      '<div class="fm-progress-meta"><span data-role="count"></span><span data-role="elapsed"></span></div>' +
      '<div class="fm-progress-file" data-role="cur">' + esc(subtitle || "") + '</div>' +
      '</div>' +
      '<footer><button class="fm-btn-secondary" data-role="bg">' + esc(self.t("runInBackground")) + '</button>' +
      '</footer></div>';
    document.body.appendChild(ov);

    var bar = ov.querySelector('[data-role="bar"]');
    var pct = ov.querySelector('[data-role="pct"]');
    var count = ov.querySelector('[data-role="count"]');
    var cur = ov.querySelector('[data-role="cur"]');
    var elapsed = ov.querySelector('[data-role="elapsed"]');
    var started = Date.now();
    var ctl = {
      hidden: false,
      update: function (done, total, current) {
        if (total > 0) {
          var ratio = Math.min(1, done / total);
          bar.classList.remove("indeterminate");
          bar.style.width = (ratio * 100).toFixed(1) + "%";
          pct.textContent = Math.round(ratio * 100) + "%";
          count.textContent = self.t("progressFiles", done, total);
        } else {
          bar.classList.add("indeterminate");
          bar.style.width = "100%";
          pct.textContent = "";
          count.textContent = self.t("progressUnknown", done);
        }
        if (current) cur.textContent = current;
        elapsed.textContent = ((Date.now() - started) / 1000).toFixed(1) + "s";
      },
      close: function () { ov.remove(); }
    };
    ov.querySelector('[data-role="bg"]').onclick = function () {
      ctl.hidden = true;
      ov.remove();
    };
    return ctl;
  };

  /* Polls an archive job and drives the progress dialog to completion. */
  P._runArchiveJob = function (kind, jobId, label, onFail, onDone) {
    var self = this;
    var title = kind === "zip" ? this.t("zipInProgress") : this.t("extractInProgress");
    var dlg = this._progressDialog(title, label);
    var settled = false;
    this._archiveJob = jobId;
    dlg.update(0, -1, "");

    function finish(fn) {
      if (settled) return;
      settled = true;
      self._archiveJob = null;
      fn();
    }

    (function poll() {
      self.getJob(jobId).then(function (job) {
        dlg.update(job.done, job.total, job.current);
        if (job.state === "done") {
          finish(function () {
            if (!dlg.hidden) dlg.close();
            self.refresh();
            self.toast(kind === "zip" ? self.t("zipDone", job.label) : self.t("extractDone", job.label), "ok");
            if (onDone) onDone();
          });
          return;
        }
        if (job.state === "failed") {
          finish(function () {
            var msg = job.error || self.t("archiveFailed");
            if (!dlg.hidden) dlg.close();
            if (onFail && onFail(msg)) return;   /* the handler took over (e.g. overwrite prompt) */
            self.toast(self.t("archiveFailed"), "error", msg);
            if (onDone) onDone();
          });
          return;
        }
        setTimeout(poll, 300);
      }).catch(function (err) {
        finish(function () {
          if (!dlg.hidden) dlg.close();
          self.toast(self.t("archiveFailed"), "error", err.message);
        });
      });
    })();
  };

  /* --------------------------------------------------------------- search */
  P.runSearch = function (term) {
    var self = this;
    term = (term || "").trim();
    if (!term) { this.navigate(this.path); return; }
    this.post("search", { path: "/", search: { pattern: term, recursive: true, maxResults: 500 } })
      .then(function (entries) {
        self.searchMode = true;
        self._searchTerm = term;
        self._searchResults = entries;
        self.entries = entries;
        self.selection.clear();
        self.renderList();
      })
      .catch(function (err) { self.toast(err.message, "error"); });
  };

  /* ---------------------------------------------------------- view menu */
  /* fills a menu container with the 7 layout items (shared by the path-bar
   * view selector dropdown and the context-menu "View" submenu) */
  P._buildViewItems = function (box) {
    var self = this;
    Object.keys(VIEW_KEY).forEach(function (v) {
      var it = document.createElement("div");
      it.className = "fm-ctx-item" + (self.view === v ? " active" : "");
      it.textContent = self.t(VIEW_KEY[v] || "vDetails");
      it.onclick = function () {
        self.setView(v);
        self._closeMenus();
      };
      box.appendChild(it);
    });
  };

  /* ------------------------------------------------------- popup plumbing */
  /* Every popup tracks its element and its document-level dismiss listeners.
   * _closeMenus detaches both, so a listener left over from an earlier menu
   * can never tear down a newer one — that was the "context menu sometimes
   * does not show up" bug. Listeners use the capture phase so they run before
   * the list's own contextmenu handler, which then opens the next menu. */
  P._menuTrack = function (el) {
    this._menus = this._menus || [];
    this._menus.push(el);
    document.body.appendChild(el);
    return el;
  };

  P._menuDismiss = function (menu) {
    var self = this;
    this._menuOff = this._menuOff || [];
    function on(target, type, fn) {
      target.addEventListener(type, fn, true);
      self._menuOff.push(function () { target.removeEventListener(type, fn, true); });
    }
    function inside(node) {
      var list = self._menus || [];
      for (var i = 0; i < list.length; i++) if (list[i].contains(node)) return true;
      return false;
    }
    on(document, "pointerdown", function (e) {
      if (menu.isConnected && !inside(e.target)) self._closeMenus();
    });
    on(document, "contextmenu", function (e) {
      if (menu.isConnected && !inside(e.target)) self._closeMenus();
    });
  };

  P._showViewMenu = function (anchor) {
    this._closeMenus();
    var menu = document.createElement("div");
    menu.className = "fm-dropdown";
    this._menuTrack(menu);
    var r = anchor.getBoundingClientRect();
    this._buildViewItems(menu);
    menu.style.left = Math.max(8, Math.min(r.left, window.innerWidth - menu.offsetWidth - 8)) + "px";
    menu.style.top = (r.bottom + 4) + "px";
    menu.style.position = "fixed";
    this._menuDismiss(menu);
  };

  /* sort dropdown, opened from the View tab */
  P._showSortMenu = function (anchor) {
    this._closeMenus();
    var menu = document.createElement("div");
    menu.className = "fm-dropdown";
    this._menuTrack(menu);
    var r = anchor.getBoundingClientRect();
    this._buildSortItems(menu);
    menu.style.position = "fixed";
    menu.style.left = Math.max(8, Math.min(r.left, window.innerWidth - menu.offsetWidth - 8)) + "px";
    menu.style.top = (r.bottom + 4) + "px";
    this._menuDismiss(menu);
  };

  /* dropdown listing the breadcrumb segments collapsed into "…" */
  P._showCrumbsMenu = function (anchor) {
    var self = this, items = this._hiddenCrumbs || [];
    if (!items.length) return;
    this._closeMenus();
    var menu = document.createElement("div");
    menu.className = "fm-dropdown";
    items.forEach(function (it) {
      var d = document.createElement("div");
      d.className = "fm-ctx-item";
      d.textContent = it.label;
      d.title = it.path;
      d.onclick = function () { self._closeMenus(); self.navigate(it.path); };
      menu.appendChild(d);
    });
    this._menuTrack(menu);
    var r = anchor.getBoundingClientRect();
    menu.style.position = "fixed";
    menu.style.left = Math.max(8, Math.min(r.left, window.innerWidth - menu.offsetWidth - 8)) + "px";
    menu.style.top = (r.bottom + 2) + "px";
    this._menuDismiss(menu);
  };

  /* -------------------------------------------------------- context menu */
  P._showContextMenu = function (x, y, onItem) {
    var self = this, c = this.config;
    this._closeMenus();
    var menu = document.createElement("div");
    menu.className = "fm-ctx";
    var sub = null;
    function hideSub() { if (sub) { sub.remove(); sub = null; } }
    function showSub(anchor, build) {
      hideSub();
      sub = document.createElement("div");
      sub.className = "fm-ctx fm-submenu";
      build(sub);
      self._menuTrack(sub);
      var r = anchor.getBoundingClientRect();
      var left = r.right + 2, top = r.top;
      if (left + sub.offsetWidth > window.innerWidth - 8) left = r.left - sub.offsetWidth - 2;
      sub.style.left = Math.max(8, left) + "px";
      sub.style.top = Math.max(8, Math.min(top, window.innerHeight - sub.offsetHeight - 8)) + "px";
    }
    function item(iconName, label, enabled, fn, danger) {
      var d = document.createElement("div");
      d.className = "fm-ctx-item" + (enabled ? "" : " disabled") + (danger ? " danger" : "");
      d.innerHTML = iconOf(iconName) + "<span>" + label + "</span>";
      if (enabled) {
        d.onclick = function () { self._closeMenus(); fn(); };
        d.onmouseenter = hideSub;
      }
      menu.appendChild(d);
      return d;
    }
    function subItem(iconName, label, build) {
      var d = item(iconName, label, true, function () {});
      d.classList.add("has-sub");
      d.insertAdjacentHTML("beforeend", '<span class="fm-subarrow"></span>');
      d.onmouseenter = function () { showSub(d, build); };
      d.onclick = function (e) { e.stopPropagation(); showSub(d, build); };
      return d;
    }
    function sep(box) {
      var s = document.createElement("div"); s.className = "fm-ctx-sep";
      (box || menu).appendChild(s);
    }
    function viewSub(box) { self._buildViewItems(box); }
    function sortSub(box) { self._buildSortItems(box); }

    if (onItem) {
      item("open", self.t("open"), self.selection.size > 0, function () {
        self._open(self.selection.values().next().value);
      });
      var firstFile = self._selectedEntries().filter(function (e) { return !e.isDirectory; })[0];
      var canPreview = !!firstFile && !self._previewBlocked(firstFile);
      item("preview", self.t("preview"), canPreview, function () { self._preview(firstFile); });
      if (c.allowDownload !== false) item("download", self.t("download"), true, function () { self._download(); });
      sep();
      if (c.allowCopyMove !== false) {
        item("cut", self.t("cut"), true, function () { self.act("cut"); });
        item("copy", self.t("copy"), true, function () { self.act("copy"); });
        sep();
      }
      if (c.allowRename !== false) item("rename", self.t("rename"), self.selection.size === 1, function () { self._rename(); });
      if (c.allowDelete !== false) item("del", self.t("delete"), true, function () { self._delete(); }, true);
      if (c.allowArchive !== false) {
        sep();
        item("zip", self.t("zip"), true, function () { self._zip(); });
        var one = self._selectedEntries();
        item("extract", self.t("extract"), one.length === 1 && !one[0].isDirectory && isArchiveName(one[0].name),
          function () { self._extract(); });
      }
    } else {
      subItem("view", self.t("view"), viewSub);
      subItem("sort", self.t("sortBy"), sortSub);
      sep();
      item("refresh", self.t("refresh"), true, function () { self.refresh(); });
      sep();
      if (c.allowCopyMove !== false && self.clipboard) item("paste", self.t("paste"), true, function () { self._paste(); });
      if (c.allowCreateFolder !== false) item("newFolder", self.t("newFolder"), true, function () { self._newDialog("folder"); });
      if (c.allowCreateFile !== false) item("newFile", self.t("newFile"), true, function () { self._newDialog("file"); });
      if (c.allowUpload !== false) item("upload", self.t("upload"), true, function () { self.$.fileInput.click(); });
      if (c.allowUpload !== false) item("uploadFolder", self.t("uploadFolder"), true, function () { self.$.folderInput.click(); });
      sep();
      item("selectAll", self.t("selectAll"), true, function () { self._selectAll(); });
      item("selectNone", self.t("selectNone"), self.selection.size > 0, function () { self._selectNone(); });
      item("invert", self.t("invertSelection"), true, function () { self._invertSelection(); });
    }
    this._menuTrack(menu);
    var mw = menu.offsetWidth, mh = menu.offsetHeight;
    menu.style.left = Math.max(8, Math.min(x, window.innerWidth - mw - 8)) + "px";
    menu.style.top = Math.max(8, Math.min(y, window.innerHeight - mh - 8)) + "px";
    this._menuDismiss(menu);
  };
  P._closeMenus = function () {
    (this._menuOff || []).forEach(function (off) { off(); });
    this._menuOff = [];
    (this._menus || []).forEach(function (n) { if (n.parentNode) n.remove(); });
    this._menus = [];
  };

  /* -------------------------------------------------------------- dialogs */
  P._prompt = function (title, label, value) {
    var self = this;
    return new Promise(function (resolve) {
      var ov = document.createElement("div");
      ov.className = "fm-overlay";
      ov.innerHTML =
        '<div class="fm-dialog"><header>' + esc(title) + '</header>' +
        '<div class="fm-dialog-body"><p class="fm-dialog-msg">' + esc(label) + '</p>' +
        '<input type="text"></div>' +
        '<footer><button class="fm-btn-secondary">' + esc(self.t("cancel")) + '</button>' +
        '<button class="fm-btn-primary">' + esc(self.t("ok")) + '</button></footer></div>';
      var input = ov.querySelector("input");
      input.value = value || "";
      document.body.appendChild(ov);
      input.focus(); input.select();
      function done(v) { ov.remove(); resolve(v); }
      ov.querySelector(".fm-btn-secondary").onclick = function () { done(null); };
      ov.querySelector(".fm-btn-primary").onclick = function () { done(input.value.trim()); };
      input.addEventListener("keydown", function (e) {
        if (e.key === "Enter") done(input.value.trim());
        if (e.key === "Escape") done(null);
      });
    });
  };

  P._confirm = function (title, message) {
    var self = this;
    return new Promise(function (resolve) {
      var ov = document.createElement("div");
      ov.className = "fm-overlay";
      ov.innerHTML =
        '<div class="fm-dialog"><header>' + esc(title) + '</header>' +
        '<div class="fm-dialog-body"><p class="fm-dialog-msg">' + esc(message) + '</p></div>' +
        '<footer><button class="fm-btn-secondary">' + esc(self.t("cancel")) + '</button>' +
        '<button class="fm-btn-primary">' + esc(self.t("ok")) + '</button></footer></div>';
      document.body.appendChild(ov);
      function done(v) { ov.remove(); resolve(v); }
      ov.querySelector(".fm-btn-secondary").onclick = function () { done(false); };
      ov.querySelector(".fm-btn-primary").onclick = function () { done(true); };
    });
  };

  /* Three-way prompt shown when a paste/drop would overwrite existing items.
   * Resolves with "overwrite", "rename" (keep both) or null (cancel). */
  P._conflictDialog = function (names, dest) {
    var self = this;
    return new Promise(function (resolve) {
      var MAX = 6;
      var list = names.slice(0, MAX).map(function (n) {
        return "<li>" + esc(n) + "</li>";
      }).join("");
      if (names.length > MAX)
        list += '<li class="fm-conflict-more">' + esc(self.t("conflictMore", names.length - MAX)) + "</li>";

      var folder = (dest || "/").split("/").filter(Boolean).pop() || self.rootName;
      var ov = document.createElement("div");
      ov.className = "fm-overlay";
      ov.innerHTML =
        '<div class="fm-dialog fm-conflict">' +
        '<header>' + esc(self.t("conflictTitle")) + '</header>' +
        '<div class="fm-dialog-body">' +
        '<p class="fm-dialog-msg">' + esc(self.t("conflictMsg", names.length, folder)) + '</p>' +
        '<ul class="fm-conflict-list">' + list + '</ul>' +
        '</div>' +
        '<footer>' +
        '<button class="fm-btn-secondary" data-choice="cancel">' + esc(self.t("cancel")) + '</button>' +
        '<button class="fm-btn-secondary" data-choice="rename">' + esc(self.t("keepBoth")) + '</button>' +
        '<button class="fm-btn-primary" data-choice="overwrite">' + esc(self.t("overwrite")) + '</button>' +
        '</footer></div>';
      document.body.appendChild(ov);
      function done(v) { ov.remove(); resolve(v); }
      ov.querySelectorAll("[data-choice]").forEach(function (b) {
        b.onclick = function () {
          var c = b.getAttribute("data-choice");
          done(c === "cancel" ? null : c);
        };
      });
      ov.querySelector('[data-choice="overwrite"]').focus();
    });
  };

  /* --------------------------------------------------------------- upload
   * Chunked + resumable. Every file goes through
   *   POST /upload/init      → session id + bytes the server already has
   *   POST /upload/chunk     → raw body, one request per ~8 MB
   *   POST /upload/complete  → moves the assembled file into place
   * A plain request body (not multipart) means file size is limited only by
   * disk space, so 10 GB+ works, and because the session id is derived from the
   * file's identity, re-dropping the same file after a network failure resumes
   * instead of restarting. Folder drops keep their hierarchy via relative paths.
   */

  var UPLOAD_CHUNK = 8 * 1024 * 1024;

  /* Reads dropped FileSystemEntry trees (folders keep their relative path). */
  P._dropEntries = function (dataTransfer) {
    var entries = [];
    if (dataTransfer.items) {
      for (var i = 0; i < dataTransfer.items.length; i++) {
        var it = dataTransfer.items[i];
        if (it.kind !== "file") continue;
        var en = it.webkitGetAsEntry ? it.webkitGetAsEntry() : null;
        if (en) entries.push(en);
      }
    }
    if (!entries.length) {
      return Promise.resolve(Array.prototype.map.call(dataTransfer.files || [], function (f) {
        return { file: f, rel: f.webkitRelativePath || f.name };
      }));
    }

    var out = [];
    function walk(entry, base) {
      return new Promise(function (resolve) {
        if (entry.isFile) {
          entry.file(function (f) { out.push({ file: f, rel: base + f.name }); resolve(); },
                     function () { resolve(); });
        } else if (entry.isDirectory) {
          var reader = entry.createReader(), all = [];
          (function readMore() {
            reader.readEntries(function (batch) {
              if (!batch.length) {
                Promise.all(all.map(function (c) { return walk(c, base + entry.name + "/"); }))
                  .then(resolve);
                return;
              }
              all = all.concat(Array.prototype.slice.call(batch));
              readMore();
            }, function () { resolve(); });
          })();
        } else resolve();
      });
    }
    return Promise.all(entries.map(function (e) { return walk(e, ""); }))
      .then(function () { return out; });
  };

  P.uploadFiles = function (items) {
    if (this.config.allowUpload === false) return;
    var self = this;
    var list = Array.prototype.map.call(items || [], function (it) {
      if (it instanceof File) return { file: it, rel: it.webkitRelativePath || it.name };
      return it;
    }).filter(function (x) { return x && x.file; });
    if (!list.length) return;

    var max = this.config.maxUploadBytes || 0;
    var tooBig = list.filter(function (x) { return max > 0 && x.file.size > max; });
    if (tooBig.length) {
      this.toast(this.t("tooBig", tooBig[0].rel), "error");
      return;
    }

    var ov = document.createElement("div");
    ov.className = "fm-overlay";
    var rows = list.map(function (x, i) {
      return '<div class="fm-progress-row"><span class="fm-pname" title="' + esc(x.rel) + '">' +
             esc(x.rel) + '</span><progress id="fmup-' + i + '" max="100" value="0"></progress></div>';
    }).join("");
    ov.innerHTML = '<div class="fm-dialog"><header>' + esc(self.t("uploading", list.length)) + '</header>' +
      '<div class="fm-dialog-body">' + rows + '</div>' +
      '<footer><button class="fm-btn-secondary">' + esc(self.t("cancel")) + '</button></footer></div>';
    document.body.appendChild(ov);

    var cancelled = false, controller = null, failed = 0, idx = 0;
    ov.querySelector(".fm-btn-secondary").onclick = function () {
      cancelled = true;
      if (controller) controller.abort();
      ov.remove();
    };

    function step() {
      if (cancelled) return;
      if (idx >= list.length) {
        if (!failed) self.toast(self.t("uploadDone", list.length), "ok");
        setTimeout(function () { ov.remove(); self.refresh(); }, 500);
        return;
      }
      var i = idx++, item = list[i];
      var bar = document.getElementById("fmup-" + i);
      controller = new AbortController();
      self._uploadOne(item.file, item.rel, bar, controller.signal)
        .then(function () { if (bar) bar.value = 100; })
        .catch(function (err) {
          if (cancelled) return;
          if (err && err.quiet) return;
          failed++;
          self.toast(item.rel + ": " + (err && err.message ? err.message : self.t("uploadFailed")), "error");
        })
        .then(step);
    }
    step();
  };

  /* Sends one file as a series of chunks; re-opens the session after a network
   * error so the transfer continues from whatever the server already stored. */
  P._uploadOne = function (file, rel, bar, signal) {
    var self = this;
    var base = this.api + "/upload", q = "?instance=" + encodeURIComponent(this.id);
    var id = null, received = 0, resumed = false;

    function progress(v) {
      if (bar) bar.value = Math.max(0, Math.min(100, Math.round(v * 100)));
    }
    function readJson(p) {
      return p.then(function (r) {
        return r.json().catch(function () { return {}; });
      });
    }
    function open() {
      return readJson(fetch(base + "/init" + q, {
        method: "POST", signal: signal,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          path: self.path, name: file.name, relativePath: rel,
          size: file.size, lastModified: file.lastModified || 0
        })
      })).then(function (res) {
        if (!res.ok) throw new Error(res.error || "Upload rejected");
        var d = res.data || {};
        id = d.id;
        received = d.received || 0;
        if (received > 0 && !resumed) {
          resumed = true;
          self.toast(self.t("resuming", rel), "info");
        }
        progress(file.size ? received / file.size : 1);
        return d.chunkSize || UPLOAD_CHUNK;
      });
    }

    var chunkSize = UPLOAD_CHUNK;

    function sendChunk(tries) {
      if (received >= file.size) return Promise.resolve();
      var end = Math.min(file.size, received + chunkSize);
      var blob = file.slice(received, end);
      var url = base + "/chunk" + q + "&id=" + id + "&offset=" + received;
      return readJson(fetch(url, {
        method: "POST", body: blob, signal: signal,
        headers: { "Content-Type": "application/octet-stream" }
      })).then(function (r) {
        if (r.ok) {
          received = r.data.received;
          progress(file.size ? received / file.size : 1);
          return sendChunk(0);
        }
        if (r.received != null) {          /* server is ahead of us — resync */
          received = r.received;
          progress(file.size ? received / file.size : 1);
          return sendChunk(0);
        }
        throw new Error(r.error || "Chunk failed");
      }, function (err) {
        if ((signal && signal.aborted) || tries >= 2) throw err;
        /* network hiccup: ask the server how much it has and continue */
        return open().then(function () { return sendChunk(tries + 1); });
      });
    }

    function complete(overwrite) {
      var url = base + "/complete" + q + "&id=" + id + (overwrite ? "&overwrite=1" : "");
      return readJson(fetch(url, { method: "POST", signal: signal })).then(function (r) {
        if (r.ok) return;
        if (r.conflicts && !overwrite) {
          return self._confirm(self.t("overwriteTitle"), self.t("overwriteFileMsg", rel))
            .then(function (yes) {
              if (!yes) { var e = new Error(""); e.quiet = true; throw e; }
              return complete(true);
            });
        }
        throw new Error(r.error || "Upload failed");
      });
    }

    return open().then(function (suggested) {
      /* small files get a finer chunk so the bar moves smoothly */
      chunkSize = Math.min(suggested, Math.max(256 * 1024, Math.ceil(file.size / 100) || 1));
      return sendChunk(0);
    }).then(function () { return complete(false); });
  };

  /* Office formats the server can convert to PDF (LibreOffice) */
  var OFFICE_EXTS = ["doc", "docx", "docm", "dot", "dotx", "wps", "rtf", "odt",
    "xls", "xlsx", "xlsm", "xlsb", "et", "csv", "ods",
    "ppt", "pptx", "pps", "ppsx", "pot", "potx", "dps", "odp"];

  /* true when the file is an Office document the server cannot convert because
   * LibreOffice is missing — preview must be blocked, not attempted */
  P._previewBlocked = function (entry) {
    if (!entry || entry.isDirectory) return false;
    if (OFFICE_EXTS.indexOf((entry.extension || "").toLowerCase()) < 0) return false;
    return this.config.officePreviewEnabled !== true || this.config.officePreviewAvailable === false;
  };

  /* -------------------------------------------------------------- preview */
  var VIDEO_EXTS = ["mp4", "webm", "ogg", "mov", "m4v", "mkv", "avi"];
  var AUDIO_EXTS = ["mp3", "wav", "m4a", "flac", "aac", "oga"];
  var TEXT_EXTS = ["txt", "log", "md", "json", "xml", "css", "scss", "less", "js", "ts", "jsx", "tsx",
    "html", "htm", "vue", "cs", "py", "java", "go", "rs", "php", "rb", "kt", "sh", "bat",
    "ps1", "yml", "yaml", "ini", "conf", "cfg", "toml", "properties", "csv", "tsv", "sql", "tex", "nfo"];
  var TEXT_PREVIEW_LIMIT = 4 * 1024 * 1024;

  /* The download endpoint reports failures as a JSON envelope (HTTP 200), so a
   * preview must sniff the content type — otherwise the error text would be
   * shown as the file's content. */
  P._fetchPayload = function (url, opts) {
    return fetch(url, opts).then(function (r) {
      var type = r.headers.get("content-type") || "";
      if (type.indexOf("application/json") >= 0) {
        return r.json().catch(function () { return null; }).then(function (j) {
          throw new Error((j && j.error) || ("HTTP " + r.status));
        });
      }
      if (!r.ok) throw new Error("HTTP " + r.status);
      return r;
    });
  };

  /* Replaces the preview body with a readable failure + a download link. */
  P._previewFail = function (ov, err, fileUrl) {
    var body = ov && ov.querySelector(".fm-preview-body");
    if (!body) return;
    var dl = fileUrl ? fileUrl.replace("inline=1", "inline=0") : "";
    body.innerHTML = '<div class="fm-preview-fail"><div>' +
      '<p>' + esc(this.t("previewFailed")) +
      (err && err.message ? "：" + esc(err.message) : "") + '</p>' +
      (dl ? '<a class="fm-btn-secondary" href="' + dl + '" download>' + esc(this.t("download")) + '</a>' : '') +
      '</div></div>';
  };

  /* ------------------------------------------------------- markdown preview */
  var MD_EXTS = ["md", "markdown"];

  function mdHeld(list, html) {
    list.push(html);
    return "\u0000" + (list.length - 1) + "\u0000";
  }

  function mdSplitRow(line) {
    var s = line.trim();
    if (s.charAt(0) === "|") s = s.slice(1);
    if (s.charAt(s.length - 1) === "|") s = s.slice(0, -1);
    return s.split("|");
  }

  /* Renders inline markup. Everything is escaped first; only the tags we build
   * here end up in the DOM, so raw HTML inside a .md file stays visible text. */
  function mdInline(src, resolve) {
    var held = [];
    src = String(src == null ? "" : src);
    src = src.replace(/`([^`]+)`/g, function (_, code) {
      return mdHeld(held, "<code>" + esc(code) + "</code>");
    });
    src = src.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+["']([^"']*)["'])?\)/g,
      function (_, alt, url, title) {
        var safe = resolve(url);
        if (!safe || /^\s*(javascript|data|vbscript):/i.test(safe)) return esc(alt);
        return mdHeld(held, '<img src="' + esc(safe) + '" alt="' + esc(alt) + '"' +
          (title ? ' title="' + esc(title) + '"' : "") + ' loading="lazy" />');
      });
    src = src.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+["']([^"']*)["'])?\)/g,
      function (_, label, url, title) {
        var safe = resolve(url);
        if (!safe || /^\s*(javascript|data|vbscript):/i.test(safe)) return esc(label);
        return mdHeld(held, '<a href="' + esc(safe) + '"' +
          (title ? ' title="' + esc(title) + '"' : "") +
          ' target="_blank" rel="noopener noreferrer">' + esc(label) + "</a>");
      });
    src = src.replace(/<((?:https?:\/\/|mailto:)[^>\s]+)>/g, function (_, url) {
      return mdHeld(held, '<a href="' + esc(url) + '" target="_blank" rel="noopener noreferrer">' +
        esc(url) + "</a>");
    });
    src = esc(src);
    src = src.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
      .replace(/__([^_]+)__/g, "<strong>$1</strong>")
      .replace(/~~([^~]+)~~/g, "<del>$1</del>")
      .replace(/(^|[\s(])\*([^*\s][^*]*?)\*(?=$|[\s).,;:!?])/g, "$1<em>$2</em>")
      .replace(/(^|[\s(])_([^_\s][^_]*?)_(?=$|[\s).,;:!?])/g, "$1<em>$2</em>");
    return src.replace(/\u0000(\d+)\u0000/g, function (_, n) { return held[+n] || ""; });
  }

  function mdList(lines, resolve) {
    var html = "", stack = [];
    lines.forEach(function (raw) {
      var m = raw.match(/^(\s*)([-*+]|\d+\.)\s+(.*)$/);
      if (!m) return;
      var indent = m[1].replace(/\t/g, "    ").length;
      var tag = /\d/.test(m[2]) ? "ol" : "ul";
      var text = m[3];
      while (stack.length && indent < stack[stack.length - 1].indent)
        html += "</li></" + stack.pop().tag + ">";
      if (!stack.length || indent > stack[stack.length - 1].indent) {
        html += "<" + tag + ">";
        stack.push({ indent: indent, tag: tag });
      } else if (stack[stack.length - 1].tag !== tag) {
        html += "</li></" + stack.pop().tag + "><" + tag + ">";
        stack.push({ indent: indent, tag: tag });
      } else {
        html += "</li>";
      }
      var task = text.match(/^\[([ xX])\]\s+(.*)$/);
      if (task) {
        html += '<li class="fm-md-task"><input type="checkbox" disabled' +
          (task[1].toLowerCase() === "x" ? " checked" : "") + " /> " + mdInline(task[2], resolve);
      } else {
        html += "<li>" + mdInline(text, resolve);
      }
    });
    while (stack.length) html += "</li></" + stack.pop().tag + ">";
    return html;
  }

  function mdBlocks(src, resolve) {
    var lines = String(src == null ? "" : src).replace(/\r\n?/g, "\n").split("\n");
    var out = [], i = 0;

    function blockStart(line) {
      if (/^\s{0,3}(#{1,6}\s|```|~~~|>)/.test(line)) return true;
      if (/^\s*([-*+]|\d+\.)\s+/.test(line)) return true;
      if (/^\s{0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)) return true;
      return line.indexOf("|") >= 0 && i + 1 < lines.length &&
        /^\s*\|?\s*:?-{2,}/.test(lines[i + 1]);
    }

    while (i < lines.length) {
      var line = lines[i];
      if (!line.trim()) { i++; continue; }

      var fence = line.match(/^\s*(```|~~~)\s*([\w+#.-]*)\s*$/);
      if (fence) {
        var code = [], close = new RegExp("^\\s*" + fence[1] + "\\s*$");
        i++;
        while (i < lines.length && !close.test(lines[i])) { code.push(lines[i]); i++; }
        if (i < lines.length) i++;
        out.push("<pre><code" + (fence[2] ? ' class="language-' + esc(fence[2]) + '"' : "") + ">" +
          esc(code.join("\n")) + "</code></pre>");
        continue;
      }

      var h = line.match(/^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$/);
      if (h) {
        var lv = h[1].length;
        out.push("<h" + lv + ">" + mdInline(h[2], resolve) + "</h" + lv + ">");
        i++; continue;
      }

      if (/^\s{0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)) { out.push("<hr />"); i++; continue; }

      if (/^\s{0,3}>/.test(line)) {
        var quote = [];
        while (i < lines.length && /^\s{0,3}>/.test(lines[i])) {
          quote.push(lines[i].replace(/^\s{0,3}>\s?/, ""));
          i++;
        }
        out.push("<blockquote>" + mdBlocks(quote.join("\n"), resolve) + "</blockquote>");
        continue;
      }

      if (line.indexOf("|") >= 0 && i + 1 < lines.length &&
          /^\s*\|?\s*:?-{2,}/.test(lines[i + 1])) {
        var header = mdSplitRow(line);
        var aligns = mdSplitRow(lines[i + 1]).map(function (c) {
          var s = c.trim();
          if (/^:-+:$/.test(s)) return "center";
          if (/^-+:$/.test(s)) return "right";
          if (/^:-+$/.test(s)) return "left";
          return "";
        });
        i += 2;
        var rows = [];
        while (i < lines.length && lines[i].trim() && lines[i].indexOf("|") >= 0) {
          rows.push(mdSplitRow(lines[i])); i++;
        }
        var th = header.map(function (c, n) {
          return "<th" + (aligns[n] ? ' style="text-align:' + aligns[n] + '"' : "") + ">" +
            mdInline(c.trim(), resolve) + "</th>";
        }).join("");
        var tb = rows.map(function (r) {
          return "<tr>" + header.map(function (_, n) {
            return "<td" + (aligns[n] ? ' style="text-align:' + aligns[n] + '"' : "") + ">" +
              mdInline((r[n] || "").trim(), resolve) + "</td>";
          }).join("") + "</tr>";
        }).join("");
        out.push("<table><thead><tr>" + th + "</tr></thead><tbody>" + tb + "</tbody></table>");
        continue;
      }

      if (/^\s*([-*+]|\d+\.)\s+/.test(line)) {
        var items = [];
        while (i < lines.length && (/^\s*([-*+]|\d+\.)\s+/.test(lines[i]) || !lines[i].trim())) {
          if (lines[i].trim()) items.push(lines[i]);
          i++;
        }
        out.push(mdList(items, resolve));
        continue;
      }

      var para = [];
      while (i < lines.length && lines[i].trim() && !blockStart(lines[i])) {
        para.push(lines[i]); i++;
      }
      out.push("<p>" + para.join("\n").split(/ {2,}\n/).map(function (p) {
        return mdInline(p.replace(/\n/g, " "), resolve);
      }).join("<br />") + "</p>");
    }
    return out.join("\n");
  }

  /* Resolves a Markdown URL against the folder of the document. Root-relative
   * and external URLs are kept; relative ones become download links; anything
   * with an unknown scheme (javascript:, data:) is dropped. */
  function mdResolveUrl(baseDir, raw, downloadUrl) {
    var url = String(raw || "").trim();
    if (!url) return "";
    if (/^(https?:\/\/|mailto:)/i.test(url)) return url;
    if (url.charAt(0) === "#" || url.charAt(0) === "/") return url;
    if (/^[a-z][a-z0-9+.-]*:/i.test(url)) return "";
    var segs = (baseDir + "/" + url).split("/"), norm = [];
    segs.forEach(function (s) {
      if (!s || s === ".") return;
      if (s === "..") { norm.pop(); return; }
      norm.push(s);
    });
    return downloadUrl + "&inline=1&path=" + encodeURIComponent("/" + norm.join("/"));
  }

  function renderMarkdown(text, resolve) {
    try {
      return mdBlocks(text, resolve);
    } catch (err) {
      return "<pre>" + esc(text) + "</pre>";
    }
  }

  /* exposed for unit tests — both functions are pure and never touch the DOM */
  core.markdown = {
    render: renderMarkdown,
    resolveUrl: mdResolveUrl
  };

  P._preview = function (e) {
    if (this.config.allowDownload === false) { this.toast(this.t("previewDisabled"), "info"); return; }
    if (this._previewBlocked(e)) {
      var reason = this.config.officePreviewEnabled !== true
        ? "officePreviewDisabled" : "officeUnavailable";
      this.toast(this.t(reason), "info");
      return;
    }
    var self = this;
    var ext = (e.extension || "").toLowerCase();
    /* media and PDFs go through /download?inline=1: seeking in a <video> needs
     * 206 Partial Content, which only that endpoint (range-enabled) provides */
    var fileUrl = this.api + "/download?instance=" + encodeURIComponent(this.id) +
                  "&inline=1&path=" + encodeURIComponent(e.path);

    if (["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico"].indexOf(ext) >= 0) {
      this._imageViewer(e);
      return;
    }

    if (VIDEO_EXTS.indexOf(ext) >= 0 || AUDIO_EXTS.indexOf(ext) >= 0) {
      var isVideo = VIDEO_EXTS.indexOf(ext) >= 0;
      var ovm = this._openPreviewShell(e.name, '<div class="fm-preview-body">' +
        (isVideo
          ? '<video controls preload="metadata" src="' + fileUrl + '"></video>'
          : '<audio controls preload="metadata" src="' + fileUrl + '"></audio>') + '</div>');
      var media = ovm.querySelector(isVideo ? "video" : "audio");
      var failed = false;
      function mediaFail() {
        if (failed) return;
        failed = true;
        self._previewFail(ovm, new Error(self.t("codecUnsupported")), fileUrl);
      }
      media.addEventListener("error", mediaFail);
      /* a request that failed before the listener was attached */
      if (media.error) mediaFail();
      return;
    }

    if (ext === "pdf") {
      var ovp = this._openPreviewShell(e.name,
        '<div class="fm-preview-body"><span class="fm-preview-note"><span class="fmnet-spinner"></span>' +
        esc(this.t("loading")) + '</span></div>');
      /* preflight with a 2-byte range request: cheap, and it turns a broken
       * path or a JSON error into a message instead of a blank frame */
      this._fetchPayload(fileUrl, { headers: { Range: "bytes=0-1" } }).then(function () {
        var body = ovp.querySelector(".fm-preview-body");
        if (!body) return;                      /* dialog already closed */
        body.innerHTML = '<iframe src="' + fileUrl + '" title="' + esc(e.name) + '"></iframe>';
      }).catch(function (err) { self._previewFail(ovp, err, fileUrl); });
      return;
    }

    if (OFFICE_EXTS.indexOf(ext) >= 0) {
      /* Office documents are converted to PDF by the server (LibreOffice) */
      var officeUrl = this.api + "/office?instance=" + encodeURIComponent(this.id) +
                      "&path=" + encodeURIComponent(e.path);
      var shell = this._openPreviewShell(e.name,
        '<div class="fm-preview-body fm-office-body">' +
        '<div class="fm-office-loading"><span class="fmnet-spinner"></span><span>' +
        esc(this.t("officeConverting")) + '</span></div><iframe title="' + esc(e.name) + '"></iframe></div>');
      var iframe = shell.querySelector("iframe");
      var loading = shell.querySelector(".fm-office-loading");
      function fail(msg) {
        if (loading) loading.innerHTML = '<span>' + esc(self.t("officePreviewFailed")) +
          (msg ? "：" + esc(msg) : "") + '</span>';
        if (iframe) iframe.remove();
      }
      fetch(officeUrl).then(function (r) {
        var type = r.headers.get("content-type") || "";
        if (!r.ok || type.indexOf("application/json") >= 0) {
          return r.json().catch(function () { return null; }).then(function (j) {
            throw new Error((j && j.error) || ("HTTP " + r.status));
          });
        }
        return r.blob();
      }).then(function (blob) {
        var objectUrl = URL.createObjectURL(blob);
        iframe.addEventListener("load", function () {
          URL.revokeObjectURL(objectUrl);
          if (loading) loading.remove();
        });
        iframe.src = objectUrl;
      }).catch(function (err) { fail(err.message); });
      return;
    }

    if (TEXT_EXTS.indexOf(ext) >= 0) {
      var isMd = MD_EXTS.indexOf(ext) >= 0;
      var mdMode = "preview";
      var headExtra = this._encodingSelectHtml(ext);
      if (isMd) {
        headExtra = '<span class="fm-md-head"><span class="fm-md-tabs">' +
          '<button type="button" class="fm-md-tab is-active" data-md="preview">' +
          esc(this.t("mdPreview")) + '</button>' +
          '<button type="button" class="fm-md-tab" data-md="source">' +
          esc(this.t("mdSource")) + '</button></span>' + headExtra + '</span>';
      }
      var ov0 = this._openPreviewShell(e.name,
        '<div class="fm-preview-body' + (isMd ? ' fm-md-body' : '') + '"><pre>' +
        esc(this.t("loading")) + '</pre></div>',
        headExtra);
      /* links and images inside the document resolve relative to its folder */
      var mdBase = e.path.slice(0, e.path.lastIndexOf("/"));
      var mdDownload = self.api + "/download?instance=" + encodeURIComponent(self.id);
      function mdResolve(raw) {
        return mdResolveUrl(mdBase, raw, mdDownload);
      }
      /* logs can be gigabytes — read the head of the file only */
      var truncated = (e.size || 0) > TEXT_PREVIEW_LIMIT;
      var opts = truncated ? { headers: { Range: "bytes=0-" + (TEXT_PREVIEW_LIMIT - 1) } } : undefined;
      this._fetchPayload(fileUrl, opts)
        .then(function (r) { return r.arrayBuffer(); })
        .then(function (buf) {
          var body = ov0.querySelector(".fm-preview-body");
          var sel = ov0.querySelector(".fm-enc-select");
          var det = ov0.querySelector(".fm-enc-detected");
          if (!body) return;                     /* dialog already closed */
          function render(choice) {
            var r = decodeBytes(buf, choice);
            var text = r.text == null ? "" : r.text;
            if (det) det.textContent = (choice === "auto" && r.enc) ? r.enc.toUpperCase() : "";
            body.classList.toggle("fm-md-body", isMd && mdMode === "preview");
            if (isMd && mdMode === "preview") {
              body.innerHTML = '<div class="fm-md">' + renderMarkdown(text, mdResolve) + '</div>' +
                (truncated ? '<div class="fm-md-note">— ' +
                  esc(self.t("previewTruncated", "4 MB")) + ' —</div>' : "");
              return;
            }
            body.innerHTML = "<pre></pre>";
            body.querySelector("pre").textContent = text +
              (truncated ? "\n\n— " + self.t("previewTruncated", "4 MB") + " —" : "");
          }
          var saved = "auto";
          try { saved = localStorage.getItem("fmnet-enc-" + ext) || "auto"; } catch (err) {}
          if (sel) sel.value = saved;
          render(saved);
          if (sel) sel.addEventListener("change", function () {
            try { localStorage.setItem("fmnet-enc-" + ext, sel.value); } catch (err) {}
            render(sel.value);
          });
          if (isMd) {
            ov0.querySelectorAll(".fm-md-tab").forEach(function (btn) {
              btn.addEventListener("click", function () {
                mdMode = btn.getAttribute("data-md");
                ov0.querySelectorAll(".fm-md-tab").forEach(function (b) {
                  b.classList.toggle("is-active", b === btn);
                });
                render(sel ? sel.value : "auto");
              });
            });
          }
        })
        .catch(function (err) { self._previewFail(ov0, err, fileUrl); });
      return;
    }

    this.toast(this.t("noPreview", ext), "info");
  };

  P._openPreviewShell = function (title, bodyHtml, headExtra, onClose) {
    var ov = document.createElement("div");
    ov.className = "fm-overlay";
    ov.innerHTML = '<div class="fm-dialog"><header><span class="fm-dialog-title">' + esc(title) +
      '</span>' + (headExtra || '') +
      '<button class="fm-dialog-close" title="' + esc(this.t("close")) + '">&times;</button></header>' + bodyHtml +
      '<footer><button class="fm-btn-secondary">' + esc(this.t("close")) + '</button></footer></div>';
    document.body.appendChild(ov);
    function close() {
      document.removeEventListener("keydown", onKey);
      if (onClose) { try { onClose(); } catch (e) {} }
      if (ov._fmOnClose) { try { ov._fmOnClose(); } catch (e) {} }
      ov.remove();
    }
    function onKey(ev) { if (ev.key === "Escape") close(); }
    document.addEventListener("keydown", onKey);
    ov.querySelector(".fm-btn-secondary").onclick = close;
    ov.querySelector(".fm-dialog-close").onclick = close;
    ov._fmClose = close;
    return ov;
  };

  /* toolbar glyphs for the image viewer (inline SVG — no font dependency) */
  var IMG_ICON = {
    rotL: '<svg viewBox="0 0 24 24"><path fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" d="M5.2 12.4a7 7 0 1 0 2-4.9"/><path fill="currentColor" d="M3.4 4.1v4.8h4.8z"/></svg>',
    rotR: '<svg viewBox="0 0 24 24"><path fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" d="M18.8 12.4a7 7 0 1 1-2-4.9"/><path fill="currentColor" d="M20.6 4.1v4.8h-4.8z"/></svg>',
    flipH: '<svg viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="1.5" stroke-dasharray="2 2" d="M12 3v18"/><path fill="currentColor" d="M9.6 6.4v11.2L3.4 12z"/><path fill="none" stroke="currentColor" stroke-width="1.6" d="M14.4 6.4h6.2v11.2h-6.2z"/></svg>',
    flipV: '<svg viewBox="0 0 24 24"><path stroke="currentColor" stroke-width="1.5" stroke-dasharray="2 2" d="M3 12h18"/><path fill="currentColor" d="M6.4 9.6h11.2L12 3.4z"/><path fill="none" stroke="currentColor" stroke-width="1.6" d="M6.4 14.4v6.2h11.2v-6.2z"/></svg>'
  };

  /* ------------------------------------------------------------ image viewer
   * Zoom (wheel / buttons / pinch), pan by dragging, rotate in 90° steps and
   * flip horizontally or vertically. Keyboard: + - 0 1 r f and the arrows. */
  P._imageViewer = function (e) {
    var self = this;
    var url = this.api + "/download?instance=" + encodeURIComponent(this.id) +
              "&inline=1&path=" + encodeURIComponent(e.path);
    var shell = this._openPreviewShell(e.name,
      '<div class="fm-imgview">' +
      '<div class="fm-imgtools">' +
      '<button data-img="out" title="' + esc(this.t("zoomOut")) + '">−</button>' +
      '<button data-img="in" title="' + esc(this.t("zoomIn")) + '">+</button>' +
      '<button data-img="fit" title="' + esc(this.t("zoomFit")) + '">' + esc(this.t("zoomFit")) + '</button>' +
      '<button data-img="one" title="' + esc(this.t("zoomActual")) + '">1:1</button>' +
      '<span class="fm-imgzoom">100%</span>' +
      '<span class="fm-imgdim"></span>' +
      '<span class="fm-imgsep"></span>' +
      '<button data-img="rotL" title="' + esc(this.t("rotateLeft")) + '">' + IMG_ICON.rotL + '</button>' +
      '<button data-img="rotR" title="' + esc(this.t("rotateRight")) + '">' + IMG_ICON.rotR + '</button>' +
      '<button data-img="flipH" title="' + esc(this.t("flipH")) + '">' + IMG_ICON.flipH + '</button>' +
      '<button data-img="flipV" title="' + esc(this.t("flipV")) + '">' + IMG_ICON.flipV + '</button>' +
      '<button data-img="reset" class="fm-imgtext" title="' + esc(this.t("imgReset")) + '">' +
      esc(this.t("imgReset")) + '</button>' +
      '</div>' +
      '<div class="fm-imgstage"><span class="fm-imgmsg">' + esc(this.t("loading")) + '</span>' +
      '<img alt="" draggable="false"></div>' +
      '</div>');

    var stage = shell.querySelector(".fm-imgstage");
    var img = stage.querySelector("img");
    var zoomLabel = shell.querySelector(".fm-imgzoom");
    var dimLabel = shell.querySelector(".fm-imgdim");
    var msg = stage.querySelector(".fm-imgmsg");
    var st = { s: 1, tx: 0, ty: 0, rot: 0, fx: 1, fy: 1, fit: true, base: 1 };

    function apply() {
      img.style.transform = "translate(" + st.tx + "px," + st.ty + "px) rotate(" + st.rot + "deg) scale(" +
        (st.s * st.fx) + "," + (st.s * st.fy) + ")";
      zoomLabel.textContent = Math.round(st.s * 100) + "%";
    }

    function fitScale() {
      var sw = Math.max(40, stage.clientWidth - 24), sh = Math.max(40, stage.clientHeight - 24);
      var iw = img.naturalWidth || 1, ih = img.naturalHeight || 1;
      var rw = st.rot % 180 ? ih : iw, rh = st.rot % 180 ? iw : ih;
      /* small images may be enlarged, but not absurdly */
      return Math.min(sw / rw, sh / rh, 4);
    }

    function fit() {
      st.base = fitScale();
      st.s = st.base; st.tx = 0; st.ty = 0; st.fit = true;
      apply();
    }

    function zoom(factor, cx, cy) {
      var ns = Math.max(0.05, Math.min(16, st.s * factor));
      if (Math.abs(ns - st.s) < 1e-4) return;
      if (cx != null) {
        st.tx = cx - (cx - st.tx) * (ns / st.s);
        st.ty = cy - (cy - st.ty) * (ns / st.s);
      }
      st.s = ns; st.fit = false;
      apply();
    }

    function centerOf(ev) {
      var r = stage.getBoundingClientRect();
      return { x: ev.clientX - (r.left + r.width / 2), y: ev.clientY - (r.top + r.height / 2) };
    }

    function rotate(delta) {
      st.rot = (st.rot + delta + 360) % 360;
      if (st.fit) { st.base = fitScale(); st.s = st.base; st.tx = st.ty = 0; }
      apply();
    }

    var act = {
      "in": function () { zoom(1.2); },
      "out": function () { zoom(1 / 1.2); },
      "fit": fit,
      "one": function () { st.s = 1; st.fit = false; st.tx = st.ty = 0; apply(); },
      rotL: function () { rotate(-90); },
      rotR: function () { rotate(90); },
      flipH: function () { st.fx *= -1; apply(); },
      flipV: function () { st.fy *= -1; apply(); },
      reset: function () { st.rot = 0; st.fx = 1; st.fy = 1; fit(); }
    };
    shell.querySelectorAll("[data-img]").forEach(function (b) {
      b.onclick = function () { act[b.getAttribute("data-img")](); };
    });

    stage.addEventListener("wheel", function (ev) {
      ev.preventDefault();
      var c = centerOf(ev);
      zoom(ev.deltaY < 0 ? 1.15 : 1 / 1.15, c.x, c.y);
    }, { passive: false });

    /* drag to pan + two-finger pinch */
    var pointers = new Map(), drag = null, pinch = null;

    function pinchInfo() {
      var p = Array.from(pointers.values());
      var dx = p[0].x - p[1].x, dy = p[0].y - p[1].y;
      return { d: Math.hypot(dx, dy) || 1, s: st.s };
    }

    stage.addEventListener("pointerdown", function (ev) {
      if (ev.pointerType === "mouse" && ev.button !== 0) return;
      ev.preventDefault();
      pointers.set(ev.pointerId, { x: ev.clientX, y: ev.clientY });
      try { stage.setPointerCapture(ev.pointerId); } catch (err) {}
      if (pointers.size === 1) {
        drag = { x: ev.clientX, y: ev.clientY, tx: st.tx, ty: st.ty };
        stage.classList.add("dragging");
      } else if (pointers.size === 2) {
        drag = null;
        pinch = pinchInfo();
      }
    });

    stage.addEventListener("pointermove", function (ev) {
      if (!pointers.has(ev.pointerId)) return;
      pointers.set(ev.pointerId, { x: ev.clientX, y: ev.clientY });
      if (pointers.size >= 2 && pinch) {
        var info = pinchInfo();
        st.s = Math.max(0.05, Math.min(16, pinch.s * (info.d / pinch.d)));
        st.fit = false;
        apply();
        return;
      }
      if (drag) {
        st.tx = drag.tx + (ev.clientX - drag.x);
        st.ty = drag.ty + (ev.clientY - drag.y);
        st.fit = false;
        apply();
      }
    });

    function endPointer(ev) {
      pointers.delete(ev.pointerId);
      if (pointers.size < 2) pinch = null;
      if (!pointers.size) { drag = null; stage.classList.remove("dragging"); }
    }
    stage.addEventListener("pointerup", endPointer);
    stage.addEventListener("pointercancel", endPointer);

    stage.addEventListener("dblclick", function () {
      if (st.fit) { st.s = 1; st.fit = false; st.tx = st.ty = 0; apply(); }
      else fit();
    });

    img.addEventListener("load", function () {
      if (msg) { msg.remove(); msg = null; }
      img.style.visibility = "visible";
      dimLabel.textContent = img.naturalWidth + " × " + img.naturalHeight;
      fit();
    });
    img.addEventListener("error", function () {
      if (!msg) {
        msg = document.createElement("span");
        msg.className = "fm-imgmsg";
        stage.appendChild(msg);
      }
      msg.textContent = self.t("previewFailed");
      img.style.visibility = "hidden";
    });

    function onKey(ev) {
      if (!shell.isConnected) { cleanup(); return; }
      var k = ev.key, step = 40;
      if (k === "+" || k === "=") { zoom(1.2); ev.preventDefault(); }
      else if (k === "-" || k === "_") { zoom(1 / 1.2); ev.preventDefault(); }
      else if (k === "0") { fit(); ev.preventDefault(); }
      else if (k === "1") { act.one(); ev.preventDefault(); }
      else if (k === "r" || k === "R") { rotate(ev.shiftKey ? -90 : 90); ev.preventDefault(); }
      else if (k === "f" || k === "F") { act.flipH(); ev.preventDefault(); }
      else if (k === "ArrowLeft") { st.tx += step; apply(); ev.preventDefault(); }
      else if (k === "ArrowRight") { st.tx -= step; apply(); ev.preventDefault(); }
      else if (k === "ArrowUp") { st.ty += step; apply(); ev.preventDefault(); }
      else if (k === "ArrowDown") { st.ty -= step; apply(); ev.preventDefault(); }
    }

    var ro = new ResizeObserver(function () { if (st.fit) fit(); });
    ro.observe(stage);
    document.addEventListener("keydown", onKey);
    function cleanup() {
      ro.disconnect();
      document.removeEventListener("keydown", onKey);
    }
    /* the shell calls this on every close path (buttons, ✕, Escape) */
    shell._fmOnClose = cleanup;

    img.src = url;
    if (img.complete && img.naturalWidth) {
      if (msg) { msg.remove(); msg = null; }
      dimLabel.textContent = img.naturalWidth + " × " + img.naturalHeight;
      fit();
    }
  };

  /* ------------------------------------------------ text encoding support */
  /* Encodings offered in the preview header; "auto" runs the detector.
   * Labels carry the script in its own writing system so they are readable
   * in any UI language. */
  var TEXT_ENCODINGS = [
    ["auto", "encAuto"],
    ["utf-8", "UTF-8"],
    ["utf-16le", "UTF-16 LE"],
    ["utf-16be", "UTF-16 BE"],
    ["gb18030", "GB18030 / GBK (简体中文)"],
    ["gbk", "GBK (简体中文)"],
    ["big5", "Big5 (繁體中文)"],
    ["big5-hkscs", "Big5-HKSCS (香港繁體)"],
    ["shift_jis", "Shift-JIS (日本語)"],
    ["euc-jp", "EUC-JP (日本語)"],
    ["iso-2022-jp", "ISO-2022-JP (日本語)"],
    ["euc-kr", "EUC-KR (한국어)"],
    ["windows-1252", "Windows-1252 (Western)"],
    ["iso-8859-1", "ISO-8859-1 (Latin-1)"],
    ["iso-8859-15", "ISO-8859-15 (Latin-9)"],
    ["windows-1250", "Windows-1250 (Central Europe)"],
    ["iso-8859-2", "ISO-8859-2 (Central Europe)"],
    ["windows-1251", "Windows-1251 (Кириллица)"],
    ["koi8-r", "KOI8-R (Кириллица)"],
    ["iso-8859-5", "ISO-8859-5 (Кириллица)"],
    ["windows-1253", "Windows-1253 (Ελληνικά)"],
    ["iso-8859-7", "ISO-8859-7 (Ελληνικά)"],
    ["windows-1254", "Windows-1254 (Türkçe)"],
    ["windows-1255", "Windows-1255 (עברית)"],
    ["windows-1256", "Windows-1256 (العربية)"],
    ["windows-1257", "Windows-1257 (Baltic)"],
    ["windows-1258", "Windows-1258 (Tiếng Việt)"],
    ["windows-874", "Windows-874 (ไทย)"],
    ["macintosh", "Macintosh"],
    ["x-mac-cyrillic", "Mac Cyrillic"],
    ["ibm866", "IBM866"]
  ];

  function tryDecode(buf, label) {
    try { return new TextDecoder(label).decode(buf); } catch (e) { return null; }
  }
  /* rough "does this look like text" score — lower is better */
  /* Most frequent Chinese characters (simplified + traditional) and Korean
   * syllables. A correct decoding hits these often, a wrong legacy decoding
   * yields rare ideographs, so the hit ratio makes a cheap language detector. */
  var CJK_SET = new Set(("的一是了不在人有我他这个们中来上大为和国地到以说时要就出会可也你对生能而子那得于着下自之年过发后作里用道行所然家种事成方多经去法学如都同现当没动面起看定天分还进好小部其些主样理心她本前开但因只从想实日军者意无力它与长把机十民第公此已工使情明性知全三又关点正业外将两高间由问很最重并物手应战向头文体政美相见被利什二等产或新己制身果加西斯月话合回特代内信表化老给世位次度门任常先海通教儿原东声提立及比员解水名真论处走义各入几口认条平系气题活尔更别打女变四神总何电数安少报才结反受目太量再感建务做接必场件计管期市直德资命山金指克许统区保至队形社便空决治展马科司五基眼书非则听白却界达光放强即像难且权思王象完设式色路记南品住告类求据程北边死张该交规万取拉格望觉术领共确传师观清今切院让识候带导争运笑飞风步改收根干造言联持组每济车亲极林服快办议往元英士证近失转夫令准布始怎呢存未远叫台单影具罗字爱击流备兵连调深商算质团集百需价花党华城石级府离况亚请技际约示复病息究线似官火断精满支视消越器容照须九增研写称企八功吗包片史委乎查轻易早曾除农找装广显吧阿李标谈吃图念六引历首医局突专费号尽另周较注语仅考落青随选列武红响虽推势参希古众构房半节土投某案黑维革划敌致陈律足态护七兴派孩验责营星够章音跟志底站严巴例防族供效续施留讲型料终答紧黄绝奇察母京段依批群项故按河米围江织害斗双境客纪采举杀攻父苏密低朝友诉止细愿千值仍男钱破网热助倒育属坐帝限船脸职速刻乐否刚威毛状率甚独球般普怕弹校苦创假久错承印晚兰试股拿脑预谁益阳若哪微尼继送急血惊伤素药适波夜省初喜卫源食险待述陆习置居劳财环排福纳欢雷警获模充负云停木游龙树疑层冷洲冲射略范竟句室异激汉村哈策演简卡罪判担州静退既衣您宗积余痛检差富灵协角占配征修皮挥胜降阶审沉坚善妈刘读超免压银买皇养伊怀执副乱抗犯追帮宣佛岁航优怪香田铁控税左右份穿艺背阵草脚概恶块顿敢守酒岛托央户烈洋哥索胡款靠评版宝座释景顾弟登货互付伯慢欧换闻危忙核暗姐介坏讨丽良序升监临亮露永呼味野架域沙掉括舰鱼杂误湾吉减编楚肯测败屋跑梦散温困剑渐封救贵枪缺楼县尚毫移娘朋画班智亦耳恩短掌恐遗固席松秘谢鲁遇康虑幸均销钟诗藏赶剧票损忽巨炮旧端探湖录叶春乡附吸予礼港雨板庭妇归睛饭额含顺输摇招婚脱补谓督毒油疗旅泽材灭逐莫笔亡鲜词圣择寻厂睡博勒烟授诺伦岸奥唐卖俄炸载洛健堂旁宫喝借君禁阴园谋宋避抓荣姑孙逃牙束跳顶玉镇雪午练迫爷篇肉嘴馆遍凡础洞卷坦牛宁纸诸训私庄祖丝翻暴森塔默握戏隐熟骨访弱蒙歌店鬼软典欲萨伙遭盘爸扩盖弄雄稳忘亿刺拥徒姆杨齐赛趣曲刀床迎冰虚玩析窗醒妻透购替塞努休虎扬途侵刑绿迅套贸毕唯谷轮库迹尤竞街促延震弃甲伟麻川申缓潜闪售灯针哲络抵朱埃抱鼓植纯夏忍页杰筑折郑贝尊吴秀混臣雅振染盛怒舞圆搞狂措姓残舰秋培迷诚宽宇猛摆梅毁伸摩盟末乃悲拍丁赵硬麦蒋操耶阻订彩抽赞魔纷沿喊违妹浪汇币丰蓝殊献桌啦瓦莱援译夺汽烧距裁偏符勇触课敬哭懂墙袭召罚侠厅拜巧侧韩冒债曼融惯享戴童犹乘挂奖绍厚纵障讯涉彻刊丈爆乌役描洗玛患妙镜唱烦签仙彼弗症仿倾牌陷鸟轰咱菜闭奋庆撤泪茶疾缘播朗杜奶季丹狗尾仪偷奔珠虫驻孔宜艾桥淡翼恨繁寒伴叹旦愈潮粮缩罢聚径恰挑袋灰捕徐珍幕映裂泰隔启尖忠累炎暂估泛荒偿横拒瑞忆孤鼻闹羊呆厉衡胞零穷舍码赫婆魂灾洪腿胆津俗辩胸晓劲贫仁偶辑邦恢赖圈摸仰润堆碰艇稍迟辆废净凶署壁御奉旋冬矿抬蛋晨伏吹鸡倍糊秦盾杯租骑乏隆诊奴摄丧污渡旗甘耐凭扎抢绪粗肩梁幻菲皆碎宙叔岩荡综爬荷悉蒂返井壮薄悄扫敏碍殖详迪矛霍允幅撒剩凯颗骂赏液番箱贴漫酸郎腰舒眉忧浮辛恋餐吓挺励辞艘键伍峰尺昨黎辈贯侦滑券崇扰宪绕趋慈乔阅汗枝拖墨胁插箭腊粉泥氏彭拔骗凤慧媒佩愤扑龄驱惜豪掩兼跃尸肃帕驶堡届欣惠册储飘桑闲惨洁踪勃宾频仇磨递邪撞拟滚奏巡颜剂绩贡疯坡瞧截燃焦殿伪柳锁逼颇昏劝呈搜勤戒驾漂饮曹朵仔柔俩孟腐幼践籍牧凉牲佳娜浓芳稿竹腹跌逻垂遵脉貌柏狱猜怜惑陶兽帐饰贷昌叙躺钢沟寄扶铺邓寿惧询汤盗肥尝匆辉奈扣廷澳嘛董迁凝慰厌脏腾幽怨鞋丢埋泉涌辖躲晋紫艰魏吾慌祝邮吐狠鉴曰械咬邻赤挤弯椅陪割揭韦悟聪雾锋梯猫祥阔" +
    "這來個們時說會為國學實現當對開關問題點還進發後裡樣種經麼動過從無與長機頭體業東車門見聽話讓給邊處親愛習書寫讀語詞彙資訊網絡電腦軟體硬體資料數據檔案測編碼檔開啟關閉轉換預覽圖標選擇刪除複製貼上重新命名資料夾視窗欄" +
    " ").split(""));
  var KR_SET = new Set(("이다는을를의에가나고하한서시스테어리도자기지사로정수인대있니요아주우게되면것그저무보국학생회원일상제장경비전성동문위소조계관광물공개활화습험들습" +
    " ").split(""));
  /* frequent Cyrillic letters, used to keep windows-1251 from losing to CJK */
  var RU_SET = new Set("оеаинтсрвлкмдпуяыьгзбчйхжшюцщэфъё".split(""));

  function commonRatio(s, set) {
    var n = Math.min(s.length, 4000), hit = 0;
    for (var i = 0; i < n; i++) if (set.has(s.charAt(i))) hit++;
    return n ? hit / n : 0;
  }
  /* share of plain ASCII — high for Western single-byte text */
  function asciiRatio(s) {
    var n = Math.min(s.length, 4000), hit = 0;
    for (var i = 0; i < n; i++) {
      var c = s.charCodeAt(i);
      if (c >= 0x20 && c < 0x7F) hit++;
    }
    return n ? hit / n : 0;
  }

  function noiseScore(s) {
    var bad = 0, n = Math.min(s.length, 4000);
    for (var i = 0; i < n; i++) {
      var c = s.charCodeAt(i);
      if (c === 0xFFFD) bad += 12;
      else if (c < 9 || (c > 13 && c < 32)) bad += 6;
      else if (c >= 0x7F && c <= 0x9F) bad += 4;
      else if (c >= 0xE000 && c <= 0xF8FF) bad += 2;
    }
    return bad;
  }
  function fullwidthKanaRatio(s) {
    var n = Math.min(s.length, 4000), k = 0;
    for (var i = 0; i < n; i++) {
      var c = s.charCodeAt(i);
      if (c >= 0x3040 && c <= 0x30FF) k++;
    }
    return n ? k / n : 0;
  }

  /* Auto-detects the encoding of raw bytes:
   *   BOM (UTF-8 / UTF-16 LE/BE) → BOM-less UTF-16 (NUL parity) →
   *   strict UTF-8 → GB18030 (GBK/GB2312) vs Big5 by replacement chars. */
  function decodeBytes(buf, forced) {
    var b = new Uint8Array(buf);
    if (forced && forced !== "auto") {
      var forcedText = tryDecode(buf, forced);
      if (forcedText != null) return { text: forcedText, enc: forced };
    }
    /* 1) byte order marks (TextDecoder drops the BOM itself) */
    if (b.length >= 3 && b[0] === 0xEF && b[1] === 0xBB && b[2] === 0xBF)
      return { text: tryDecode(buf, "utf-8"), enc: "utf-8" };
    if (b.length >= 2 && b[0] === 0xFF && b[1] === 0xFE)
      return { text: tryDecode(buf, "utf-16le"), enc: "utf-16le" };
    if (b.length >= 2 && b[0] === 0xFE && b[1] === 0xFF)
      return { text: tryDecode(buf, "utf-16be"), enc: "utf-16be" };
    /* 2) BOM-less UTF-16: NUL bytes concentrate on one parity */
    var n = Math.min(b.length, 4096), evenNul = 0, oddNul = 0;
    for (var i = 0; i < n; i++) {
      if (b[i] !== 0) continue;
      if (i % 2) oddNul++; else evenNul++;
    }
    var pairs = n / 2;
    if (pairs > 4 && oddNul > pairs * 0.3 && evenNul < pairs * 0.05)
      return { text: tryDecode(buf, "utf-16le"), enc: "utf-16le" };
    if (pairs > 4 && evenNul > pairs * 0.3 && oddNul < pairs * 0.05)
      return { text: tryDecode(buf, "utf-16be"), enc: "utf-16be" };
    /* 3) strict UTF-8 (the common case) */
    try {
      return { text: new TextDecoder("utf-8", { fatal: true }).decode(buf), enc: "utf-8" };
    } catch (e) { /* not valid UTF-8 */ }
    /* 3.5) Japanese Shift-JIS: hiragana/katakana use 0x82/0x83 lead bytes and
     * decode to full-width kana, while the same bytes read as GBK give rare
     * ideographs or half-width katakana — a reliable discriminator. */
    var sjisLead = 0;
    for (var si = 0; si < n; si++) if (b[si] === 0x82 || b[si] === 0x83) sjisLead++;
    if (sjisLead > n * 0.08) {
      var sj = tryDecode(buf, "shift_jis");
      if (sj && fullwidthKanaRatio(sj) > 0.1) return { text: sj, enc: "shift_jis" };
    }
    /* 4) legacy encodings: score = noise − script confidence.
     *    Confidence is the hit ratio against frequent characters of each
     *    script, normalised by that script's typical baseline, so a genuine
     *    Korean decode (≈0.31 of a small syllable set) beats a Chinese
     *    mis-decode of the same bytes. */
    var high = 0;
    for (var hi = 0; hi < n; hi++) if (b[hi] >= 0x80) high++;
    var highRatio = n ? high / n : 0;
    var candidates = ["gb18030", "big5", "shift_jis", "euc-kr",
                      "windows-1251", "windows-1252"];
    var best = null;
    for (var ci = 0; ci < candidates.length; ci++) {
      var enc = candidates[ci];
      var txt = tryDecode(buf, enc);
      if (txt == null) continue;
      var conf;
      if (enc === "euc-kr") conf = commonRatio(txt, KR_SET) / 0.40;
      else if (enc === "shift_jis") conf = fullwidthKanaRatio(txt) / 0.35;
      else if (enc === "windows-1251") conf = commonRatio(txt, RU_SET) / 0.45;
      else if (enc === "windows-1252") {
        conf = asciiRatio(txt) / 0.85;
        if (highRatio > 0.3) conf -= 0.15;      /* dense 8-bit data is not Latin-1 */
      } else conf = commonRatio(txt, CJK_SET) / 0.45;
      if (highRatio < 0.15 && enc !== "windows-1251" && enc !== "windows-1252")
        conf *= 0.2;                            /* mostly ASCII → not CJK */
      var score = noiseScore(txt) - conf * 100;
      if (!best || score < best.score) best = { text: txt, enc: enc, score: score };
    }
    if (best) return { text: best.text, enc: best.enc };
    return { text: "", enc: "utf-8" };
  }

  P._encodingSelectHtml = function (ext) {
    var saved = "auto";
    try { saved = localStorage.getItem("fmnet-enc-" + ext) || "auto"; } catch (e) {}
    var self = this;
    var opts = TEXT_ENCODINGS.map(function (en) {
      var label = en[1] === "encAuto" ? self.t("encAuto") : en[1];
      return '<option value="' + en[0] + '"' + (saved === en[0] ? " selected" : "") + '>' +
             esc(label) + '</option>';
    }).join("");
    return '<span class="fm-enc-wrap"><select class="fm-enc-select" title="' + esc(this.t("encoding")) + '">' +
           opts + '</select><span class="fm-enc-detected"></span></span>';
  };

  /* ---------------------------------------------------------------- toast */
  var TOAST_ICONS = {
    ok: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#2E7D32"/>' +
      '<path d="m7.6 12.4 3 3 5.8-6.4" fill="none" stroke="#fff" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    error: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#C62828"/>' +
      '<path d="M8.4 8.4 15.6 15.6M15.6 8.4 8.4 15.6" fill="none" stroke="#fff" stroke-width="2.1" stroke-linecap="round"/></svg>',
    info: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#1565C0"/>' +
      '<path d="M12 10.6v6.2" stroke="#fff" stroke-width="2.1" stroke-linecap="round"/>' +
      '<circle cx="12" cy="7.7" r="1.25" fill="#fff"/></svg>'
  };

  P.toast = function (msg, type, detail) {
    var holder = document.querySelector(".fm-toasts");
    if (!holder) {
      holder = document.createElement("div");
      holder.className = "fm-toasts";
      document.body.appendChild(holder);
    }
    var kind = type || "info";
    var el = document.createElement("div");
    el.className = "fm-toast " + kind;
    el.innerHTML = '<span class="fm-toast-icon">' + (TOAST_ICONS[kind] || TOAST_ICONS.info) + '</span>' +
      '<div class="fm-toast-body"><div class="fm-toast-title"></div>' +
      (detail ? '<div class="fm-toast-detail"></div>' : '') + '</div>' +
      '<button class="fm-toast-close" aria-label="' + esc(this.t("close")) + '">&times;</button>';
    el.querySelector(".fm-toast-title").textContent = msg;
    if (detail) el.querySelector(".fm-toast-detail").textContent = detail;
    holder.appendChild(el);
    var life = kind === "error" ? 8000 : kind === "ok" ? 4000 : 3000;
    var timer = setTimeout(function () { el.remove(); }, life);
    el.querySelector(".fm-toast-close").onclick = function () { clearTimeout(timer); el.remove(); };
  };

  core.uiReady = true;
})();
