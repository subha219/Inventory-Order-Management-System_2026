const App = {
    setup() {
        const emptyForm = () => ({ id: '', code: '', zone: '', aisle: '', rack: '', shelf: '', bin: '', locationType: 'Bin', capacity: null, isActive: true, notes: '' });
        const state = Vue.reactive({
            loading: true, saving: false,
            warehouses: [], products: [], warehouseId: '',
            tab: 'map', tabs: [{ key: 'map', label: 'Map' }, { key: 'list', label: 'List' }, { key: 'unplaced', label: 'Not put away' }],
            locations: [], zones: [], unplaced: [],
            summary: { totalLocations: 0, activeLocations: 0, emptyLocations: 0, fullLocations: 0, totalCapacity: 0, totalUsed: 0, utilizationPercent: 0, unplacedUnits: 0 },
            search: '', statusFilter: '',
            selectedId: '', edits: {},
            put: { locationId: '', productId: '', quantity: null, lockProduct: false, lockLocation: false, returnToDetail: false },
            move: { fromId: '', fromCode: '', productId: '', productName: '', available: 0, toLocationId: '', quantity: null },
            form: emptyForm(),
            gen: { zone: 'A', aisles: 2, racks: 4, shelves: 3, bins: 4, capacity: 100 }
        });
        const detailRef = Vue.ref(null), putRef = Vue.ref(null), moveRef = Vue.ref(null), formRef = Vue.ref(null), genRef = Vue.ref(null);
        const modals = {};
        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
        const uid = () => StorageManager.getUserId();
        const err = (e) => e?.response?.data?.message || e?.response?.data?.title || e.message;
        const fail = (title, e) => { console.error(e); Swal.fire({ icon: 'error', title, text: err(e) }); };

        const methods = {
            num: (v) => nf.format(v || 0),
            band: (p) => (p >= 95 ? 'full' : p >= 80 ? 'high' : p >= 50 ? 'mid' : ''),
            cellClass: (l) => {
                if (!l.isActive) return 'off';
                if (l.used <= 0) return '';
                if (l.utilization === null) return 'info';
                return l.utilization >= 95 ? 'full' : l.utilization >= 80 ? 'high' : l.utilization >= 50 ? 'mid' : 'low';
            },
            shortCode: (l) => [l.rack, l.shelf, l.bin].filter(Boolean).join('-') || l.code,
            tip: (l) => l.code + ' - ' + (l.capacity ? methods.num(l.used) + '/' + methods.num(l.capacity) + ' units' : methods.num(l.used) + ' units') +
                (l.items.length ? '\n' + l.items.map(i => i.productName + ': ' + methods.num(i.quantity)).join('\n') : '')
        };

        const filtered = Vue.computed(() => {
            const q = state.search.trim().toLowerCase();
            return state.locations.filter(l => {
                if (state.statusFilter === 'empty' && !(l.isActive && l.used <= 0)) return false;
                if (state.statusFilter === 'used' && !(l.used > 0)) return false;
                if (state.statusFilter === 'full' && !(l.utilization !== null && l.utilization >= 95)) return false;
                if (state.statusFilter === 'inactive' && l.isActive) return false;
                if (!q) return true;
                return (l.code || '').toLowerCase().includes(q) || l.items.some(i => (i.productName || '').toLowerCase().includes(q) || (i.productNumber || '').toLowerCase().includes(q));
            });
        });

        const mapGroups = Vue.computed(() => {
            const zones = {};
            filtered.value.forEach(l => {
                const z = l.zone || '-', a = l.aisle || '-';
                ((zones[z] = zones[z] || {})[a] = zones[z][a] || []).push(l);
            });
            return Object.keys(zones).sort().map(z => ({ zone: z, aisles: Object.keys(zones[z]).sort().map(a => ({ aisle: a, items: zones[z][a] })) }));
        });

        const selected = Vue.computed(() => state.locations.find(l => l.id === state.selectedId) || null);

        const free = (l) => (l.capacity ? Math.max(l.capacity - l.used, 0) : Infinity);
        const putOptions = Vue.computed(() => {
            const wh = state.put.locationId && state.put.lockLocation ? (state.locations.find(l => l.id === state.put.locationId) || {}).warehouseId : state.warehouseId;
            const qty = state.put.quantity || 0;
            const pid = state.put.productId;
            const rows = state.locations.filter(l => l.isActive && (!wh || l.warehouseId === wh)).map(l => {
                const holds = pid && l.items.some(i => i.productId === pid);
                const fits = free(l) >= qty;
                const rank = (fits ? 0 : 3) + (holds ? 0 : l.used <= 0 ? 2 : 1);
                const fr = free(l);
                const why = holds ? 'already holds this product' : l.used <= 0 ? 'empty location' : 'best fit';
                return { id: l.id, code: l.code, rank, fits, holds, fr, why,
                    label: l.code + ' - ' + (l.capacity ? 'free ' + methods.num(fr) : 'no limit') + (holds ? ' - holds this product' : '') + (fits ? '' : ' - not enough room') };
            });
            rows.sort((a, b) => a.rank - b.rank || (isFinite(a.fr) && isFinite(b.fr) ? a.fr - b.fr : 0) || a.code.localeCompare(b.code));
            if (rows.length && rows[0].fits) rows[0].suggested = true;
            return rows;
        });
        const putHint = Vue.computed(() => {
            const u = state.unplaced.find(x => x.productId === state.put.productId);
            return u ? 'Not put away yet: ' + methods.num(u.unplaced) + ' units' : '';
        });
        const moveOptions = Vue.computed(() => {
            const from = state.locations.find(l => l.id === state.move.fromId);
            const qty = state.move.quantity || 0;
            return state.locations.filter(l => from && l.id !== from.id && l.isActive && l.warehouseId === from.warehouseId)
                .map(l => ({ id: l.id, label: l.code + ' - ' + (l.capacity ? 'free ' + methods.num(free(l)) : 'no limit') + (free(l) >= qty ? '' : ' - not enough room'), fits: free(l) >= qty, fr: free(l) }))
                .sort((a, b) => (b.fits - a.fits) || (isFinite(a.fr) && isFinite(b.fr) ? a.fr - b.fr : 0));
        });
        const genTotal = Vue.computed(() => (state.gen.aisles || 0) * (state.gen.racks || 0) * (state.gen.shelves || 0) * (state.gen.bins || 0));

        const swap = (from, to) => {
            const f = modals[from];
            if (!f || !f._isShown) { modals[to].show(); return; }
            f._element.addEventListener('hidden.bs.modal', () => modals[to].show(), { once: true });
            f.hide();
        };

        const load = async () => {
            state.loading = true;
            try {
                const res = await AxiosManager.get('/StorageLocation/GetStorageLocationList?warehouseId=' + encodeURIComponent(state.warehouseId), {});
                const c = res?.data?.content ?? {};
                state.locations = c.data ?? [];
                state.zones = c.zones ?? [];
                state.unplaced = c.unplaced ?? [];
                if (c.summary) state.summary = c.summary;
            } catch (e) { fail('Could not load locations', e); } finally { state.loading = false; }
        };

        const run = async (url, body, okMsg) => {
            state.saving = true;
            try {
                const res = await AxiosManager.post(url, body);
                await load();
                return res?.data?.content ?? {};
            } catch (e) { fail('That did not work', e); return null; } finally { state.saving = false; }
        };

        const handler = {
            load,
            // ---- detail
            openDetail: (id) => { state.selectedId = id; state.edits = {}; modals.detail.show(); },
            closeDetail: () => modals.detail.hide(),
            saveQty: async (i) => {
                const v = state.edits[i.productId];
                if (v === undefined || v === null || v === '') return;
                await run('/StorageLocation/SetLocationStock', { locationId: state.selectedId, productId: i.productId, quantity: v, updatedById: uid() });
                state.edits = {};
            },
            removeItem: async (i) => {
                const r = await Swal.fire({ icon: 'question', title: 'Remove ' + i.productName + ' from this location?', text: 'The stock itself is not changed, it just becomes "not put away".', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!r.isConfirmed) return;
                await run('/StorageLocation/SetLocationStock', { locationId: state.selectedId, productId: i.productId, quantity: 0, updatedById: uid() });
            },
            deleteLocation: async () => {
                const l = selected.value; if (!l) return;
                const r = await Swal.fire({ icon: 'warning', title: 'Delete location ' + l.code + '?', showCancelButton: true, confirmButtonText: 'Delete' });
                if (!r.isConfirmed) return;
                const ok = await run('/StorageLocation/DeleteStorageLocation', { id: l.id, deletedById: uid() });
                if (ok) modals.detail.hide();
            },
            // ---- put away
            openPutAway: (opt) => {
                state.put = { locationId: opt.locationId || '', productId: opt.productId || '', quantity: opt.quantity ?? null,
                    lockProduct: !!opt.productId, lockLocation: !!opt.locationId, returnToDetail: !!opt.locationId };
                if (!state.put.locationId && putOptions.value.length && putOptions.value[0].fits) state.put.locationId = putOptions.value[0].id;
                swap('detail', 'put');
            },
            putChanged: () => {
                if (!state.put.lockLocation) state.put.locationId = (putOptions.value[0] && putOptions.value[0].fits) ? putOptions.value[0].id : '';
            },
            closePut: () => { modals.put.hide(); if (state.put.returnToDetail) modals.put._element.addEventListener('hidden.bs.modal', () => modals.detail.show(), { once: true }); },
            savePut: async () => {
                const p = state.put;
                if (!p.productId || !p.locationId || !(p.quantity > 0)) return Swal.fire({ icon: 'warning', title: 'Choose a product, a location and a quantity' });
                const loc = state.locations.find(l => l.id === p.locationId);
                const existing = loc?.items.find(i => i.productId === p.productId)?.quantity ?? 0;
                const ok = await run('/StorageLocation/SetLocationStock', { locationId: p.locationId, productId: p.productId, quantity: existing + p.quantity, updatedById: uid() });
                if (ok) {
                    modals.put.hide();
                    if (p.returnToDetail) { modals.put._element.addEventListener('hidden.bs.modal', () => modals.detail.show(), { once: true }); }
                }
            },
            // ---- move
            openMove: (i) => {
                const l = selected.value;
                state.move = { fromId: l.id, fromCode: l.code, productId: i.productId, productName: i.productName, available: i.quantity, toLocationId: '', quantity: i.quantity };
                swap('detail', 'move');
            },
            closeMove: () => { modals.move.hide(); modals.move._element.addEventListener('hidden.bs.modal', () => modals.detail.show(), { once: true }); },
            saveMove: async () => {
                const m = state.move;
                if (!m.toLocationId || !(m.quantity > 0)) return Swal.fire({ icon: 'warning', title: 'Choose a destination and a quantity' });
                const ok = await run('/StorageLocation/MoveLocationStock', { fromLocationId: m.fromId, toLocationId: m.toLocationId, productId: m.productId, quantity: m.quantity, updatedById: uid() });
                if (ok) { modals.move.hide(); modals.move._element.addEventListener('hidden.bs.modal', () => modals.detail.show(), { once: true }); }
            },
            // ---- location form
            openForm: (l) => {
                state.form = l ? { id: l.id, code: l.code || '', zone: l.zone || '', aisle: l.aisle || '', rack: l.rack || '', shelf: l.shelf || '', bin: l.bin || '',
                    locationType: l.locationType || 'Bin', capacity: l.capacity, isActive: l.isActive, notes: l.notes || '' } : emptyForm();
                swap('detail', 'form');
            },
            closeForm: () => { modals.form.hide(); },
            saveForm: async () => {
                const f = state.form;
                let code = (f.code || '').trim();
                if (!code) code = [f.zone, f.aisle, f.rack, f.shelf, f.bin].map(v => (v || '').trim()).filter(Boolean).join('-');
                if (!code) return Swal.fire({ icon: 'warning', title: 'Enter a code, or fill in the zone / aisle / rack / shelf / bin' });
                const body = { code, zone: f.zone, aisle: f.aisle, rack: f.rack, shelf: f.shelf, bin: f.bin, locationType: f.locationType,
                    capacity: f.capacity === null || f.capacity === '' || f.capacity <= 0 ? null : f.capacity, isActive: f.isActive, notes: f.notes };
                const ok = f.id
                    ? await run('/StorageLocation/UpdateStorageLocation', { ...body, id: f.id, updatedById: uid() })
                    : await run('/StorageLocation/CreateStorageLocation', { ...body, warehouseId: state.warehouseId, createdById: uid() });
                if (ok) modals.form.hide();
            },
            // ---- generator
            openGenerate: () => { modals.gen.show(); },
            closeGenerate: () => modals.gen.hide(),
            saveGenerate: async () => {
                const g = state.gen;
                if (!(g.zone || '').trim()) return Swal.fire({ icon: 'warning', title: 'Enter a zone name' });
                const ok = await run('/StorageLocation/GenerateStorageLocations', { warehouseId: state.warehouseId, zone: g.zone, aisles: g.aisles, racksPerAisle: g.racks,
                    shelvesPerRack: g.shelves, binsPerShelf: g.bins, capacity: g.capacity > 0 ? g.capacity : null, createdById: uid() });
                if (ok) { modals.gen.hide(); Swal.fire({ icon: 'success', title: ok.message || 'Done', timer: 1800, showConfirmButton: false }); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                const mk = (r) => new bootstrap.Modal(r.value, { backdrop: 'static', keyboard: false });
                modals.detail = mk(detailRef); modals.put = mk(putRef); modals.move = mk(moveRef); modals.form = mk(formRef); modals.gen = mk(genRef);
                formRef.value.addEventListener('hidden.bs.modal', () => { if (state.form.id && state.selectedId) modals.detail.show(); });
                const [wh, pr] = await Promise.all([
                    AxiosManager.get('/Warehouse/GetWarehouseList', {}),
                    AxiosManager.get('/Product/GetProductList', {})
                ]);
                state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
                state.products = (pr?.data?.content?.data ?? []).filter(p => p.physical !== false);
                if (state.warehouses.length === 1) state.warehouseId = state.warehouses[0].id;
                await load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler, filtered, mapGroups, selected, putOptions, putHint, moveOptions, genTotal,
            detailRef, putRef, moveRef, formRef, genRef };
    }
};

Vue.createApp(App).mount('#app');
