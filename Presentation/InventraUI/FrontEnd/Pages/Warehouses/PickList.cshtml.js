const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, search: '', orders: [], selected: '', warehouseName: '', lines: [], history: [], qty: {}, bqty: {}, view: 'product'
        });
        const userMap = {};

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '-'),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            user: (id) => (id ? (userMap[id] || '') : ''),
            pct: (o) => (o.needed > 0 ? Math.min(100, Math.round(o.picked / o.needed * 100)) : 0),
            label: (o) => (o.picked <= 0 ? 'To pick' : o.picked + 1e-9 >= o.needed ? 'Picked' : 'Partial'),
            badge: (o) => (o.picked <= 0 ? 'bg-secondary' : o.picked + 1e-9 >= o.needed ? 'bg-success' : 'bg-warning text-dark'),
            expiry: (b) => {
                if (!b.expiryDate) return 'No expiry';
                const d = new Date(b.expiryDate).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
                return b.expired ? d + ' (expired)' : d + (b.daysToExpiry != null && b.daysToExpiry <= 30 ? ' (' + b.daysToExpiry + 'd)' : '');
            },
            current: () => state.orders.find(o => o.id === state.selected),
            orders: () => {
                const q = state.search.trim().toLowerCase();
                return state.orders.filter(o => !q || (o.number || '').toLowerCase().includes(q) || (o.customer || '').toLowerCase().includes(q));
            },
            // the walking route: remaining pick rows grouped by zone, zones in order, locations in order within a zone
            route: () => {
                const zones = {};
                state.lines.filter(l => l.remaining > 0).forEach(l => l.sources.forEach(s => {
                    if (!(s.suggested > 0) && !(Number(state.qty[l.productId + '|' + s.locationId]) > 0)) return;
                    const z = s.zone || 'No zone';
                    (zones[z] = zones[z] || []).push({ key: l.productId + '|' + s.locationId, code: s.code, productName: l.productName, productNumber: l.productNumber, quantity: s.quantity });
                }));
                return Object.keys(zones).sort((a, b) => (a === 'No zone') - (b === 'No zone') || a.localeCompare(b)).map(z => {
                    const rows = zones[z].sort((a, b) => (a.code || '').localeCompare(b.code || ''));
                    return { zone: z, rows, stops: new Set(rows.map(r => r.code)).size, units: rows.reduce((a, r) => a + (Number(state.qty[r.key]) || 0), 0) };
                });
            },
            total: () => Object.values(state.qty).reduce((a, v) => a + (Number(v) > 0 ? Number(v) : 0), 0)
        };

        const handler = {
            fetch: async (id) => {
                const res = await AxiosManager.get('/PickList/GetPickList?deliveryOrderId=' + encodeURIComponent(id || ''), {});
                const c = res?.data?.content ?? {};
                state.orders = c.orders ?? [];
                state.warehouseName = c.warehouseName || '';
                state.lines = c.lines ?? [];
                state.history = c.history ?? [];
                state.qty = {};
                state.bqty = {};
                state.lines.forEach(l => (l.batches || []).forEach(b => { if (b.suggested > 0) state.bqty[l.productId + '|' + b.batchId] = b.suggested; }));
                state.lines.forEach(l => l.sources.forEach(s => { if (s.suggested > 0) state.qty[l.productId + '|' + s.locationId] = s.suggested; }));
            },
            select: async (id) => {
                state.selected = id;
                try { await handler.fetch(id); }
                catch (e) { Swal.fire({ icon: 'error', title: 'Could not load the pick list', text: e?.response?.data?.message || e.message }); }
            },
            print: () => {
                const esc = (v) => String(v ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
                const o = methods.current();
                const body = methods.route().map(z => '<h3>Zone ' + esc(z.zone) + '</h3><table><tr><th>Location</th><th>Product</th><th>Qty</th><th>Picked</th></tr>' +
                    z.rows.map(r => '<tr><td>' + esc(r.code) + '</td><td>' + esc(r.productName) + ' (' + esc(r.productNumber) + ')</td><td class="n">' + methods.n(Number(state.qty[r.key]) || 0) + '</td><td class="box"></td></tr>').join('') + '</table>').join('');
                const w = window.open('', '_blank');
                if (!w) { Swal.fire({ icon: 'info', title: 'Allow pop-ups to print the pick sheet' }); return; }
                w.document.write('<html><head><title>Pick sheet ' + esc(o?.number) + '</title><style>body{font-family:Arial,sans-serif;margin:24px}h2{margin:0}h3{margin:18px 0 6px;border-bottom:2px solid #000}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:5px 8px;text-align:left;font-size:13px}.n{text-align:right}.box{width:70px}</style></head><body><h2>Pick sheet ' +
                    esc(o?.number) + '</h2><div>' + esc(o?.customer || '') + ' &middot; ' + esc(state.warehouseName) + '</div>' + body + '</body></html>');
                w.document.close(); w.focus(); w.print();
            },
            confirm: async () => {
                const picks = [];
                Object.keys(state.qty).forEach(k => {
                    const q = Number(state.qty[k]);
                    if (q > 0) { const [productId, locationId] = k.split('|'); picks.push({ productId, locationId, quantity: q }); }
                });
                const batchPicks = [];
                Object.keys(state.bqty).forEach(k => {
                    const q = Number(state.bqty[k]);
                    if (q > 0) { const [productId, batchId] = k.split('|'); batchPicks.push({ productId, batchId, quantity: q }); }
                });
                state.saving = true;
                try {
                    await AxiosManager.post('/PickList/ConfirmPick', { deliveryOrderId: state.selected, picks, batchPicks });
                    await Swal.fire({ icon: 'success', title: 'Pick recorded', timer: 1200, showConfirmButton: false });
                    await handler.fetch(state.selected);
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not record the pick', text: e?.response?.data?.message || e.message });
                } finally {
                    state.saving = false;
                }
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
