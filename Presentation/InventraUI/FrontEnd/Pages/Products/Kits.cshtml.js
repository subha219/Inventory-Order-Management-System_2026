const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, busy: false, warehouseId: '', warehouses: [], products: [], kits: [], history: [], selected: '', editing: false, isNew: false,
            edit: { kitId: '', lines: [] }, build: { warehouseId: '', qty: null, notes: '' }
        });
        const userMap = {};

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            user: (id) => (id ? (userMap[id] || '') : ''),
            current: () => state.kits.find(k => k.productId === state.selected),
            productName: (id) => (state.products.find(p => p.id === id)?.name || ''),
            // a product already used in another line is not offered again
            componentOptions: (current) => state.products.filter(p => p.id !== state.edit.kitId && (p.id === current || !state.edit.lines.some(l => l.componentId === p.id)))
        };

        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/Kit/GetKits?warehouseId=' + encodeURIComponent(state.warehouseId || ''), {});
                const c = res?.data?.content ?? {};
                state.kits = c.kits ?? []; state.products = c.products ?? []; state.warehouses = c.warehouses ?? []; state.history = c.history ?? [];
                if (!state.build.warehouseId) state.build.warehouseId = state.warehouseId || (state.warehouses[0]?.id ?? '');
            },
            newKit: () => { state.selected = ''; state.editing = true; state.isNew = true; state.edit = { kitId: '', lines: [{ componentId: '', quantity: 1 }] }; },
            select: (k) => {
                state.selected = k.productId; state.editing = true; state.isNew = false;
                state.edit = { kitId: k.productId, lines: k.components.map(c => ({ componentId: c.productId, quantity: c.perKit })) };
            },
            saveBom: async () => {
                const lines = state.edit.lines.filter(l => l.componentId && Number(l.quantity) > 0).map(l => ({ componentProductId: l.componentId, quantity: Number(l.quantity) }));
                if (!state.edit.kitId || !lines.length) { Swal.fire({ icon: 'warning', title: 'Choose the kit product and at least one component' }); return; }
                state.busy = true;
                try {
                    await AxiosManager.post('/Kit/SaveBom', { kitProductId: state.edit.kitId, lines });
                    await handler.fetch();
                    state.selected = state.edit.kitId; state.isNew = false;
                    Swal.fire({ icon: 'success', title: 'Saved', timer: 1000, showConfirmButton: false });
                } catch (e) { err('Could not save', e); } finally { state.busy = false; }
            },
            removeBom: async () => {
                const ask = await Swal.fire({ title: 'Remove this kit definition?', text: 'Stock and earlier builds are not changed.', icon: 'question', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Kit/DeleteBom', { kitProductId: state.selected }); state.selected = ''; state.editing = false; await handler.fetch(); }
                catch (e) { err('Could not remove', e); }
            },
            build: async () => {
                const b = state.build;
                if (!b.warehouseId || !(Number(b.qty) > 0)) { Swal.fire({ icon: 'warning', title: 'Choose the warehouse and how many kits' }); return; }
                const ask = await Swal.fire({ title: 'Build ' + methods.n(Number(b.qty)) + ' x ' + methods.current().productName + '?', text: 'The components leave stock and the kits arrive in stock right away.', icon: 'question', showCancelButton: true, confirmButtonText: 'Build' });
                if (!ask.isConfirmed) return;
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/Kit/BuildKit', { kitProductId: state.selected, warehouseId: b.warehouseId, quantity: Number(b.qty), notes: b.notes });
                    b.qty = null; b.notes = '';
                    await handler.fetch();
                    Swal.fire({ icon: 'success', title: 'Built', text: res?.data?.content?.message });
                } catch (e) { err('Could not build', e); } finally { state.busy = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                try {
                    const u = await AxiosManager.get('/Security/GetUserList', {});
                    (u?.data?.content?.data ?? []).forEach(x => { userMap[x.id] = ((x.firstName || '') + ' ' + (x.lastName || '')).trim() || x.email || x.id; });
                } catch (e) { }
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
