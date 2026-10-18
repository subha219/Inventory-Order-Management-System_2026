const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, rows: [], products: 0, totalReserved: 0, totalIncoming: 0, overcommitted: 0, low: 0,
            filter: 'all', search: '',
            filters: [
                { key: 'all', label: 'All' }, { key: 'reserved', label: 'Has reservations' }, { key: 'incoming', label: 'Has incoming' },
                { key: 'attention', label: 'Needs attention' }
            ]
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            badge: (s) => ({ Ok: 'bg-success', Low: 'bg-warning text-dark', Short: 'bg-warning text-dark', Overcommitted: 'bg-danger' }[s] || 'bg-secondary'),
            rows: () => {
                const q = state.search.trim().toLowerCase();
                return state.rows.filter(r => {
                    if (state.filter === 'reserved' && !(r.reserved > 0)) return false;
                    if (state.filter === 'incoming' && !(r.incoming > 0)) return false;
                    if (state.filter === 'attention' && r.state === 'Ok') return false;
                    if (q && !((r.name || '').toLowerCase().includes(q) || (r.number || '').toLowerCase().includes(q))) return false;
                    return true;
                });
            }
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/StockAvailability/GetStockAvailability', {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.data ?? [];
                    state.products = c.products ?? 0;
                    state.totalReserved = c.totalReserved ?? 0;
                    state.totalIncoming = c.totalIncoming ?? 0;
                    state.overcommitted = c.overcommitted ?? 0;
                    state.low = c.low ?? 0;
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not load stock availability', text: e?.response?.data?.message || e.message });
                } finally {
                    state.loading = false;
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                await handler.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
