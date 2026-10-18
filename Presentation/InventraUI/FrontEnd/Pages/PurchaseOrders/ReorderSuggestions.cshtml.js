const App = {
    setup() {
        const state = Vue.reactive({
            loading: true,
            saving: false,
            emailing: false,
            items: [],
            vendors: [],
            taxes: [],
            vendorId: '',
            taxId: ''
        });

        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
        const methods = {
            num: (v) => nf.format(v || 0),
            money: (v) => nf.format(v || 0)
        };

        const selectedItems = Vue.computed(() => state.items.filter(r => r.selected && r.quantity > 0));
        const selectedCount = Vue.computed(() => selectedItems.value.length);
        const selectedTotal = Vue.computed(() => selectedItems.value.reduce((s, r) => s + r.quantity * r.unitPrice, 0));
        const allSelected = Vue.computed(() => state.items.length > 0 && state.items.every(r => r.selected));

        const loadData = async () => {
            state.loading = true;
            try {
                const [sugg, vendors, taxes] = await Promise.all([
                    AxiosManager.get('/PurchaseOrder/GetReorderSuggestions', {}),
                    AxiosManager.get('/Vendor/GetVendorList', {}),
                    AxiosManager.get('/Tax/GetTaxList', {})
                ]);
                state.vendors = vendors?.data?.content?.data ?? [];
                state.taxes = taxes?.data?.content?.data ?? [];
                state.items = (sugg?.data?.content?.data ?? []).map(r => ({
                    ...r,
                    // Skip items that already have enough on a draft PO by default.
                    selected: r.onDraftOrders < r.suggestedQuantity,
                    quantity: r.suggestedQuantity
                }));
                if (state.taxes.length === 1) state.taxId = state.taxes[0].id;
            } catch (e) {
                console.error(e);
                Swal.fire({ icon: 'error', title: 'Could not load suggestions', text: e?.response?.data?.message || e.message });
            } finally {
                state.loading = false;
            }
        };

        const handler = {
            reload: loadData,
            emailNow: async () => {
                state.emailing = true;
                try {
                    const response = await AxiosManager.post('/LowStockAlert/SendNow', {});
                    const d = response?.data?.content;
                    Swal.fire({ icon: d?.sent ? 'success' : 'info', title: d?.sent ? 'Alert sent' : 'Not sent', text: d?.message ?? '' });
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not send the alert', text: e?.response?.data?.message || e.message });
                } finally {
                    state.emailing = false;
                }
            },
            toggleAll: (checked) => state.items.forEach(r => { r.selected = checked; }),
            createDraft: async () => {
                if (!state.vendorId || !state.taxId) {
                    Swal.fire({ icon: 'warning', title: 'Choose a vendor and a tax first' });
                    return;
                }
                if (selectedCount.value === 0) return;
                state.saving = true;
                try {
                    const response = await AxiosManager.post('/PurchaseOrder/CreateDraftFromReorder', {
                        vendorId: state.vendorId,
                        taxId: state.taxId,
                        createdById: StorageManager.getUserId(),
                        lines: selectedItems.value.map(r => ({ productId: r.productId, quantity: r.quantity }))
                    });
                    const data = response?.data?.content;
                    await Swal.fire({
                        icon: 'success',
                        title: 'Draft purchase order created',
                        text: `${data?.number ?? ''} with ${data?.lineCount ?? 0} items. Review it under Purchase Order.`,
                        confirmButtonText: 'Open Purchase Orders',
                        showCancelButton: true,
                        cancelButtonText: 'Stay here'
                    }).then(r => {
                        if (r.isConfirmed) window.location.href = '/PurchaseOrders/PurchaseOrderList';
                    });
                    await loadData();
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not create the purchase order', text: e?.response?.data?.message || e.message });
                } finally {
                    state.saving = false;
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['PurchaseOrders']);
                await SecurityManager.validateToken();
                await loadData();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, handler, methods, selectedCount, selectedTotal, allSelected };
    }
};

Vue.createApp(App).mount('#app');
