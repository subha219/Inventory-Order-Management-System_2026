const App = {
    setup() {
        const state = Vue.reactive({
            form: { host: '', port: 587, userName: '', fromAddress: '', fromName: '', security: 'Auto', hasPassword: false, source: 'Not set up' },
            password: '', clearPassword: false, testTo: '', testResult: null, busy: false
        });

        const handler = {
            load: async () => {
                const res = await AxiosManager.get('/SmtpSettings/Get', {});
                Object.assign(state.form, res?.data?.content ?? {});
                state.password = '';
                state.clearPassword = false;
            },
            save: async () => {
                state.busy = true;
                try {
                    const f = state.form;
                    const res = await AxiosManager.post('/SmtpSettings/Save', {
                        host: f.host, port: f.port, userName: f.userName, fromAddress: f.fromAddress, fromName: f.fromName, security: f.security,
                        password: state.password, clearPassword: state.clearPassword
                    });
                    Object.assign(state.form, res?.data?.content ?? {});
                    state.password = '';
                    state.clearPassword = false;
                    Swal.fire({ icon: 'success', title: 'Saved', text: 'New mail settings are active now. Send a test e-mail to check them.', timer: 2600, showConfirmButton: false });
                } catch (e) {
                    Swal.fire({ icon: 'error', title: 'Could not save', text: e?.response?.data?.message || e.message });
                } finally { state.busy = false; }
            },
            test: async () => {
                state.busy = true;
                state.testResult = null;
                try {
                    const res = await AxiosManager.post('/SmtpSettings/SendTest', { to: state.testTo });
                    state.testResult = res?.data?.content ?? { sent: false, message: 'No answer from the server.' };
                } catch (e) {
                    state.testResult = { sent: false, message: e?.response?.data?.message || e.message };
                } finally { state.busy = false; }
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
