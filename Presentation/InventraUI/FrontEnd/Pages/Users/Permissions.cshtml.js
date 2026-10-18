const App = {
    setup() {
        const state = Vue.reactive({
            roles: [], role: '', modules: [], keys: ['Create', 'Edit', 'Confirm', 'Cancel', 'Delete'], allowed: {}, busy: false
        });

        const methods = {
            label: (m) => (m || '').replace(/([a-z])([A-Z])/g, '$1 $2')
        };

        const handler = {
            load: async () => {
                if (!state.role) return;
                try {
                    const res = await AxiosManager.get('/Permission/GetPermissionMatrix?role=' + encodeURIComponent(state.role), {});
                    const c = res?.data?.content ?? {};
                    state.modules = c.modules ?? [];
                    const allowed = {};
                    state.modules.forEach(m => m.actions.forEach(a => { allowed[m.module + '|' + a] = true; }));
                    (c.denies ?? []).forEach(d => { allowed[d.module + '|' + d.actionKey] = false; });
                    state.allowed = allowed;
                } catch (e) { Swal.fire({ icon: 'error', title: 'Could not load the permissions', text: e?.response?.data?.message || e.message }); }
            },
            setAll: (v) => { Object.keys(state.allowed).forEach(k => { state.allowed[k] = v; }); },
            setColumn: (key, v) => { Object.keys(state.allowed).forEach(k => { if (k.endsWith('|' + key)) state.allowed[k] = v; }); },
            save: async () => {
                const denies = Object.keys(state.allowed).filter(k => state.allowed[k] === false).map(k => { const [module, actionKey] = k.split('|'); return { module, actionKey }; });
                state.busy = true;
                try {
                    await AxiosManager.post('/Permission/SavePermissionRules', { roleName: state.role, denies });
                    Swal.fire({ icon: 'success', title: 'Saved', text: denies.length ? denies.length + ' action(s) are switched off for ' + state.role + '.' : state.role + ' can do everything.', timer: 2200, showConfirmButton: false });
                } catch (e) { Swal.fire({ icon: 'error', title: 'Could not save', text: e?.response?.data?.message || e.message }); }
                finally { state.busy = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                const res = await AxiosManager.get('/Security/GetRoleList', {});
                state.roles = (res?.data?.content?.data ?? []).map(r => r.name).filter(n => n && n !== 'Users' && n !== 'Administrator').sort();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
