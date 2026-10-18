const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, busy: false, tab: 'active', tabs: [{ key: 'active', label: 'On hold' }, { key: 'released', label: 'Released' }],
            holds: [], stock: [], warehouses: [], reasons: ['Quality check', 'Damaged', 'Recall', 'Customer dispute', 'Other'],
            form: { warehouseId: '', key: '', quantity: null, reason: 'Quality check', notes: '' }
        });
        const userMap = {};
        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : ''),
            user: (id) => userMap[id] || '',
            list: () => state.holds.filter(h => (state.tab === 'active') === (h.status === 'Active')),
            stockFor: () => state.stock.filter(s => s.warehouseId === state.form.warehouseId && s.onHand - s.held > 0)
        };
        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });
        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/StockHold/GetHolds', {});
                const c = res?.data?.content ?? {};
                state.holds = c.holds ?? []; state.stock = c.stock ?? []; state.warehouses = c.warehouses ?? [];
            },
            place: async () => {
                const f = state.form;
                if (!f.warehouseId || !f.key || !(Number(f.quantity) > 0)) { Swal.fire({ icon: 'warning', title: 'Choose the warehouse, product and units' }); return; }
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/StockHold/PlaceHold', { productId: f.key, warehouseId: f.warehouseId, quantity: Number(f.quantity), reason: f.reason, notes: f.notes });
                    f.key = ''; f.quantity = null; f.notes = '';
                    await handler.fetch();
                    Swal.fire({ icon: 'success', title: res?.data?.content?.message || 'Done', timer: 1400, showConfirmButton: false });
                } catch (e) { err('Could not place the hold', e); } finally { state.busy = false; }
            },
            release: async (h, all) => {
                let qty = null;
                if (!all) {
                    const ask = await Swal.fire({ title: 'Release how many units?', input: 'number', inputValue: h.quantity, showCancelButton: true });
                    if (!ask.isConfirmed) return;
                    qty = Number(ask.value);
                } else {
                    const ask = await Swal.fire({ title: 'Release all ' + methods.n(h.quantity) + ' units of ' + h.productName + '?', icon: 'question', showCancelButton: true, confirmButtonText: 'Release' });
                    if (!ask.isConfirmed) return;
                }
                try { await AxiosManager.post('/StockHold/ReleaseHold', { id: h.id, quantity: qty }); await handler.fetch(); } catch (e) { err('Could not release', e); }
            }
        };
        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                try {
                    const u = await AxiosManager.get('/Security/GetUserList', {});
                    (u?.data?.content?.data ?? []).forEach(x => { userMap[x.id] = ((x.firstName || '') + ' ' + (x.lastName || '')).trim() || x.email || x.id; });
                } catch (e) { }
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });
        return { state, methods, handler };
    }
};
Vue.createApp(App).mount('#app');
