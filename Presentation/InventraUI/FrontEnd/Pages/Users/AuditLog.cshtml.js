const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, rows: [], entityNames: [], users: [], truncated: false,
            days: 7, entityName: '', action: '', userId: '', search: '', open: ''
        });
        const userMap = {};

        const methods = {
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-'),
            user: (id) => (id ? (userMap[id] || id.substring(0, 8) + '...') : 'System'),
            badge: (a) => ({ Created: 'bg-success', Updated: 'bg-primary', Deleted: 'bg-danger' }[a] || 'bg-secondary'),
            parsed: (r) => {
                try {
                    const o = JSON.parse(r.changes || '{}');
                    return Object.keys(o).map(k => {
                        const v = o[k];
                        return (v && typeof v === 'object') ? { field: k, old: v.old ?? '', new: v.new ?? '' } : { field: k, old: '', new: v };
                    });
                } catch (e) { return []; }
            },
            summary: (r) => {
                const p = methods.parsed(r);
                if (!p.length) return '';
                if (r.action === 'Updated') return p.slice(0, 3).map(c => c.field).join(', ') + (p.length > 3 ? ' +' + (p.length - 3) : '');
                return p.length + ' field' + (p.length === 1 ? '' : 's');
            }
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const q = '?days=' + state.days + '&entityName=' + encodeURIComponent(state.entityName) + '&action=' + encodeURIComponent(state.action) +
                        '&userId=' + encodeURIComponent(state.userId) + '&search=' + encodeURIComponent(state.search);
                    const res = await AxiosManager.get('/AuditLog/GetAuditLogList' + q, {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.data ?? [];
                    state.truncated = !!c.truncated;
                    if (!state.entityName) state.entityNames = c.entityNames ?? state.entityNames;
                    state.open = '';
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not load the audit log', text: e?.response?.data?.message || e.message });
                } finally {
                    state.loading = false;
                }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const lines = [['When', 'User', 'Action', 'Record type', 'Record', 'Changes'].map(esc).join(',')];
                state.rows.forEach(r => lines.push([methods.when(r.timestamp), methods.user(r.userId), r.action, r.entityName, r.entityLabel || r.entityId, r.changes].map(esc).join(',')));
                const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = 'audit-log-' + new Date().toISOString().substring(0, 10) + '.csv';
                a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users', 'Auditor / Read-only']);
                await SecurityManager.validateToken();
                try {
                    const res = await AxiosManager.get('/Security/GetUserList', {});
                    (res?.data?.content?.data ?? []).forEach(u => {
                        const name = ((u.firstName || '') + ' ' + (u.lastName || '')).trim() || u.email || u.id;
                        userMap[u.id] = name;
                        state.users.push({ id: u.id, name });
                    });
                } catch (e) { console.error('user list error:', e); }
                await handler.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
