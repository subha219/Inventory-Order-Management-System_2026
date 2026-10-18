const App = {
    setup() {
        const state = Vue.reactive({ presets: [], users: [], target: {}, busy: false });

        const handler = {
            load: async () => {
                const res = await AxiosManager.get('/RolePreset/GetPresets', {});
                state.presets = res?.data?.content?.presets ?? [];
                const ur = await AxiosManager.get('/Security/GetUserList', {});
                state.users = (ur?.data?.content?.data ?? []).filter(u => u.email && !u.isDeleted).sort((a, b) => a.email.localeCompare(b.email));
            },
            createTest: async (p) => {
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/RolePreset/Apply', { presetKey: p.key, createTestUser: true });
                    const c = res?.data?.content ?? {};
                    await Swal.fire({
                        icon: 'success',
                        title: 'Test login ready',
                        html: '<div style="text-align:left">Email: <b>' + c.email + '</b><br>Password: <b>' + c.password + '</b><br><small>Shown once. Roles: ' + (c.roles || []).join(', ') + '</small></div>'
                    });
                    await handler.load();
                } catch (e) { Swal.fire({ icon: 'error', title: 'Could not create the test login', text: e?.response?.data?.message || e.message }); }
                finally { state.busy = false; }
            },
            applyTo: async (p) => {
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/RolePreset/Apply', { presetKey: p.key, userId: state.target[p.key], createTestUser: false });
                    const c = res?.data?.content ?? {};
                    Swal.fire({ icon: 'success', title: 'Roles applied', text: (c.email || '') + ' now has: ' + (c.roles || []).join(', ') });
                } catch (e) { Swal.fire({ icon: 'error', title: 'Could not apply the preset', text: e?.response?.data?.message || e.message }); }
                finally { state.busy = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                await handler.load();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, handler };
    }
};

Vue.createApp(App).mount('#app');
