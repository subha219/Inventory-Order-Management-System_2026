/* WHMS sidebar menu: grouped, searchable, pinnable, remembers open sections. */
(function () {
    var GROUP_ICON = {
        'dashboards': 'fa-tachometer-alt', 'sales': 'fa-shopping-bag', 'purchase': 'fa-truck-loading',
        'inventory': 'fa-boxes', 'utilities': 'fa-toolbox', 'membership': 'fa-user-shield',
        'profiles': 'fa-id-card', 'settings': 'fa-sliders-h'
    };
    var ITEM_ICON = {
        'role presets': 'fa-user-tag',
        'email settings': 'fa-envelope-open-text',
        'stock holds': 'fa-hand-paper',
        'tasks & labor': 'fa-tasks',
        'operations kpis': 'fa-tachometer-alt',
        'supplier scorecard': 'fa-star-half-alt',
        'abc & aging': 'fa-sort-amount-down',
        'partner portals': 'fa-handshake',
        'integrations': 'fa-plug',
        'dock schedule': 'fa-truck-loading',
        'kits & assembly': 'fa-boxes',
        'wave picking': 'fa-layer-group',
        'permissions': 'fa-user-lock',
        'role presets': 'fa-user-tag',
        'email settings': 'fa-envelope-open-text',
        'costing & landed cost': 'fa-coins',
        'bin moves & replenishment': 'fa-exchange-alt',
        'returns inspection': 'fa-undo-alt',
        'packing & shipping': 'fa-box-open',
        'my dashboard': 'fa-tachometer-alt',
        'default': 'fa-chart-pie', 'customer group': 'fa-layer-group', 'customer category': 'fa-tags', 'customer': 'fa-user-friends',
        'customer contact': 'fa-address-book', 'sales order': 'fa-file-invoice-dollar', 'sales report': 'fa-chart-line',
        'vendor group': 'fa-layer-group', 'vendor category': 'fa-tags', 'vendor': 'fa-industry', 'vendor contact': 'fa-address-book',
        'purchase order': 'fa-file-invoice', 'reorder suggestions': 'fa-cart-plus', 'purchase report': 'fa-chart-line',
        'unit measure': 'fa-ruler-combined', 'product group': 'fa-layer-group', 'product': 'fa-box', 'scan & labels': 'fa-barcode',
        'demand forecast': 'fa-chart-area', 'ask inventory': 'fa-comments', 'batches & expiry': 'fa-hourglass-half', 'stock availability': 'fa-layer-group', 'approvals': 'fa-check-double', 'pick list': 'fa-dolly', 'scheduled reports': 'fa-calendar-check', 'import data': 'fa-file-import', 'inventory insights': 'fa-chart-pie', 'automation': 'fa-bell', 'recall trace': 'fa-route', 'cycle counts': 'fa-clipboard-list', 'serial numbers': 'fa-barcode', 'backorders': 'fa-hourglass-half', 'receiving & put-away': 'fa-truck-loading', 'warehouse': 'fa-warehouse',
        'delivery order': 'fa-shipping-fast', 'sales return': 'fa-undo-alt', 'goods receive': 'fa-dolly', 'purchase return': 'fa-reply',
        'transfer out': 'fa-sign-out-alt', 'transfer in': 'fa-sign-in-alt', 'positive adjustment': 'fa-plus-circle',
        'negative adjustment': 'fa-minus-circle', 'scrapping': 'fa-trash-alt', 'stock count': 'fa-clipboard-check',
        'transaction report': 'fa-receipt', 'stock report': 'fa-chart-bar', 'movement reports': 'fa-exchange-alt',
        'todo': 'fa-tasks', 'todo item': 'fa-check-square', 'users': 'fa-users', 'roles': 'fa-user-lock',
        'my profile': 'fa-user-circle', 'my company': 'fa-building', 'tax': 'fa-percent', 'number sequence': 'fa-sort-numeric-down'
    };
    var NEW_ITEMS = ['email settings', 'role presets', 'stock holds', 'tasks & labor', 'operations kpis', 'supplier scorecard', 'abc & aging', 'partner portals', 'integrations', 'dock schedule', 'kits & assembly', 'wave picking', 'permissions', 'costing & landed cost', 'bin moves & replenishment', 'returns inspection', 'packing & shipping', 'reorder suggestions', 'scan & labels', 'demand forecast', 'ask inventory', 'batches & expiry', 'stock availability', 'approvals', 'pick list', 'receiving & put-away', 'backorders', 'serial numbers', 'cycle counts', 'recall trace', 'automation', 'import data', 'inventory insights', 'scheduled reports'];
    var OPEN_KEY = 'whms-nav-open', PIN_KEY = 'whms-nav-pins';

    function load(k) { try { return JSON.parse(localStorage.getItem(k) || 'null'); } catch (e) { return null; } }
    function save(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { } }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function key(n) { return String(n || '').toLowerCase(); }

    function render(host, items) {
        var path = location.pathname.replace(/\/+$/, '').toLowerCase();
        var seg = path.split('/')[1] || '';
        var groups = items.filter(function (i) { return !i.pid; });
        var kids = function (g) { return items.filter(function (i) { return i.pid === g.id && i.navURL; }); };
        var leaves = items.filter(function (i) { return i.pid && i.navURL; });

        // Active item: exact URL match first, otherwise first item in the same top-level section.
        var active = leaves.filter(function (i) { return i.navURL.replace(/\/+$/, '').toLowerCase() === path; })[0]
            || leaves.filter(function (i) { return (i.navURL.split('/')[1] || '').toLowerCase() === seg; })[0];

        var open = load(OPEN_KEY) || {};
        var pins = load(PIN_KEY) || [];
        if (active) open[active.pid] = true;

        function link(i, inPins) {
            var k = key(i.name), isActive = active && active.id === i.id, pinned = pins.indexOf(i.id) >= 0;
            return '<li class="wm-item' + (isActive ? ' is-active' : '') + '" data-id="' + esc(i.id) + '" data-text="' + esc(k) + '">' +
                '<a class="wm-link" href="' + esc(i.navURL) + '"><i class="fas ' + (ITEM_ICON[k] || 'fa-circle') + ' wm-ico"></i>' +
                '<span class="wm-text">' + esc(i.name) + '</span>' +
                (NEW_ITEMS.indexOf(k) >= 0 ? '<span class="wm-new">New</span>' : '') + '</a>' +
                '<button type="button" class="wm-pin' + (pinned ? ' on' : '') + '" data-pin="' + esc(i.id) + '" title="' + (pinned ? 'Unpin' : 'Pin to top') + '"><i class="fas fa-thumbtack"></i></button></li>';
        }

        var html = '<div class="wm-search"><i class="fas fa-search"></i><input type="search" id="wmSearch" placeholder="Search menu..." autocomplete="off" aria-label="Search menu"><kbd>/</kbd></div>';
        var pinned = pins.map(function (id) { return leaves.filter(function (l) { return l.id === id; })[0]; }).filter(Boolean);
        if (pinned.length) {
            html += '<div class="wm-section wm-pinned"><div class="wm-label"><i class="fas fa-thumbtack"></i> Pinned</div><ul class="wm-list">' +
                pinned.map(function (p) { return link(p, true); }).join('') + '</ul></div>';
        }
        groups.forEach(function (g) {
            var list = kids(g);
            if (!list.length) return;
            var isOpen = !!open[g.id], hasActive = active && active.pid === g.id;
            html += '<div class="wm-group' + (isOpen ? ' open' : '') + (hasActive ? ' has-active' : '') + '" data-gid="' + esc(g.id) + '">' +
                '<button type="button" class="wm-head"><span class="wm-gico"><i class="fas ' + (GROUP_ICON[key(g.name)] || 'fa-folder') + '"></i></span>' +
                '<span class="wm-gname">' + esc(g.name) + '</span><span class="wm-count">' + list.length + '</span><i class="fas fa-chevron-right wm-chev"></i></button>' +
                '<div class="wm-body"><ul class="wm-list">' + list.map(function (l) { return link(l); }).join('') + '</ul></div></div>';
        });
        html += '<div class="wm-empty" hidden>No menu items match.</div>';
        host.innerHTML = html;
        host.classList.add('wm-root');

        host.addEventListener('click', function (e) {
            var pin = e.target.closest('[data-pin]');
            if (pin) {
                e.preventDefault();
                var id = pin.getAttribute('data-pin'), ix = pins.indexOf(id);
                if (ix >= 0) pins.splice(ix, 1); else pins.push(id);
                save(PIN_KEY, pins);
                render(host, items);
                return;
            }
            var head = e.target.closest('.wm-head');
            if (head) {
                var g = head.parentElement;
                g.classList.toggle('open');
                open[g.getAttribute('data-gid')] = g.classList.contains('open');
                save(OPEN_KEY, open);
            }
        });

        var input = host.querySelector('#wmSearch');
        input.addEventListener('input', function () {
            var q = input.value.trim().toLowerCase(), any = false;
            host.classList.toggle('wm-searching', q.length > 0);
            host.querySelectorAll('.wm-group').forEach(function (g) {
                var shown = 0;
                g.querySelectorAll('.wm-item').forEach(function (li) {
                    var hit = !q || li.getAttribute('data-text').indexOf(q) >= 0;
                    li.hidden = !hit; if (hit) shown++;
                });
                g.hidden = shown === 0;
                if (shown) any = true;
            });
            host.querySelectorAll('.wm-pinned').forEach(function (s) {
                var shown = 0;
                s.querySelectorAll('.wm-item').forEach(function (li) { var hit = !q || li.getAttribute('data-text').indexOf(q) >= 0; li.hidden = !hit; if (hit) shown++; });
                s.hidden = shown === 0; if (shown) any = true;
            });
            host.querySelector('.wm-empty').hidden = any || !q;
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { var first = host.querySelector('.wm-item:not([hidden]) .wm-link'); if (first) first.click(); }
            if (e.key === 'Escape') { input.value = ''; input.dispatchEvent(new Event('input')); input.blur(); }
        });
        if (!window.__wmKey) {
            window.__wmKey = true;
            document.addEventListener('keydown', function (e) {
                var t = e.target && e.target.tagName;
                if (e.key === '/' && t !== 'INPUT' && t !== 'TEXTAREA' && t !== 'SELECT' && !(e.target && e.target.isContentEditable)) {
                    var el = document.getElementById('wmSearch');
                    if (el) { e.preventDefault(); el.focus(); }
                }
            });
        }
        var cur = host.querySelector('.wm-item.is-active');
        if (cur && cur.scrollIntoView) { try { cur.scrollIntoView({ block: 'nearest' }); } catch (e) { } }
    }

    window.WhmsMenu = { render: render };
})();
