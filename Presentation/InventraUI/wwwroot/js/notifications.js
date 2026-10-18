/* Bell menu in the top bar: polls the notification list and shows it in a dropdown. */
(function boot() {
    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', boot, { once: true }); return; }
    var POLL_MS = 60000;
    var bell = document.getElementById('notifBell');
    if (!bell || typeof AxiosManager === 'undefined') return;
    var badge = document.getElementById('notifBadge');
    var list = document.getElementById('notifList');
    var markAll = document.getElementById('notifMarkAll');

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function ago(d) {
        if (!d) return '';
        var t = new Date(d.endsWith('Z') ? d : d + 'Z'), m = Math.round((Date.now() - t.getTime()) / 60000);
        if (m < 1) return 'just now';
        if (m < 60) return m + ' min ago';
        if (m < 1440) return Math.round(m / 60) + ' h ago';
        return Math.round(m / 1440) + ' d ago';
    }

    function render(c) {
        var rows = (c && c.data) || [], unread = (c && c.unreadCount) || 0;
        badge.textContent = unread > 99 ? '99+' : unread;
        badge.style.display = unread ? '' : 'none';
        if (!rows.length) { list.innerHTML = '<div class="notif-empty">Nothing needs your attention.</div>'; return; }
        list.innerHTML = rows.map(function (n) {
            return '<a class="notif-item sev-' + esc(n.severity) + (n.unread ? ' unread' : '') + '" href="' + esc(n.link || '#') + '" data-id="' + esc(n.id) + '">' +
                '<span class="notif-dot"></span><span class="notif-body"><b>' + esc(n.title) + '</b><small>' + esc(n.message) + '</small><em>' + esc(ago(n.at)) + '</em></span></a>';
        }).join('');
    }

    function load() {
        AxiosManager.get('/Automation/GetNotifications', {}).then(function (res) {
            render(res && res.data && res.data.content);
        }).catch(function () { /* the bell is a convenience; stay quiet when the call fails */ });
    }

    list.addEventListener('click', function (e) {
        var a = e.target.closest('.notif-item');
        if (!a) return;
        AxiosManager.post('/Automation/MarkNotificationsRead', { ids: [a.getAttribute('data-id')] }).catch(function () { });
    });
    markAll.addEventListener('click', function (e) {
        e.preventDefault(); e.stopPropagation();
        AxiosManager.post('/Automation/MarkNotificationsRead', { ids: [] }).then(load).catch(function () { });
    });

    load();
    setInterval(load, POLL_MS);
})();
