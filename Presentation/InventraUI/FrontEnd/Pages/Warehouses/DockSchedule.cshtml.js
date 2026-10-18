const App = {
    setup() {
        const pad = (n) => String(n).padStart(2, '0');
        const dayStr = (d) => d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
        const localInput = (d) => dayStr(d) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
        const emptyForm = () => ({ id: '', doorId: '', direction: 'Inbound', start: '', minutes: 60, carrier: '', reference: '', party: '', notes: '' });

        const state = Vue.reactive({
            loading: true, busy: false, day: dayStr(new Date()), doors: [], appts: [], warehouses: [],
            form: emptyForm(), door: { name: '', warehouseId: '', type: 'Both' }
        });

        const lateCount = Vue.computed(() => state.appts.filter(a => a.late).length);
        const asUtc = (s) => new Date(s && !/Z$/.test(s) ? s + 'Z' : s);

        const methods = {
            forDoor: (id) => state.appts.filter(a => a.dockDoorId === id),
            time: (s) => asUtc(s).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }),
            badge: (a) => a.late ? 'bg-danger' : ({ Scheduled: 'bg-primary', CheckedIn: 'bg-success', Completed: 'bg-secondary', Cancelled: 'bg-secondary', NoShow: 'bg-dark' }[a.status] || 'bg-secondary')
        };
        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const from = new Date(state.day + 'T00:00:00');
                const to = new Date(from.getTime()); to.setDate(to.getDate() + 1);
                const res = await AxiosManager.get('/Dock/GetSchedule?fromUtc=' + encodeURIComponent(from.toISOString()) + '&toUtc=' + encodeURIComponent(to.toISOString()), {});
                const c = res?.data?.content ?? {};
                state.doors = c.doors ?? []; state.appts = c.appointments ?? []; state.warehouses = c.warehouses ?? [];
            },
            shift: (n) => { const d = new Date(state.day + 'T00:00:00'); d.setDate(d.getDate() + n); state.day = dayStr(d); handler.fetch(); },
            today: () => { state.day = dayStr(new Date()); handler.fetch(); },
            resetForm: () => { state.form = emptyForm(); },
            edit: (a) => {
                state.form = { id: a.id, doorId: a.dockDoorId, direction: a.direction, start: localInput(asUtc(a.startAtUtc)), minutes: a.durationMinutes, carrier: a.carrier || '', reference: a.reference || '', party: a.partyName || '', notes: a.notes || '' };
                window.scrollTo({ top: 0, behavior: 'smooth' });
            },
            save: async () => {
                const f = state.form;
                if (!f.doorId || !f.start) { Swal.fire({ icon: 'warning', title: 'Choose the door and arrival time' }); return; }
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/Dock/SaveAppointment', {
                        id: f.id || null, dockDoorId: f.doorId, direction: f.direction, startAtUtc: new Date(f.start).toISOString(), durationMinutes: Number(f.minutes) || 60,
                        carrier: f.carrier, reference: f.reference, partyName: f.party, notes: f.notes
                    });
                    state.day = dayStr(new Date(f.start));
                    handler.resetForm();
                    await handler.fetch();
                    Swal.fire({ icon: 'success', title: res?.data?.content?.message || 'Saved', timer: 1200, showConfirmButton: false });
                } catch (e) { err('Could not book', e); } finally { state.busy = false; }
            },
            act: async (a, action) => {
                if (action === 'Cancel' || action === 'NoShow') {
                    const ask = await Swal.fire({ title: (action === 'Cancel' ? 'Cancel ' : 'Mark no-show for ') + a.number + '?', icon: 'question', showCancelButton: true });
                    if (!ask.isConfirmed) return;
                }
                try { await AxiosManager.post('/Dock/SetStatus', { id: a.id, action }); await handler.fetch(); }
                catch (e) { err('Could not update', e); }
            },
            addDoor: async () => {
                const d = state.door;
                if (!d.name || !d.warehouseId) { Swal.fire({ icon: 'warning', title: 'Enter a door name and choose the warehouse' }); return; }
                try { await AxiosManager.post('/Dock/SaveDoor', { name: d.name, warehouseId: d.warehouseId, doorType: d.type, isActive: true }); state.door.name = ''; await handler.fetch(); }
                catch (e) { err('Could not add door', e); }
            },
            toggleDoor: async (d) => {
                try { await AxiosManager.post('/Dock/SaveDoor', { id: d.id, name: d.name, warehouseId: d.warehouseId, doorType: d.doorType, isActive: !d.isActive }); await handler.fetch(); }
                catch (e) { err('Could not update door', e); }
            },
            removeDoor: async (d) => {
                const ask = await Swal.fire({ title: 'Remove ' + d.name + '?', icon: 'question', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Dock/DeleteDoor', { id: d.id }); await handler.fetch(); }
                catch (e) { err('Could not remove door', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });

        return { state, methods, handler, lateCount };
    }
};

Vue.createApp(App).mount('#app');
