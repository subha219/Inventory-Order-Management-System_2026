// Light / dark switch. The saved choice is applied in <head> (see __css.cshtml) so there is no flash;
// this file wires up the toggle button and swaps the Syncfusion theme stylesheet.
(function () {
    function isDark() { return document.documentElement.getAttribute('data-theme') === 'dark'; }

    function paintButton() {
        var icon = document.querySelector('#themeToggle i');
        var btn = document.getElementById('themeToggle');
        if (icon) icon.className = isDark() ? 'fas fa-sun' : 'fas fa-moon';
        if (btn) {
            var label = isDark() ? 'Switch to light mode' : 'Switch to dark mode';
            btn.setAttribute('title', label);
            btn.setAttribute('aria-label', label);
        }
    }

    function apply(dark) {
        var root = document.documentElement;
        root.setAttribute('data-theme', dark ? 'dark' : 'light');
        root.classList.toggle('dark-mode', dark);
        var sf = document.getElementById('sf-theme');
        if (sf) sf.href = '/lib/syncfusion/css/' + (dark ? 'bootstrap5-dark' : 'bootstrap5') + '.css';
        try { localStorage.setItem('whms-theme', dark ? 'dark' : 'light'); } catch (e) { }
        paintButton();
    }

    document.addEventListener('DOMContentLoaded', function () {
        paintButton();
        var btn = document.getElementById('themeToggle');
        if (btn) btn.addEventListener('click', function () { apply(!isDark()); });
    });
})();

// List pages: show status words as pills and mark the Add / Delete toolbar buttons. Display only; grid data is untouched.
(function () {
    var STATUS = {
        draft: 'neutral', pending: 'warn', confirmed: 'good', approved: 'good', completed: 'good', paid: 'good',
        delivered: 'good', received: 'good', active: 'good', cancelled: 'bad', canceled: 'bad', rejected: 'bad',
        archived: 'muted', closed: 'muted', inactive: 'muted'
    };
    var timer = null;

    function enhance() {
        var cells = document.querySelectorAll('.e-grid .e-content .e-rowcell');
        for (var i = 0; i < cells.length; i++) {
            var td = cells[i];
            if (td.children.length) continue;
            var text = (td.textContent || '').trim();
            if (!text) continue;
            var kind = STATUS[text.toLowerCase()];
            if (kind) {
                var pill = document.createElement('span');
                pill.className = 'pill pill-' + kind;
                pill.textContent = text;
                td.textContent = '';
                td.appendChild(pill);
            } else if (/^-?[\d,]+\.\d{2}$/.test(text)) {
                td.classList.add('num-cell');
            } else if (/^\d{4}-\d{2}-\d{2}/.test(text)) {
                td.classList.add('date-cell');
            } else if (/^[0-9A-Z-]{8,}$/.test(text) && /\d/.test(text) && /[A-Z]/.test(text)) {
                td.classList.add('id-cell');
            }
        }
        var grids = document.querySelectorAll('.e-grid');
        for (var g = 0; g < grids.length; g++) {
            var grid = grids[g];
            var rows = grid.querySelectorAll('.e-content tr.e-row');
            if (!rows.length) continue;
            var headers = grid.querySelectorAll('.e-gridheader .e-headercell');
            var numeric = {};
            for (var r = 0; r < rows.length && r < 8; r++) {
                var rc = rows[r].querySelectorAll('.e-rowcell');
                for (var c = 0; c < rc.length; c++) if (rc[c].classList.contains('num-cell')) numeric[c] = true;
            }
            Object.keys(numeric).forEach(function (idx) {
                if (headers[idx]) headers[idx].classList.add('num-col');
                for (var r2 = 0; r2 < rows.length; r2++) {
                    var cell = rows[r2].querySelectorAll('.e-rowcell')[idx];
                    if (cell) cell.classList.add('num-col');
                }
            });
        }
        var items = document.querySelectorAll('.e-grid .e-toolbar .e-toolbar-item');
        for (var j = 0; j < items.length; j++) {
            var label = (items[j].textContent || '').trim().toLowerCase();
            if (label === 'add') items[j].classList.add('tb-add');
            else if (label === 'delete') items[j].classList.add('tb-del');
        }
    }

    function schedule() {
        clearTimeout(timer);
        timer = setTimeout(enhance, 80);
    }

    document.addEventListener('DOMContentLoaded', function () {
        new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
        schedule();
    });
})();
