const App = {
    setup() {
        const emptyForm = () => ({ id: '', productId: '', warehouseId: '', batchNumber: '', manufactureDate: '', expiryDate: '', quantity: null, notes: '' });
        const state = Vue.reactive({
            loading: true,
            saving: false,
            expiringDays: 30,
            items: [],
            products: [],
            warehouses: [],
            summary: { batchCount: 0, expiredBatches: 0, expiringBatches: 0, expiredUnits: 0, expiringUnits: 0 },
            filter: 'all',
            filters: [
                { key: 'all', label: 'All' },
                { key: 'Expired', label: 'Expired' },
                { key: 'ExpiringSoon', label: 'Expiring soon' },
                { key: 'Ok', label: 'OK' }
            ],
            search: '',
            form: emptyForm()
        });
        const modalRef = Vue.ref(null);
        let modal = null;

        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
        const toInput = (d) => (d ? String(d).substring(0, 10) : '');
        const methods = {
            num: (v) => nf.format(v || 0),
            date: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '-'),
            days: (n) => (n < 0 ? Math.abs(n) + ' days ago' : n === 0 ? 'today' : 'in ' + n + ' days'),
            label: (s) => ({ Expired: 'Expired', ExpiringSoon: 'Expiring soon', Ok: 'OK', NoExpiry: 'No expiry' }[s] || s),
            badge: (s) => ({ Expired: 'bg-danger', ExpiringSoon: 'bg-warning text-dark', Ok: 'bg-success', NoExpiry: 'bg-secondary' }[s] || 'bg-secondary')
        };

        const filtered = Vue.computed(() => {
            const q = state.search.trim().toLowerCase();
            return state.items.filter(b =>
                (state.filter === 'all' || b.status === state.filter) &&
                (!q || (b.productName || '').toLowerCase().includes(q) || (b.productNumber || '').toLowerCase().includes(q) || (b.batchNumber || '').toLowerCase().includes(q)));
        });

        const load = async () => {
            state.loading = true;
            try {
                const [batches, products, warehouses] = await Promise.all([
                    AxiosManager.get('/ProductBatch/GetProductBatchList?expiringDays=' + state.expiringDays, {}),
                    AxiosManager.get('/Product/GetProductList', {}),
                    AxiosManager.get('/Warehouse/GetWarehouseList', {})
                ]);
                const c = batches?.data?.content ?? {};
                state.items = c.data ?? [];
                state.summary = c.summary ?? state.summary;
                state.products = (products?.data?.content?.data ?? []).filter(p => p.physical !== false);
                state.warehouses = (warehouses?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
            } catch (e) {
                console.error(e);
                Swal.fire({ icon: 'error', title: 'Could not load batches', text: e?.response?.data?.message || e.message });
            } finally {
                state.loading = false;
            }
        };

        const handler = {
            openNew: () => { state.form = emptyForm(); modal.show(); },
            openEdit: (b) => {
                state.form = {
                    id: b.id, productId: b.productId || '', warehouseId: b.warehouseId || '', batchNumber: b.batchNumber || '',
                    manufactureDate: toInput(b.manufactureDate), expiryDate: toInput(b.expiryDate), quantity: b.quantity, notes: b.notes || ''
                };
                modal.show();
            },
            close: () => modal.hide(),
            save: async () => {
                const f = state.form;
                if (!f.productId) return Swal.fire({ icon: 'warning', title: 'Choose a product' });
                if (!f.batchNumber.trim()) return Swal.fire({ icon: 'warning', title: 'Enter a batch / lot number' });
                const body = {
                    productId: f.productId,
                    warehouseId: f.warehouseId || null,
                    batchNumber: f.batchNumber.trim(),
                    manufactureDate: f.manufactureDate || null,
                    expiryDate: f.expiryDate || null,
                    quantity: f.quantity === null || f.quantity === '' ? 0 : f.quantity,
                    notes: f.notes || null
                };
                state.saving = true;
                try {
                    if (f.id) {
                        await AxiosManager.post('/ProductBatch/UpdateProductBatch', { ...body, id: f.id, updatedById: StorageManager.getUserId() });
                    } else {
                        await AxiosManager.post('/ProductBatch/CreateProductBatch', { ...body, createdById: StorageManager.getUserId() });
                    }
                    modal.hide();
                    await load();
                    Swal.fire({ icon: 'success', title: 'Saved', timer: 1200, showConfirmButton: false });
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not save', text: e?.response?.data?.message || e.message });
                } finally {
                    state.saving = false;
                }
            },
            remove: async (b) => {
                const r = await Swal.fire({ icon: 'question', title: 'Delete batch ' + b.batchNumber + '?', showCancelButton: true, confirmButtonText: 'Delete' });
                if (!r.isConfirmed) return;
                try {
                    await AxiosManager.post('/ProductBatch/DeleteProductBatch', { id: b.id, deletedById: StorageManager.getUserId() });
                    await load();
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not delete', text: e?.response?.data?.message || e.message });
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                modal = new bootstrap.Modal(modalRef.value, { backdrop: 'static', keyboard: false });
                await load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler, filtered, modalRef };
    }
};

Vue.createApp(App).mount('#app');
