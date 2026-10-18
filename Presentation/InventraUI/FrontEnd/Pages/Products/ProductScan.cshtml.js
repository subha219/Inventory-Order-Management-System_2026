const App = {
    setup() {
        const state = Vue.reactive({
            loading: true,
            products: [],
            stock: [],
            query: '',
            lastCode: '',
            notFound: false,
            matches: [],
            result: null,
            stockRows: [],
            totalStock: 0,
            status: { text: '', cls: 'bg-secondary' },
            copies: 1,
            onlyInStock: true,
            bulkCount: 0,
            scanning: false,
            cameraError: ''
        });

        const queryRef = Vue.ref(null);
        let qr = null;

        // ---------- helpers ----------
        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 });
        const methods = {
            num: (v) => nf.format(v || 0),
            money: (v) => nf.format(v || 0),
            escapeHtml: (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])),
            norm: (s) => String(s ?? '').trim().toLowerCase(),
            stockOf: (productId) => state.stock
                .filter(x => x.productId === productId)
                .reduce((sum, x) => sum + (x.stock || 0), 0),
            refreshBulkCount: () => {
                state.bulkCount = state.products.filter(p => p.number && p.physical !== false &&
                    (!state.onlyInStock || methods.stockOf(p.id) > 0)).length;
            },
            computeStatus: (p, total) => {
                if (p.physical === false) return { text: 'Service / non-stock item', cls: 'bg-secondary' };
                if (total <= 0) return { text: 'Out of stock', cls: 'bg-danger' };
                if (p.reorderPoint != null && total <= p.reorderPoint) return { text: 'Low stock', cls: 'bg-warning text-dark' };
                return { text: 'In stock', cls: 'bg-success' };
            },
            barcodeSvg: (value) => {
                const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                JsBarcode(svg, value, { format: 'CODE128', width: 2, height: 46, displayValue: true, fontSize: 14, margin: 0 });
                return svg.outerHTML;
            },
            labelHtml: (p) => `
                <div class="label">
                    <div class="name">${methods.escapeHtml(p.name)}</div>
                    ${methods.barcodeSvg(p.number)}
                    <div class="price">${methods.escapeHtml(methods.money(p.unitPrice))}</div>
                </div>`,
            printLabels: (list) => {
                if (!list.length) return;
                if (typeof JsBarcode === 'undefined') {
                    Swal.fire({ icon: 'error', title: 'Barcode library not loaded' });
                    return;
                }
                const html = `<!doctype html><html><head><meta charset="utf-8"><title>Labels</title>
                    <style>
                        @page { margin: 8mm; }
                        body { font-family: Arial, sans-serif; margin: 0; }
                        .sheet { display: flex; flex-wrap: wrap; }
                        .label { width: 62mm; height: 34mm; box-sizing: border-box; padding: 2mm 3mm; border: 1px dashed #bbb;
                                 text-align: center; overflow: hidden; page-break-inside: avoid; }
                        .name { font-size: 11px; font-weight: bold; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
                        .price { font-size: 12px; margin-top: 1px; }
                        svg { max-width: 100%; height: 17mm; }
                        @media print { .label { border-color: transparent; } }
                    </style></head><body><div class="sheet">${list.map(methods.labelHtml).join('')}</div></body></html>`;

                const frame = document.createElement('iframe');
                frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;';
                document.body.appendChild(frame);
                frame.contentDocument.open();
                frame.contentDocument.write(html);
                frame.contentDocument.close();
                setTimeout(() => {
                    frame.contentWindow.focus();
                    frame.contentWindow.print();
                    setTimeout(() => frame.remove(), 2000);
                }, 300);
            }
        };

        // ---------- data ----------
        const services = {
            getProducts: async () => AxiosManager.get('/Product/GetProductList', {}),
            getStock: async () => AxiosManager.get('/InventoryTransaction/GetInventoryStockList', {})
        };

        const loadData = async () => {
            state.loading = true;
            try {
                const [p, s] = await Promise.all([services.getProducts(), services.getStock()]);
                state.products = p?.data?.content?.data ?? [];
                state.stock = s?.data?.content?.data ?? [];
                methods.refreshBulkCount();
            } finally {
                state.loading = false;
            }
        };

        // ---------- actions ----------
        const handler = {
            select: (product) => {
                state.result = product;
                state.matches = [];
                state.notFound = false;
                state.stockRows = state.stock.filter(x => x.productId === product.id);
                state.totalStock = methods.stockOf(product.id);
                state.status = methods.computeStatus(product, state.totalStock);
                state.copies = 1;
            },
            lookup: (code) => {
                const q = methods.norm(code);
                state.lastCode = String(code ?? '').trim();
                state.matches = [];
                state.notFound = false;
                if (!q) return;

                let hits = state.products.filter(p => methods.norm(p.number) === q);
                if (!hits.length) hits = state.products.filter(p => methods.norm(p.name) === q);
                if (!hits.length) {
                    hits = state.products
                        .filter(p => methods.norm(p.number).includes(q) || methods.norm(p.name).includes(q))
                        .slice(0, 8);
                }

                if (hits.length === 1) {
                    handler.select(hits[0]);
                } else if (hits.length > 1) {
                    state.result = null;
                    state.matches = hits;
                } else {
                    state.result = null;
                    state.notFound = true;
                }
                // ready for the next scan
                state.query = '';
                Vue.nextTick(() => queryRef.value?.focus());
            },
            startCamera: async () => {
                state.cameraError = '';
                if (typeof Html5Qrcode === 'undefined') {
                    state.cameraError = 'Camera scanner library did not load.';
                    return;
                }
                if (!window.isSecureContext) {
                    state.cameraError = 'The camera needs HTTPS (or localhost). Open this site over https:// on your phone.';
                    return;
                }
                try {
                    state.scanning = true;
                    await Vue.nextTick();
                    qr = new Html5Qrcode('reader');
                    await qr.start(
                        { facingMode: 'environment' },
                        { fps: 10, qrbox: { width: 260, height: 140 } },
                        (decodedText) => {
                            handler.lookup(decodedText);
                            handler.stopCamera();
                        },
                        () => { /* ignore per-frame decode misses */ }
                    );
                } catch (e) {
                    state.scanning = false;
                    state.cameraError = 'Could not start the camera: ' + (e?.message || e);
                }
            },
            stopCamera: async () => {
                try {
                    if (qr) {
                        if (qr.isScanning) await qr.stop();
                        qr.clear();
                    }
                } catch (e) { /* ignore */ }
                qr = null;
                state.scanning = false;
            },
            printOne: () => {
                if (!state.result?.number) return;
                const copies = Math.min(Math.max(parseInt(state.copies) || 1, 1), 200);
                methods.printLabels(Array.from({ length: copies }, () => state.result));
            },
            printAll: () => {
                const list = state.products.filter(p => p.number && p.physical !== false &&
                    (!state.onlyInStock || methods.stockOf(p.id) > 0));
                if (!list.length) {
                    Swal.fire({ icon: 'info', title: 'Nothing to print', text: 'No matching products.' });
                    return;
                }
                if (list.length > 300) {
                    Swal.fire({ icon: 'warning', title: 'Too many labels', text: 'Please filter first (max 300 per print).' });
                    return;
                }
                methods.printLabels(list);
            }
        };

        Vue.watch(() => state.onlyInStock, methods.refreshBulkCount);

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                await loadData();
                queryRef.value?.focus();
            } catch (e) {
                console.error('page init error:', e);
            }
        });

        Vue.onBeforeUnmount(() => handler.stopCamera());

        return { state, queryRef, handler, methods };
    }
};

Vue.createApp(App).mount('#app');
