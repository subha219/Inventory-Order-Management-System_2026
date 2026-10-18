const App = {
    setup() {
        const state = Vue.reactive({ loading: true, running: false, saving: '', rules: [] });

        const fail = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const methods = {
            sev: (s) => ({ critical: 'bg-danger', warning: 'bg-warning text-dark', info: 'bg-info text-dark' }[s] || 'bg-secondary'),
            ago: (d) => {
                const m = Math.round((Date.now() - new Date(d.endsWith('Z') ? d : d + 'Z').getTime()) / 60000);
                return m < 1 ? 'just now' : m < 60 ? m + ' min ago' : m < 1440 ? Math.round(m / 60) + ' h ago' : Math.round(m / 1440) + ' d ago';
            }
        };

        const handler = {
            load: async () => {
                state.loading = true;
                try {
                    const res = await AxiosManager.get('/Automation/GetAutomationRules', {});
                    state.rules = res?.data?.content?.data ?? [];
                } catch (e) { fail('Could not load the rules', e); } finally { state.loading = false; }
            },
            save: async (r) => {
                state.saving = r.id;
                try {
                    await AxiosManager.post('/Automation/SaveAutomationRule', {
                        id: r.id, enabled: r.enabled, threshold: r.thresholdUnit ? r.threshold : null, notifyInApp: r.notifyInApp, emailRecipients: r.emailRecipients || ''
                    });
                    Swal.fire({ icon: 'success', title: 'Saved', timer: 900, showConfirmButton: false });
                    await handler.load();
                } catch (e) { fail('Could not save the rule', e); } finally { state.saving = ''; }
            },
            runNow: async () => {
                state.running = true;
                try {
                    const res = await AxiosManager.post('/Automation/RunNow', {});
                    const c = res?.data?.content ?? {};
                    const extra = (c.problems || []).length ? '\n' + c.problems.join('\n') : '';
                    await Swal.fire({
                        icon: (c.problems || []).length ? 'warning' : 'success', title: 'Checked ' + c.checked + ' rule(s)',
                        text: c.triggered + ' need attention, ' + c.emailsSent + ' e-mail(s) sent.' + extra
                    });
                    await handler.load();
                } catch (e) { fail('Could not run the check', e); } finally { state.running = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                await handler.load();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
