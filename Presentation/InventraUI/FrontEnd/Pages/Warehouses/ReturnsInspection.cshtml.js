const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, search: '', returns: [], selected: '', lines: [], locations: [], reasons: [], reasonCodes: [], scrapNumber: '', form: {}
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '-'),
            current: () => state.returns.find(r => r.id === state.selected),
            returns: () => {
                const q = state.search.trim().toLowerCase();
                return state.returns.filter(r => !q || (r.number || '').toLowerCase().includes(q) || (r.customer || '').toLowerCase().includes(q));
            }
        };

        const handler = {
            fetch: async (id) => {
                const res = await AxiosManager.get('/ReturnInspection/GetReturns?salesReturnId=' + encodeURIComponent(id || ''), {});
                const c = res?.data?.content ?? {};
                state.returns = c.returns ?? [];
                state.reasons = c.reasons ?? [];
                state.reasonCodes = c.reasonCodes ?? [];
                state.lines = c.lines ?? [];
                state.locations = c.locations ?? [];
                state.scrapNumber = c.scrapNumber || '';
                const form = {};
                // a return not inspected yet starts with "everything good"
                state.lines.forEach(l => { form[l.productId] = { good: l.inspected ? l.good : l.returned, damaged: l.inspected ? l.damaged : 0, reasonCode: l.reasonCode || '', note: l.note || '', locationId: l.locationId || '' }; });
                state.form = form;
            },
            select: async (id) => {
                state.selected = id;
                try { await handler.fetch(id); }
                catch (e) { Swal.fire({ icon: 'error', title: 'Could not load the return', text: e?.response?.data?.message || e.message }); }
            },
            save: async () => {
                const lines = state.lines.map(l => {
                    const f = state.form[l.productId];
                    return { productId: l.productId, good: Number(f.good) || 0, damaged: Number(f.damaged) || 0, reasonCode: f.reasonCode, note: f.note, locationId: f.locationId || null };
                });
                state.saving = true;
                try {
                    const res = await AxiosManager.post('/ReturnInspection/SaveReturnInspection', { salesReturnId: state.selected, lines });
                    const note = res?.data?.content?.note;
                    await Swal.fire(note ? { icon: 'info', title: 'Inspection saved', text: note } : { icon: 'success', title: 'Inspection saved', timer: 1200, showConfirmButton: false });
                    await handler.fetch(state.selected);
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not save the inspection', text: e?.response?.data?.message || e.message });
                } finally { state.saving = false; }
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
