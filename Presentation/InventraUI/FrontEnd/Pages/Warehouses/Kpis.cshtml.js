const App = {
    setup() {
        const state = Vue.reactive({ loading: true, days: 30, kpis: [], series: [] });
        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 1 }),
            h: (v) => {
                const max = Math.max(1, ...state.series.map(d => Math.max(d.shipments, d.receipts)));
                return (v ? Math.max(4, Math.round(110 * v / max)) : 0) + 'px';
            }
        };
        const handler = {
            fetch: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Analytics/GetOperationsKpis?days=' + state.days, {});
                    const c = res?.data?.content ?? {};
                    state.kpis = c.kpis ?? []; state.series = c.days ?? [];
                } finally { state.loading = false; }
            }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['Warehouses']); await SecurityManager.validateToken(); await handler.fetch(); }
            catch (e) { console.error('page init error:', e); state.loading = false; }
        });
        return { state, methods, handler };
    }
};
Vue.createApp(App).mount('#app');
