const App = {
    setup() {
        const state = Vue.reactive({ busy: false, kind: 'PurchaseOrder', orderId: '', days: 30, links: [], purchaseOrders: [], salesOrders: [] });
        const origin = window.location.origin;
        const asUtc = (d) => new Date(d && !/Z$/.test(d) ? d + 'Z' : d);

        const methods = {
            orders: () => (state.kind === 'PurchaseOrder' ? state.purchaseOrders : state.salesOrders),
            day: (d) => (d ? asUtc(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : ''),
            when: (d) => (d ? asUtc(d).toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '')
        };
        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async () => {
                const res = await AxiosManager.get('/portal/admin', {});
                const c = res?.data?.content ?? {};
                state.links = c.links ?? []; state.purchaseOrders = c.purchaseOrders ?? []; state.salesOrders = c.salesOrders ?? [];
            },
            copyText: async (text) => {
                try { await navigator.clipboard.writeText(text); Swal.fire({ icon: 'success', title: 'Link copied', timer: 1300, showConfirmButton: false }); }
                catch (e) { Swal.fire({ title: 'Copy this link', input: 'text', inputValue: text }); }
            },
            copy: (l) => handler.copyText(origin + '/portal/' + l.token),
            create: async () => {
                if (!state.orderId) { Swal.fire({ icon: 'warning', title: 'Choose the order first' }); return; }
                state.busy = true;
                try {
                    const res = await AxiosManager.post('/portal/createlink', { entityType: state.kind, entityId: state.orderId, expiresInDays: state.days || 30 });
                    state.orderId = '';
                    await handler.fetch();
                    await handler.copyText(origin + '/portal/' + res?.data?.content?.token);
                } catch (e) { err('Could not create the link', e); } finally { state.busy = false; }
            },
            off: async (l) => {
                const ask = await Swal.fire({ title: 'Switch off the link for ' + l.orderNumber + '?', text: 'The partner will no longer be able to open it.', icon: 'question', showCancelButton: true, confirmButtonText: 'Switch off' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/portal/deactivate', { id: l.id }); await handler.fetch(); } catch (e) { err('Could not switch off', e); }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['PurchaseOrders', 'SalesOrders']);
                await SecurityManager.validateToken();
                await handler.fetch();
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
