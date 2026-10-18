const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, days: 365, deadDays: 90, rows: [], s: {}, search: '', filter: 'all',
            filters: [{ key: 'all', label: 'All' }, { key: 'A', label: 'Class A' }, { key: 'B', label: 'Class B' }, { key: 'C', label: 'Class C' }, { key: 'Dead', label: 'Dead' }, { key: 'Slow', label: 'Slow' }, { key: 'No stock', label: 'No stock' }]
        });
        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 }),
            ago: (d) => (d == null ? '-' : d === 0 ? 'today' : d + ' d ago'),
            badge: (s) => ({ Fast: 'bg-success', Normal: 'bg-secondary', Slow: 'bg-warning text-dark', Dead: 'bg-danger', 'No stock': 'bg-dark' }[s] || 'bg-secondary'),
            rows: () => {
                const q = state.search.trim().toLowerCase();
                return state.rows.filter(r => {
                    if (q && !((r.productName || '').toLowerCase().includes(q) || (r.productNumber || '').toLowerCase().includes(q) || (r.groupName || '').toLowerCase().includes(q))) return false;
                    if (state.filter === 'all') return true;
                    if (['A', 'B', 'C'].includes(state.filter)) return r.abcClass === state.filter;
                    return r.status === state.filter;
                });
            }
        };
        const handler = {
            fetch: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Analytics/GetInventoryAnalysis?days=' + state.days + '&deadDays=' + state.deadDays, {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.rows ?? []; state.s = c.summary ?? {};
                } finally { state.loading = false; }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const head = ['Number', 'Product', 'Group', 'Class', 'Status', 'On hand', 'Units sold', 'Cover days', 'Turns per year', 'Days since last sale', 'Days since last in', 'Stock value', 'Suggestion'];
                const lines = methods.rows().map(r => [r.productNumber, r.productName, r.groupName, r.abcClass, r.status, r.onHand, r.unitsSold, r.coverDays, r.turnover, r.daysSinceLastSale, r.daysSinceLastIn, r.stockValue, r.hint]);
                const blob = new Blob(['﻿' + [head, ...lines].map(r => r.map(esc).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'abc-aging.csv'; a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['StockReports']); await SecurityManager.validateToken(); await handler.fetch(); }
            catch (e) { console.error('page init error:', e); state.loading = false; }
        });
        return { state, methods, handler };
    }
};
Vue.createApp(App).mount('#app');
