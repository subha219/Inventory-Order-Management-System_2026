const App = {
    setup() {
        const state = Vue.reactive({
            tab: 'replenish', tabs: [{ key: 'replenish', label: 'Replenishment' }, { key: 'move', label: 'Move stock' }, { key: 'rules', label: 'Rules' }, { key: 'history', label: 'History' }],
            busy: false, stock: [], locations: [], rules: [], suggestions: [], history: [], allProducts: [],
            move: { productId: '', fromId: '', toId: '', qty: null, reason: '' },
            rule: { locationId: '', productId: '', min: null, max: null }
        });
        const userMap = {};

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            user: (id) => (id ? (userMap[id] || '') : ''),
            products: () => { const seen = {}; state.stock.forEach(s => { seen[s.productId] = s.productName; }); return Object.keys(seen).map(id => ({ id, name: seen[id] })).sort((a, b) => a.name.localeCompare(b.name)); },
            sources: () => state.stock.filter(s => s.productId === state.move.productId),
            targets: () => {
                const from = state.locations.find(l => l.id === state.move.fromId);
                return state.locations.filter(l => from && l.warehouseId === from.warehouseId && l.id !== from.id);
            }
        };

        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/BinMove/GetBinMoves', {});
                const c = res?.data?.content ?? {};
                state.stock = c.stock ?? []; state.locations = c.locations ?? []; state.rules = c.rules ?? []; state.suggestions = c.suggestions ?? []; state.history = c.history ?? [];
            },
            send: async (url, body) => AxiosManager.post(url, body),
            doOne: async (s) => {
                state.busy = true;
                try {
                    await AxiosManager.post('/BinMove/MoveStock', { productId: s.productId, fromLocationId: s.fromLocationId, toLocationId: s.toLocationId, quantity: s.quantity, moveType: 'Replenish', reason: 'Replenishment' });
                    await handler.fetch();
                } catch (e) { err('Could not move the stock', e); } finally { state.busy = false; }
            },
            doAll: async () => {
                state.busy = true; let done = 0, failed = 0;
                for (const s of state.suggestions.filter(x => x.fromLocationId && x.quantity > 0)) {
                    try { await AxiosManager.post('/BinMove/MoveStock', { productId: s.productId, fromLocationId: s.fromLocationId, toLocationId: s.toLocationId, quantity: s.quantity, moveType: 'Replenish', reason: 'Replenishment' }); done++; }
                    catch (e) { failed++; }
                }
                try { await handler.fetch(); } catch (e) { }
                state.busy = false;
                Swal.fire({ icon: failed ? 'warning' : 'success', title: done + ' move(s) done' + (failed ? ', ' + failed + ' failed' : ''), timer: failed ? undefined : 1500, showConfirmButton: !!failed });
            },
            moveManual: async () => {
                const m = state.move;
                if (!m.productId || !m.fromId || !m.toId || !(Number(m.qty) > 0)) { Swal.fire({ icon: 'warning', title: 'Choose the product, both locations and a quantity' }); return; }
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/BinMove/MoveStock', { productId: m.productId, fromLocationId: m.fromId, toLocationId: m.toId, quantity: Number(m.qty), moveType: 'Move', reason: m.reason });
                    await handler.fetch();
                    state.move.qty = null; state.move.toId = '';
                    Swal.fire({ icon: 'success', title: res?.data?.content?.message || 'Moved', timer: 1500, showConfirmButton: false });
                } catch (e) { err('Could not move the stock', e); } finally { state.busy = false; }
            },
            saveRule: async () => {
                const r = state.rule;
                if (!r.locationId || !r.productId) { Swal.fire({ icon: 'warning', title: 'Choose a location and a product' }); return; }
                state.busy = true;
                try {
                    await AxiosManager.post('/BinMove/SaveReplenishmentRule', { locationId: r.locationId, productId: r.productId, min: Number(r.min) || 0, max: Number(r.max) || 0 });
                    await handler.fetch();
                    r.min = null; r.max = null;
                } catch (e) { err('Could not save the rule', e); } finally { state.busy = false; }
            },
            deleteRule: async (r) => {
                const ask = await Swal.fire({ title: 'Remove this rule?', icon: 'question', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/BinMove/DeleteReplenishmentRule', { id: r.id }); await handler.fetch(); } catch (e) { err('Could not remove the rule', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                try {
                    const u = await AxiosManager.get('/Security/GetUserList', {});
                    (u?.data?.content?.data ?? []).forEach(x => { userMap[x.id] = ((x.firstName || '') + ' ' + (x.lastName || '')).trim() || x.email || x.id; });
                } catch (e) { }
                try {
                    const p = await AxiosManager.get('/Product/GetProductList', {});
                    state.allProducts = (p?.data?.content?.data ?? []).filter(x => x.physical !== false).map(x => ({ id: x.id, name: x.name })).sort((a, b) => (a.name || '').localeCompare(b.name || ''));
                } catch (e) { console.error('products error', e); }
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
