const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, rows: [], summary: [], deliveries: [], total: 0, truncated: false,
            products: [], warehouses: [], locations: [],
            search: '', status: '', productId: '',
            showRegister: false,
            reg: { productId: '', warehouseId: '', locationId: '', notes: '', text: '' },
            gen: { prefix: '', from: 1, to: 10, pad: 4 }
        });

        const fail = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '-'),
            statusText: (s) => ({ InStock: 'In stock', Shipped: 'Shipped', Returned: 'Returned', Scrapped: 'Scrapped' }[s] || s),
            badge: (s) => ({ InStock: 'bg-success', Shipped: 'bg-primary', Returned: 'bg-info text-dark', Scrapped: 'bg-secondary' }[s] || 'bg-secondary'),
            parsed: () => state.reg.text.split(/[\r\n,;]+/).map(s => s.trim()).filter(s => s.length)
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const q = '?search=' + encodeURIComponent(state.search) + '&status=' + encodeURIComponent(state.status) + '&productId=' + encodeURIComponent(state.productId);
                    const res = await AxiosManager.get('/ProductSerial/GetSerialList' + q, {});
                    const c = res?.data?.content ?? {};
                    state.rows = c.data ?? [];
                    state.summary = c.summary ?? [];
                    state.deliveries = c.deliveries ?? [];
                    state.total = c.total ?? 0;
                    state.truncated = !!c.truncated;
                } catch (e) { fail('Could not load serial numbers', e); } finally { state.loading = false; }
            },
            loadLocations: async () => {
                state.reg.locationId = '';
                state.locations = [];
                if (!state.reg.warehouseId) return;
                try {
                    const res = await AxiosManager.get('/StorageLocation/GetStorageLocationList?warehouseId=' + encodeURIComponent(state.reg.warehouseId), {});
                    state.locations = (res?.data?.content?.data ?? []).filter(l => l.isActive);
                } catch (e) { console.error(e); }
            },
            generate: () => {
                const g = state.gen;
                if (!(g.to >= g.from) || g.to - g.from > 1999) { Swal.fire({ icon: 'warning', title: 'Check the range', text: 'Use a range of up to 2000 numbers.' }); return; }
                const out = [];
                for (let i = g.from; i <= g.to; i++) out.push((g.prefix || '') + String(i).padStart(g.pad || 1, '0'));
                state.reg.text = out.join('\n');
            },
            register: async () => {
                const r = state.reg;
                if (!r.productId || !r.warehouseId) { Swal.fire({ icon: 'warning', title: 'Choose a product and a warehouse' }); return; }
                state.saving = true;
                try {
                    const res = await AxiosManager.post('/ProductSerial/RegisterSerials', {
                        productId: r.productId, warehouseId: r.warehouseId, locationId: r.locationId, notes: r.notes, serials: methods.parsed()
                    });
                    await Swal.fire({ icon: 'success', title: (res?.data?.content?.count ?? 0) + ' serial numbers registered', timer: 1400, showConfirmButton: false });
                    state.reg.text = '';
                    state.showRegister = false;
                    await handler.load();
                } catch (e) { fail('Could not register the serial numbers', e); } finally { state.saving = false; }
            },
            call: async (url, body, ok) => {
                try {
                    await AxiosManager.post(url, body);
                    Swal.fire({ icon: 'success', title: ok, timer: 1000, showConfirmButton: false });
                    await handler.load();
                } catch (e) { fail('Could not save the change', e); }
            },
            ship: async (r) => {
                const options = {};
                state.deliveries.filter(d => d.productIds.includes(r.productId)).forEach(d => { options[d.id] = d.number + (d.customer ? ' - ' + d.customer : ''); });
                if (!Object.keys(options).length) { Swal.fire({ icon: 'info', title: 'No delivery order carries this product', text: 'Create the delivery order first, then ship the serial number on it.' }); return; }
                const ask = await Swal.fire({ title: 'Ship ' + r.serialNumber, input: 'select', inputOptions: options, inputPlaceholder: 'Choose the delivery order', showCancelButton: true, confirmButtonText: 'Ship' });
                if (!ask.isConfirmed || !ask.value) return;
                await handler.call('/ProductSerial/ChangeSerialStatus', { id: r.id, status: 'Shipped', deliveryOrderId: ask.value }, 'Shipped');
            },
            setStatus: async (r, status) => {
                if (status === 'Scrapped') {
                    const ask = await Swal.fire({ title: 'Scrap ' + r.serialNumber + '?', input: 'text', inputPlaceholder: 'Reason (optional)', showCancelButton: true, confirmButtonText: 'Scrap', confirmButtonColor: '#dc3545' });
                    if (!ask.isConfirmed) return;
                    await handler.call('/ProductSerial/ChangeSerialStatus', { id: r.id, status, notes: ask.value || '' }, 'Scrapped');
                    return;
                }
                await handler.call('/ProductSerial/ChangeSerialStatus', { id: r.id, status }, 'Updated');
            },
            remove: async (r) => {
                const ask = await Swal.fire({ title: 'Delete ' + r.serialNumber + '?', text: 'Use this for mistyped serial numbers only.', icon: 'warning', showCancelButton: true, confirmButtonText: 'Delete', confirmButtonColor: '#dc3545' });
                if (!ask.isConfirmed) return;
                await handler.call('/ProductSerial/DeleteSerial', { id: r.id }, 'Deleted');
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                try {
                    const [wh, pr] = await Promise.all([AxiosManager.get('/Warehouse/GetWarehouseList', {}), AxiosManager.get('/Product/GetProductList', {})]);
                    state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
                    state.products = (pr?.data?.content?.data ?? []).filter(p => p.physical !== false);
                } catch (e) { console.error('lookup error:', e); }
                await handler.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
