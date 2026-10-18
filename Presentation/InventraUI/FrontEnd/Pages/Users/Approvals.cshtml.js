const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, rows: [], pendingCount: 0, status: 'Pending',
            settings: { enabled: true, unitsThreshold: 0, canApprove: false },
            tabs: [{ key: 'Pending', label: 'Pending' }, { key: 'Approved', label: 'Approved' }, { key: 'Rejected', label: 'Rejected' }, { key: 'All', label: 'All' }]
        });
        const userMap = {};

        const methods = {
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-'),
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            user: (id) => (id ? (userMap[id] || id.substring(0, 8) + '...') : 'System'),
            type: (t) => ({ PositiveAdjustment: 'Positive adjustment', NegativeAdjustment: 'Negative adjustment', Scrapping: 'Scrapping' }[t] || t),
            badge: (s) => ({ Pending: 'bg-warning text-dark', Approved: 'bg-success', Rejected: 'bg-danger' }[s] || 'bg-secondary')
        };

        const handler = {
            setStatus: async (key) => { state.status = key; await handler.load(); },
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Approval/GetApprovalRequestList?status=' + encodeURIComponent(state.status) + '&days=365', {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.data ?? [];
                    state.pendingCount = c.pendingCount ?? 0;
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not load approvals', text: e?.response?.data?.message || e.message });
                } finally {
                    state.loading = false;
                }
            },
            decide: async (r, approve) => {
                const ask = await Swal.fire({
                    title: (approve ? 'Approve ' : 'Reject ') + (r.entityNumber || 'request') + '?',
                    text: approve ? 'The document will be confirmed and stock will move.' : 'The document stays as it is and the requester is told no.',
                    input: 'text', inputPlaceholder: 'Note (optional)', showCancelButton: true,
                    confirmButtonText: approve ? 'Approve' : 'Reject', confirmButtonColor: approve ? '#198754' : '#dc3545'
                });
                if (!ask.isConfirmed) return;
                try {
                    await AxiosManager.post('/Approval/DecideApproval', { id: r.id, approve: approve, note: ask.value || '' });
                    Swal.fire({ icon: 'success', title: approve ? 'Approved' : 'Rejected', timer: 1200, showConfirmButton: false });
                    await handler.load();
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not save the decision', text: e?.response?.data?.message || e.message });
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                try {
                    const res = await AxiosManager.get('/Security/GetUserList', {});
                    (res?.data?.content?.data ?? []).forEach(u => {
                        userMap[u.id] = ((u.firstName || '') + ' ' + (u.lastName || '')).trim() || u.email || u.id;
                    });
                } catch (e) { console.error('user list error:', e); }
                try {
                    const s = await AxiosManager.get('/Approval/GetApprovalSettings', {});
                    Object.assign(state.settings, s?.data?.content ?? {});
                } catch (e) { console.error('settings error:', e); }
                await handler.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
