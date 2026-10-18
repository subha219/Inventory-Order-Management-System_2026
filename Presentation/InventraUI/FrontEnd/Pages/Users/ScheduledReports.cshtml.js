const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, reports: [], types: [], warehouses: [], offsetMinutes: 330, edit: null,
            days: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
        });

        const fail = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });
        const pad = (n) => String(n).padStart(2, '0');

        const methods = {
            zone: () => {
                const m = state.offsetMinutes, sign = m < 0 ? '-' : '+', a = Math.abs(m);
                return 'UTC' + sign + pad(Math.floor(a / 60)) + ':' + pad(a % 60);
            },
            type: (t) => state.types.find(x => x.type === t) || { name: t || '', description: '', usesWarehouse: false, usesSlowDays: false },
            when: (r) => {
                const t = pad(r.sendHour) + ':' + pad(r.sendMinute);
                return r.frequency === 'Weekly' ? 'Every ' + state.days[r.dayOfWeek ?? 1] + ' at ' + t
                    : r.frequency === 'Monthly' ? 'Day ' + (r.dayOfMonth ?? 1) + ' of each month at ' + t : 'Every day at ' + t;
            },
            stamp: (d) => new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/ScheduledReport/GetScheduledReports', {});
                    const c = res?.data?.content ?? {};
                    state.reports = c.data ?? []; state.types = c.types ?? []; state.offsetMinutes = c.offsetMinutes ?? 330;
                } catch (e) { fail('Could not load the schedules', e); } finally { state.loading = false; }
            },
            newReport: () => {
                state.edit = { id: '', name: '', reportType: state.types[0]?.type || 'InventoryValuation', frequency: 'Weekly', dayOfWeek: 1, dayOfMonth: 1, time: '08:00', recipients: '', enabled: true, warehouseId: '', slowDays: null };
            },
            edit: (r) => {
                state.edit = { id: r.id, name: r.name, reportType: r.reportType, frequency: r.frequency, dayOfWeek: r.dayOfWeek ?? 1, dayOfMonth: r.dayOfMonth ?? 1,
                    time: pad(r.sendHour) + ':' + pad(r.sendMinute), recipients: r.recipients, enabled: r.enabled, warehouseId: r.warehouseId || '', slowDays: r.slowDays };
                window.scrollTo({ top: 0, behavior: 'smooth' });
            },
            save: async () => {
                const e = state.edit;
                const [h, m] = (e.time || '08:00').split(':').map(Number);
                state.saving = true;
                try {
                    await AxiosManager.post('/ScheduledReport/SaveScheduledReport', {
                        id: e.id || null, name: e.name, reportType: e.reportType, frequency: e.frequency, dayOfWeek: e.frequency === 'Weekly' ? e.dayOfWeek : null,
                        dayOfMonth: e.frequency === 'Monthly' ? e.dayOfMonth : null, sendHour: h, sendMinute: m, recipients: e.recipients, enabled: e.enabled,
                        warehouseId: e.warehouseId || null, slowDays: e.slowDays || null
                    });
                    state.edit = null;
                    Swal.fire({ icon: 'success', title: 'Saved', timer: 900, showConfirmButton: false });
                    await handler.load();
                } catch (err) { fail('Could not save the schedule', err); } finally { state.saving = false; }
            },
            sendNow: async (r) => {
                const ask = await Swal.fire({ title: 'Send "' + r.name + '" now?', text: 'It goes to ' + r.recipients + '.', icon: 'question', showCancelButton: true, confirmButtonText: 'Send' });
                if (!ask.isConfirmed) return;
                state.saving = true;
                try {
                    const res = await AxiosManager.post('/ScheduledReport/SendNow', { id: r.id });
                    const c = res?.data?.content ?? {};
                    await Swal.fire({ icon: (c.problems || []).length ? 'warning' : 'success', title: c.message || 'Done', text: (c.problems || []).join('\n') });
                    await handler.load();
                } catch (e) { fail('Could not send the report', e); } finally { state.saving = false; }
            },
            remove: async (r) => {
                const ask = await Swal.fire({ title: 'Delete "' + r.name + '"?', icon: 'warning', showCancelButton: true, confirmButtonText: 'Delete', confirmButtonColor: '#dc3545' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/ScheduledReport/DeleteScheduledReport', { id: r.id }); await handler.load(); } catch (e) { fail('Could not delete', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                try {
                    const wh = await AxiosManager.get('/Warehouse/GetWarehouseList', {});
                    state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
                } catch (e) { console.error(e); }
                await handler.load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
