const App = {
    setup() {
        const pad = (n) => String(n).padStart(2, '0');
        const localInput = (d) => d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
        const emptyForm = () => ({ id: '', title: '', taskType: 'Pick', priority: 'Normal', assignedToId: '', warehouseId: '', due: '', reference: '', notes: '' });
        const state = Vue.reactive({
            loading: true, busy: false, days: 30, tab: 'mine', tabs: [{ key: 'mine', label: 'My tasks' }, { key: 'all', label: 'All open' }, { key: 'done', label: 'Finished' }, { key: 'people', label: 'People' }],
            types: ['Pick', 'PutAway', 'Count', 'Move', 'Receive', 'Pack', 'Other'], tasks: [], workers: [], warehouses: [], users: [], me: '', form: emptyForm()
        });
        const userMap = {};

        const methods = {
            user: (id) => userMap[id] || id,
            when: (d) => (d ? new Date(d).toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : ''),
            prio: (p) => ({ Low: 'bg-light text-dark', Normal: 'bg-primary', High: 'bg-warning text-dark', Urgent: 'bg-danger' }[p] || 'bg-secondary'),
            stat: (s) => ({ Open: 'bg-info', InProgress: 'bg-primary', Done: 'bg-success', Cancelled: 'bg-secondary' }[s] || 'bg-secondary'),
            list: () => {
                const rank = { Urgent: 0, High: 1, Normal: 2, Low: 3 };
                let l = state.tasks;
                if (state.tab === 'mine') l = l.filter(t => (t.status === 'Open' || t.status === 'InProgress') && (t.assignedToId === state.me || !t.assignedToId));
                else if (state.tab === 'all') l = l.filter(t => t.status === 'Open' || t.status === 'InProgress');
                else l = l.filter(t => t.status === 'Done' || t.status === 'Cancelled');
                return [...l].sort((a, b) => (rank[a.priority] ?? 9) - (rank[b.priority] ?? 9));
            }
        };
        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/Task/GetTasks?days=' + state.days, {});
                const c = res?.data?.content ?? {};
                state.tasks = c.tasks ?? []; state.workers = c.workers ?? []; state.warehouses = c.warehouses ?? [];
            },
            reset: () => { state.form = emptyForm(); },
            edit: (t) => {
                state.form = { id: t.id, title: t.title, taskType: t.taskType, priority: t.priority, assignedToId: t.assignedToId || '', warehouseId: t.warehouseId || '', due: t.dueAtUtc ? localInput(new Date(t.dueAtUtc)) : '', reference: t.reference || '', notes: t.notes || '' };
            },
            save: async () => {
                const f = state.form;
                if (!f.title.trim()) { Swal.fire({ icon: 'warning', title: 'Describe the task' }); return; }
                state.busy = true;
                try {
                    await AxiosManager.post('/Task/SaveTask', {
                        id: f.id || null, title: f.title, taskType: f.taskType, priority: f.priority, assignedToId: f.assignedToId || null, warehouseId: f.warehouseId || null,
                        dueAtUtc: f.due ? new Date(f.due).toISOString() : null, reference: f.reference, notes: f.notes
                    });
                    handler.reset(); await handler.fetch();
                } catch (e) { err('Could not save the task', e); } finally { state.busy = false; }
            },
            act: async (t, action) => {
                if (action === 'Cancel') {
                    const ask = await Swal.fire({ title: 'Cancel ' + t.number + '?', icon: 'question', showCancelButton: true });
                    if (!ask.isConfirmed) return;
                }
                try { await AxiosManager.post('/Task/Act', { id: t.id, action }); await handler.fetch(); } catch (e) { err('Could not update the task', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                try {
                    state.me = (typeof StorageManager !== 'undefined' && StorageManager.getUserId ? StorageManager.getUserId() : '') || '';
                    const u = await AxiosManager.get('/Security/GetUserList', {});
                    const list = u?.data?.content?.data ?? [];
                    list.forEach(x => { userMap[x.id] = ((x.firstName || '') + ' ' + (x.lastName || '')).trim() || x.email || x.id; });
                    state.users = list.map(x => ({ id: x.id, name: userMap[x.id] }));
                } catch (e) { }
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });
        return { state, methods, handler };
    }
};
Vue.createApp(App).mount('#app');
