const App = {
    setup() {
        const state = Vue.reactive({
            term: '', last: '', searched: false, loading: false,
            batches: [], shipments: [], customers: [], unlinked: [], totalShipped: 0, totalRemaining: 0
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
        };

        const handler = {
            search: async () => {
                const t = state.term.trim();
                if (t.length < 2) return;
                state.loading = true; state.searched = true; state.last = t;
                try {
                    const res = await AxiosManager.get('/Recall/GetRecallTrace?term=' + encodeURIComponent(t), {});
                    const c = res?.data?.content ?? {};
                    state.batches = c.batches ?? [];
                    state.shipments = c.shipments ?? [];
                    state.customers = c.customers ?? [];
                    state.unlinked = c.unlinked ?? [];
                    state.totalShipped = c.totalShipped ?? 0;
                    state.totalRemaining = c.totalRemaining ?? 0;
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not run the trace', text: e?.response?.data?.message || e.message });
                } finally { state.loading = false; }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const lines = [['Customer', 'Phone', 'Email', 'Address', 'Units', 'Deliveries', 'Last delivery'].map(esc).join(',')];
                state.customers.forEach(c => lines.push([c.customerName, c.phone, c.email, c.address, c.quantity, c.deliveries, c.lastDelivery ? methods.day(c.lastDelivery) : ''].map(esc).join(',')));
                const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = 'recall-' + state.last.replace(/[^A-Za-z0-9_-]+/g, '_') + '.csv';
                a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
