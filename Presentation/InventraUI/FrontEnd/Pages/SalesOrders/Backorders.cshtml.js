const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, lines: [], products: [], ordersAffected: 0, linesBackordered: 0, unitsShort: 0, oldestDays: 0,
            filter: 'short', search: '',
            filters: [{ key: 'short', label: 'Short' }, { key: 'Backordered', label: 'Nothing to ship' }, { key: 'Partial', label: 'Partly fillable' }, { key: 'Ready', label: 'Ready to ship' }, { key: 'all', label: 'All open lines' }]
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            badge: (s) => ({ Ready: 'bg-success', Partial: 'bg-warning text-dark', Backordered: 'bg-danger' }[s] || 'bg-secondary'),
            rows: () => {
                const q = state.search.trim().toLowerCase();
                return state.lines.filter(r => {
                    if (state.filter === 'short' && !(r.short > 0)) return false;
                    if (['Backordered', 'Partial', 'Ready'].includes(state.filter) && r.state !== state.filter) return false;
                    if (q && !((r.orderNumber || '') + ' ' + (r.customer || '') + ' ' + (r.productName || '') + ' ' + (r.productNumber || '')).toLowerCase().includes(q)) return false;
                    return true;
                });
            }
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Backorder/GetBackorders', {});
                    const c = res?.data?.content ?? {};
                    state.lines = c.lines ?? [];
                    state.products = c.products ?? [];
                    state.ordersAffected = c.ordersAffected ?? 0;
                    state.linesBackordered = c.linesBackordered ?? 0;
                    state.unitsShort = c.unitsShort ?? 0;
                    state.oldestDays = c.oldestDays ?? 0;
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not load backorders', text: e?.response?.data?.message || e.message });
                } finally {
                    state.loading = false;
                }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const lines = [['Order', 'Customer', 'Days waiting', 'Product number', 'Product', 'Ordered', 'Delivered', 'Outstanding', 'Can ship now', 'Short', 'Covered by incoming', 'Status'].map(esc).join(',')];
                methods.rows().forEach(r => lines.push([r.orderNumber, r.customer, r.daysWaiting, r.productNumber, r.productName, r.ordered, r.delivered, r.outstanding, r.canShipNow, r.short, r.coveredByIncoming, r.state].map(esc).join(',')));
                const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = 'backorders-' + new Date().toISOString().substring(0, 10) + '.csv';
                a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['SalesOrders']);
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
