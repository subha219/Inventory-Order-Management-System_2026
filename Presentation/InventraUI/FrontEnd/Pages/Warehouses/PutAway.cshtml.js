const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, search: '', receipts: [], selected: '', warehouseName: '', lines: [], history: [], qty: {}, insp: {}
        });
        const userMap = {};

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '-'),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            user: (id) => (id ? (userMap[id] || '') : ''),
            pct: (r) => (r.holdable > 0 ? Math.min(100, Math.round(r.putAway / r.holdable * 100)) : (r.received > 0 && r.putAway > 0 ? 100 : 0)),
            label: (r) => (r.putAway + 1e-9 >= r.holdable && r.putAway > 0 ? 'Put away' : r.putAway > 0 ? 'Partial' : r.inspected ? 'Inspected' : 'New'),
            badge: (r) => (r.putAway + 1e-9 >= r.holdable && r.putAway > 0 ? 'bg-success' : r.putAway > 0 ? 'bg-warning text-dark' : r.inspected ? 'bg-info text-dark' : 'bg-secondary'),
            current: () => state.receipts.find(r => r.id === state.selected),
            receipts: () => {
                const q = state.search.trim().toLowerCase();
                return state.receipts.filter(r => !q || (r.number || '').toLowerCase().includes(q) || (r.vendor || '').toLowerCase().includes(q));
            },
            total: () => Object.values(state.qty).reduce((a, v) => a + (Number(v) > 0 ? Number(v) : 0), 0)
        };

        const handler = {
            fetch: async (id) => {
                const res = await AxiosManager.get('/Receiving/GetReceiving?goodsReceiveId=' + encodeURIComponent(id || ''), {});
                const c = res?.data?.content ?? {};
                state.receipts = c.receipts ?? [];
                state.warehouseName = c.warehouseName || '';
                state.lines = c.lines ?? [];
                state.history = c.history ?? [];
                state.qty = {};
                state.insp = {};
                state.lines.forEach(l => {
                    state.insp[l.productId] = { passed: l.inspected ? l.passed : l.received, failed: l.failed || 0, reason: l.reason || '' };
                    l.locations.forEach(s => { if (s.suggested > 0) state.qty[l.productId + '|' + s.locationId] = s.suggested; });
                });
            },
            select: async (id) => {
                state.selected = id;
                try { await handler.fetch(id); }
                catch (e) { Swal.fire({ icon: 'error', title: 'Could not load the receipt', text: e?.response?.data?.message || e.message }); }
            },
            saveInspection: async () => {
                state.saving = true;
                try {
                    const lines = state.lines.map(l => ({ productId: l.productId, passed: Number(state.insp[l.productId].passed) || 0, failed: Number(state.insp[l.productId].failed) || 0, reason: state.insp[l.productId].reason }));
                    const saved = await AxiosManager.post('/Receiving/SaveInspection', { goodsReceiveId: state.selected, lines });
                    const note = saved?.data?.content?.scrapNote;
                    if (note) await Swal.fire({ icon: 'info', title: 'Inspection saved', text: note });
                    else await Swal.fire({ icon: 'success', title: 'Inspection saved', timer: 1200, showConfirmButton: false });
                    await handler.fetch(state.selected);
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not save the inspection', text: e?.response?.data?.message || e.message });
                } finally { state.saving = false; }
            },
            confirmPutAway: async () => {
                const lines = [];
                Object.keys(state.qty).forEach(k => {
                    const q = Number(state.qty[k]);
                    if (q > 0) { const [productId, locationId] = k.split('|'); lines.push({ productId, locationId, quantity: q }); }
                });
                state.saving = true;
                try {
                    await AxiosManager.post('/Receiving/ConfirmPutAway', { goodsReceiveId: state.selected, lines });
                    await Swal.fire({ icon: 'success', title: 'Put-away recorded', timer: 1200, showConfirmButton: false });
                    await handler.fetch(state.selected);
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not record the put-away', text: e?.response?.data?.message || e.message });
                } finally { state.saving = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                try {
                    const res = await AxiosManager.get('/Security/GetUserList', {});
                    (res?.data?.content?.data ?? []).forEach(u => { userMap[u.id] = ((u.firstName || '') + ' ' + (u.lastName || '')).trim() || u.email || u.id; });
                } catch (e) { console.error('user list error:', e); }
                await handler.fetch('');
            } catch (e) {
                console.error('page init error:', e);
            } finally {
                state.loading = false;
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
