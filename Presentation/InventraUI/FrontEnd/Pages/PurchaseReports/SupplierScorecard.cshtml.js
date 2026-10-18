const App = {
    setup() {
        const state = Vue.reactive({ loading: true, days: 365, rows: [] });
        const methods = { n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 }) };
        const handler = {
            fetch: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Analytics/GetSupplierScorecard?days=' + state.days, {});
                    state.rows = res?.data?.content?.rows ?? [];
                } finally { state.loading = false; }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const head = ['Vendor', 'Grade', 'Score', 'Orders', 'Spend', 'Ordered', 'Received', 'Fill rate %', 'Open units', 'Returned', 'Return rate %', 'Lead days'];
                const lines = state.rows.map(r => [r.vendorName, r.grade, r.score, r.orders, r.spend, r.ordered, r.received, r.fillRate, r.open, r.returned, r.returnRate, r.leadDays]);
                const blob = new Blob(['﻿' + [head, ...lines].map(r => r.map(esc).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'supplier-scorecard.csv'; a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };
        Vue.onMounted(async () => {
            try { await SecurityManager.authorizePage(['PurchaseReports']); await SecurityManager.validateToken(); await handler.fetch(); }
            catch (e) { console.error('page init error:', e); state.loading = false; }
        });
        return { state, methods, handler };
    }
};
Vue.createApp(App).mount('#app');
