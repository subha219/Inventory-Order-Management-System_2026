const App = {
    setup() {
        const state = Vue.reactive({
            question: '',
            busy: false,
            history: [],
            suggestions: [
                'What is low on stock?',
                'Which products are out of stock?',
                'Top selling products this month',
                'What will run out in 2 weeks?',
                'Slow moving products in the last 60 days',
                'Total inventory value'
            ]
        });
        const inputRef = Vue.ref(null);

        const handler = {
            ask: async (text) => {
                const question = (text || '').trim();
                if (!question || state.busy) return;
                state.busy = true;
                try {
                    const response = await AxiosManager.get('/Product/AskInventory?question=' + encodeURIComponent(question), {});
                    const d = response?.data?.content ?? {};
                    state.history.unshift({
                        question,
                        answer: d.answer ?? '',
                        columns: d.columns ?? [],
                        rows: d.rows ?? []
                    });
                    if (d.suggestions && d.suggestions.length) state.suggestions = d.suggestions;
                    state.question = '';
                } catch (e) {
                    console.error(e);
                    Swal.fire({ icon: 'error', title: 'Could not answer that', text: e?.response?.data?.message || e.message });
                } finally {
                    state.busy = false;
                    inputRef.value?.focus();
                }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                inputRef.value?.focus();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return { state, handler, inputRef };
    }
};

Vue.createApp(App).mount('#app');
