const App = {
    setup() {
        const state = Vue.reactive({
            loading: false,
            saving: false,
            items: [],
            lookbackDays: 90,
            leadTimeDays: 7,
            serviceLevel: 95
        });

        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
        const methods = {
            num: (v) => nf.format(v || 0),
            coverClass: (d) => d == null ? 'bg-secondary'
                : d <= state.leadTimeDays ? 'bg-danger'
                : d <= state.leadTimeDays * 2 ? 'bg-warning text-dark'
                : 'bg-success'
        };

        const selectedItems = Vue.computed(() => state.items.filter(r => r.selected));
        const selectedCount = Vue.computed(() => selectedItems.value.length);
        const allSelected = Vue.computed(() => state.items.length > 0 && state.items.every(r => r.selected));

        const load = async () => {
            state.loading = true;
            try {
                const response = await AxiosManager.get(
                    `/Product/GetDemandForecast?lookbackDays=${state.lookbackDays}&leadTimeDays=${state.leadTimeDays || 7}&serviceLevel=${state.serviceLevel}`, {});
                state.items = (response?.data?.content?.data ?? []).map(r => ({
                    ...r,
                    // Pre-select only products whose levels differ from what is saved.
                    selected: r.currentReorderPoint !== r.recommendedReorderPoint ||
                        r.currentReorderQuantity !== r.recommendedReorderQuantity
                }));
            } catch (e) {
                console.error(e);
                Swal.fire({ icon: 'error', title: 'Could not calculate the forecast', text: e?.response?.data?.message || e.message });
            } finally {
                state.loading = false;
            }
        };

        const handler = {
            load,
            toggleAll: (checked) => state.items.forEach(r => { r.selected = checked; }),
            apply: async () => {
                if (selectedCount.value === 0) return;
                const confirm = await Swal.fire({
                    icon: 'question',
                    title: `Update ${selectedCount.value} products?`,
                    text: 'This overwrites their current Reorder Point and Reorder Quantity.',
                    showCancelButton: true,
                    confirmButtonText: 'Apply'
                });
                if (!confirm.isConfirmed) return;

                state.saving = true;
                try {
                    const response = await AxiosManager.post('/Product/ApplyReorderRecommendations', {
                        updatedById: StorageManager.getUserId(),
                        items: selectedItems.value.map(r => ({
                            productId: r.productId,
                            reorderPoint: Number(r.recommendedReorderPoint) || 0,
                            reorderQuantity: Number(r.recommendedReorderQuantity) || 0
                        }))
                    });
                    await Swal.fire({ icon: 'success', title: 'Reorder levels updated', text: `${response?.data?.content?.updated ?? 0} products updated.` });
                    await load();
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not apply', text: e?.response?.data?.message || e.message });
                } finally {
                    state.saving = false;
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                await load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, handler, methods, selectedCount, allSelected };
    }
};

Vue.createApp(App).mount('#app');
