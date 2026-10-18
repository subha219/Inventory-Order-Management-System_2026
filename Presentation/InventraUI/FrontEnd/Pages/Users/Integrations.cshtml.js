const App = {
    setup() {
        const state = Vue.reactive({
            keys: [], webhooks: [], shipments: [], events: [], keyName: '', newKey: null,
            hook: { name: '', url: '', events: [] }
        });
        const origin = window.location.origin;

        const methods = {
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : 'never')
        };
        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });
        const toast = (title) => Swal.fire({ icon: 'success', title, timer: 1400, showConfirmButton: false });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/Integration/GetIntegrations', {});
                const c = res?.data?.content ?? {};
                state.keys = c.keys ?? []; state.webhooks = c.webhooks ?? []; state.shipments = c.shipments ?? []; state.events = c.events ?? [];
            },
            copy: async (text) => {
                try { await navigator.clipboard.writeText(text); toast('Copied'); }
                catch (e) { Swal.fire({ title: 'Copy this', input: 'text', inputValue: text }); }
            },
            createKey: async () => {
                if (!state.keyName.trim()) { Swal.fire({ icon: 'warning', title: 'Give the key a name' }); return; }
                try {
                    const res = await AxiosManager.post('/Integration/CreateApiKey', { name: state.keyName.trim() });
                    state.newKey = { name: state.keyName.trim(), value: res?.data?.content?.secret };
                    state.keyName = '';
                    await handler.fetch();
                } catch (e) { err('Could not create the key', e); }
            },
            revoke: async (k) => {
                const ask = await Swal.fire({ title: 'Revoke ' + k.name + '?', text: 'Any system using this key stops working at once.', icon: 'warning', showCancelButton: true, confirmButtonText: 'Revoke' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Integration/RevokeApiKey', { id: k.id }); await handler.fetch(); } catch (e) { err('Could not revoke', e); }
            },
            addHook: async () => {
                const h = state.hook;
                try {
                    await AxiosManager.post('/Integration/SaveWebhook', { name: h.name, url: h.url, events: h.events.join(','), isActive: true });
                    state.hook = { name: '', url: '', events: [] };
                    await handler.fetch(); toast('Webhook added');
                } catch (e) { err('Could not add the webhook', e); }
            },
            toggleHook: async (h) => {
                try { await AxiosManager.post('/Integration/SaveWebhook', { id: h.id, name: h.name, url: h.url, events: h.events, isActive: !h.isActive }); await handler.fetch(); }
                catch (e) { err('Could not update', e); }
            },
            removeHook: async (h) => {
                const ask = await Swal.fire({ title: 'Remove ' + h.name + '?', icon: 'question', showCancelButton: true, confirmButtonText: 'Remove' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Integration/DeleteWebhook', { id: h.id }); await handler.fetch(); } catch (e) { err('Could not remove', e); }
            },
            test: async (h) => {
                try { const res = await AxiosManager.post('/Integration/TestWebhook', { id: h.id }); await handler.fetch(); Swal.fire({ icon: 'info', title: 'Test sent', text: res?.data?.content?.message }); }
                catch (e) { err('Could not send the test', e); }
            },
            link: async (s) => {
                try {
                    let token = s.publicToken;
                    if (!token) { const res = await AxiosManager.post('/Integration/CreateTrackingLink', { shipmentId: s.id }); token = res?.data?.content?.secret; await handler.fetch(); }
                    await handler.copy(origin + '/track/' + token);
                } catch (e) { err('Could not create the link', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Users']);
                await SecurityManager.validateToken();
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler, origin };
    }
};

Vue.createApp(App).mount('#app');
