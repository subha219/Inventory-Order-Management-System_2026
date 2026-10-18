const App = {
    setup() {
        const state = Vue.reactive({
            loading: false, saving: false, warehouses: [], warehouseId: '', daysA: 30, daysB: 90, daysC: 180,
            items: [], selected: [], dueA: 0, dueB: 0, dueC: 0, neverCounted: 0, filter: 'due',
            filters: [{ key: 'due', label: 'Due' }, { key: 'A', label: 'Class A' }, { key: 'B', label: 'Class B' }, { key: 'C', label: 'Class C' }, { key: 'all', label: 'All' }]
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }),
            cls: (c) => ({ A: 'bg-danger', B: 'bg-warning text-dark', C: 'bg-secondary' }[c] || 'bg-secondary'),
            rows: () => state.items.filter(i => state.filter === 'all' ? true : state.filter === 'due' ? i.due : i.abcClass === state.filter)
        };

        const handler = {
            load: async () => {
                state.selected = [];
                if (!state.warehouseId) { state.items = []; return; }
                state.loading = true;
                try {
                    const q = '?warehouseId=' + encodeURIComponent(state.warehouseId) + '&daysA=' + (state.daysA || 30) + '&daysB=' + (state.daysB || 90) + '&daysC=' + (state.daysC || 180);
                    const res = await AxiosManager.get('/CycleCount/GetCycleCountPlan' + q, {});
                    const c = res?.data?.content ?? {};
                    state.items = c.items ?? [];
                    state.dueA = c.dueA ?? 0; state.dueB = c.dueB ?? 0; state.dueC = c.dueC ?? 0; state.neverCounted = c.neverCounted ?? 0;
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not load the plan', text: e?.response?.data?.message || e.message });
                } finally { state.loading = false; }
            },
            selectDue: () => { state.selected = methods.rows().filter(i => i.due).slice(0, 300).map(i => i.productId); },
            createSheet: async () => {
                state.saving = true;
                try {
                    const res = await AxiosManager.post('/CycleCount/CreateCycleCountSheet', {
                        warehouseId: state.warehouseId, productIds: state.selected, description: 'Cycle count'
                    });
                    const c = res?.data?.content ?? {};
                    const go = await Swal.fire({
                        icon: 'success', title: 'Count sheet ' + (c.number || '') + ' created', text: c.lines + ' products. Open it to enter the counted quantities.',
                        showCancelButton: true, confirmButtonText: 'Open Stock Counts', cancelButtonText: 'Stay here'
                    });
                    if (go.isConfirmed) window.location.href = '/StockCounts/StockCountList';
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not create the count sheet', text: e?.response?.data?.message || e.message });
                } finally { state.saving = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['StockCounts']);
                await SecurityManager.validateToken();
                const wh = await AxiosManager.get('/Warehouse/GetWarehouseList', {});
                state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
                if (state.warehouses.length === 1) { state.warehouseId = state.warehouses[0].id; await handler.load(); }
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
