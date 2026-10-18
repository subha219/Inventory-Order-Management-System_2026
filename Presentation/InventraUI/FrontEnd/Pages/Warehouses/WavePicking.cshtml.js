const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, busy: false, available: [], chosen: [], notes: '', waves: [], selected: '', waveNumber: '', waveStatus: '', orders: [], rows: [], shortfalls: [], qty: {}
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '-'),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            badge: (s) => ({ Open: 'bg-info text-dark', Picked: 'bg-success', Cancelled: 'bg-secondary' }[s] || 'bg-secondary'),
            total: () => Object.values(state.qty).reduce((a, v) => a + (Number(v) > 0 ? Number(v) : 0), 0),
            zones: () => {
                const z = {};
                state.rows.forEach(r => { const k = r.zone || 'No zone'; (z[k] = z[k] || []).push(r); });
                return Object.keys(z).sort((a, b) => (a === 'No zone') - (b === 'No zone') || a.localeCompare(b)).map(k => ({ zone: k, rows: z[k] }));
            }
        };

        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async (id) => {
                const res = await AxiosManager.get('/Wave/GetWaves?waveId=' + encodeURIComponent(id || ''), {});
                const c = res?.data?.content ?? {};
                state.available = c.available ?? [];
                state.chosen = state.chosen.filter(x => state.available.some(a => a.id === x));
                state.waves = c.waves ?? [];
                state.waveNumber = c.waveNumber || ''; state.waveStatus = c.waveStatus || '';
                state.orders = c.orders ?? []; state.rows = c.rows ?? []; state.shortfalls = c.shortfalls ?? [];
                const qty = {};
                state.rows.forEach(r => { qty[r.productId + '|' + r.locationId] = r.quantity; });
                state.qty = qty;
            },
            toggleAll: (on) => { state.chosen = on ? state.available.map(a => a.id) : []; },
            select: async (id) => {
                state.selected = id;
                try { await handler.fetch(id); } catch (e) { err('Could not load the wave', e); }
            },
            create: async () => {
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/Wave/CreateWave', { deliveryOrderIds: state.chosen, notes: state.notes });
                    state.chosen = []; state.notes = '';
                    state.selected = res?.data?.content?.id || '';
                    await handler.fetch(state.selected);
                } catch (e) { err('Could not create the wave', e); } finally { state.busy = false; }
            },
            cancel: async () => {
                const ask = await Swal.fire({ title: 'Cancel ' + state.waveNumber + '?', text: 'Its orders go back to the list of orders waiting for a wave.', icon: 'question', showCancelButton: true, confirmButtonText: 'Cancel wave' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Wave/CancelWave', { waveId: state.selected }); await handler.fetch(state.selected); } catch (e) { err('Could not cancel', e); }
            },
            confirm: async () => {
                const picks = Object.keys(state.qty).filter(k => Number(state.qty[k]) > 0).map(k => { const [productId, locationId] = k.split('|'); return { productId, locationId, quantity: Number(state.qty[k]) }; });
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/Wave/ConfirmWavePick', { waveId: state.selected, picks });
                    const c = res?.data?.content ?? {};
                    await handler.fetch(state.selected);
                    Swal.fire({ icon: 'success', title: 'Pick recorded', text: methods.n(c.units) + ' units shared over ' + c.orders + ' order(s).' + (c.done ? ' The wave is complete.' : ' Some units are still to pick.'), timer: 2500, showConfirmButton: false });
                } catch (e) {
                    err('Could not record the pick', e);
                    try { await handler.fetch(state.selected); } catch (x) { }
                } finally { state.busy = false; }
            },
            print: () => {
                const esc = (v) => String(v ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
                const body = methods.zones().map(z => '<h3>Zone ' + esc(z.zone) + '</h3><table><tr><th>Location</th><th>Product</th><th>For orders</th><th>Qty</th><th>Picked</th></tr>' +
                    z.rows.map(r => '<tr><td>' + esc(r.code) + '</td><td>' + esc(r.productName) + ' (' + esc(r.productNumber) + ')</td><td>' + esc(r.allocations.map(a => a.orderNumber + ' x' + methods.n(a.quantity)).join(', ')) +
                        '</td><td class="n">' + methods.n(Number(state.qty[r.productId + '|' + r.locationId]) || 0) + '</td><td class="box"></td></tr>').join('') + '</table>').join('');
                const w = window.open('', '_blank');
                if (!w) { Swal.fire({ icon: 'info', title: 'Allow pop-ups to print the sheet' }); return; }
                w.document.write('<html><head><title>Wave ' + esc(state.waveNumber) + '</title><style>body{font-family:Arial,sans-serif;margin:24px}h2{margin:0}h3{margin:18px 0 6px;border-bottom:2px solid #000}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:5px 8px;text-align:left;font-size:13px}.n{text-align:right}.box{width:70px}</style></head><body><h2>Pick wave ' +
                    esc(state.waveNumber) + '</h2><div>' + state.orders.map(o => esc(o.number)).join(', ') + '</div>' + body + '</body></html>');
                w.document.close(); w.focus(); w.print();
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                await handler.fetch('');
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
