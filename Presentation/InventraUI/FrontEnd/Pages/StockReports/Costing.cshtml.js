const App = {
    setup() {
        const state = Vue.reactive({
            tab: 'valuation', tabs: [{ key: 'valuation', label: 'Valuation' }, { key: 'landed', label: 'Landed costs' }],
            loading: true, busy: false, search: '', rows: [], totalAvg: 0, totalFifo: 0, receipts: [],
            form: { receiptId: '', description: '', amount: null, basis: 'Value' }
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '-'),
            rows: () => {
                const q = state.search.trim().toLowerCase();
                return state.rows.filter(r => !q || (r.productName || '').toLowerCase().includes(q) || (r.productNumber || '').toLowerCase().includes(q) || (r.groupName || '').toLowerCase().includes(q));
            }
        };

        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/Costing/GetCosting', {});
                const c = res?.data?.content ?? {};
                state.rows = c.rows ?? []; state.totalAvg = c.totalAverageValue ?? 0; state.totalFifo = c.totalFifoValue ?? 0; state.receipts = c.receipts ?? [];
            },
            save: async () => {
                const f = state.form;
                if (!f.receiptId || !(f.description || '').trim() || !(Number(f.amount) > 0)) { Swal.fire({ icon: 'warning', title: 'Choose a receipt, describe the cost and enter an amount' }); return; }
                state.busy = true;
                try {
                    await AxiosManager.post('/Costing/SaveLandedCost', { goodsReceiveId: f.receiptId, description: f.description, amount: Number(f.amount), basis: f.basis });
                    f.description = ''; f.amount = null;
                    await handler.fetch();
                } catch (e) { err('Could not add the cost', e); } finally { state.busy = false; }
            },
            remove: async (c) => {
                const ask = await Swal.fire({ title: 'Remove "' + c.description + '"?', icon: 'question', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Costing/DeleteLandedCost', { id: c.id }); await handler.fetch(); } catch (e) { err('Could not remove the cost', e); }
            },
            exportCsv: () => {
                const esc = (v) => '"' + String(v ?? '').replace(/"/g, '""') + '"';
                const head = ['Product number', 'Product', 'Group', 'On hand', 'Average cost', 'Average value', 'FIFO cost', 'FIFO value', 'Selling price', 'Margin %', 'Estimated'];
                const lines = methods.rows().map(r => [r.productNumber, r.productName, r.groupName, r.onHand, r.averageCost, r.averageValue, r.fifoCost, r.fifoValue, r.sellingPrice, r.marginPercent == null ? '' : Math.round(r.marginPercent * 100) / 100, r.estimated ? 'yes' : '']);
                const blob = new Blob(['﻿' + [head, ...lines].map(r => r.map(esc).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'stock-costing.csv'; a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['StockReports']);
                await SecurityManager.validateToken();
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
