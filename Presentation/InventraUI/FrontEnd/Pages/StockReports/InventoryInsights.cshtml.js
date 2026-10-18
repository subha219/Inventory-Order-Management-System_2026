const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, warehouses: [], warehouseId: '', slowDays: 90, rows: [], groups: [],
            totalValue: 0, totalUnits: 0, slowValue: 0, deadValue: 0, slowCount: 0, deadCount: 0,
            filter: 'all', search: '',
            filters: [{ key: 'all', label: 'All' }, { key: 'Slow', label: 'Slow-moving' }, { key: 'Dead', label: 'Never sold' }, { key: 'Active', label: 'Active' }]
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }),
            badge: (s) => ({ Active: 'bg-success', Slow: 'bg-warning text-dark', Dead: 'bg-danger' }[s] || 'bg-secondary'),
            pct: (g) => (state.groups.length && state.groups[0].value > 0 ? Math.max(2, Math.round(g.value / state.groups[0].value * 100)) : 0),
            rows: () => {
                const q = state.search.trim().toLowerCase();
                return state.rows.filter(r => (state.filter === 'all' || r.movement === state.filter) &&
                    (!q || ((r.productName || '') + ' ' + (r.productNumber || '') + ' ' + (r.groupName || '')).toLowerCase().includes(q)));
            }
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Import/GetInventoryInsights?warehouseId=' + encodeURIComponent(state.warehouseId) + '&slowDays=' + (state.slowDays || 90), {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.rows ?? []; state.groups = c.groups ?? [];
                    state.totalValue = c.totalValue ?? 0; state.totalUnits = c.totalUnits ?? 0;
                    state.slowValue = c.slowValue ?? 0; state.deadValue = c.deadValue ?? 0; state.slowCount = c.slowCount ?? 0; state.deadCount = c.deadCount ?? 0;
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not load the report', text: e?.response?.data?.message || e.message });
                } finally { state.loading = false; }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const lines = [['Product number', 'Product', 'Group', 'On hand', 'Unit price', 'Value', 'Last sold', 'Days since sold', 'Movement'].map(esc).join(',')];
                methods.rows().forEach(r => lines.push([r.productNumber, r.productName, r.groupName, r.onHand, r.unitPrice, r.value, r.lastSold ? methods.day(r.lastSold) : 'Never', r.daysSinceSold ?? '', r.movement].map(esc).join(',')));
                const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
                a.download = 'inventory-insights-' + new Date().toISOString().substring(0, 10) + '.csv'; a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            },
            print: () => window.print()
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['StockReports']);
                await SecurityManager.validateToken();
                try {
                    const wh = await AxiosManager.get('/Warehouse/GetWarehouseList', {});
                    state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
                } catch (e) { console.error(e); }
                await handler.load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
