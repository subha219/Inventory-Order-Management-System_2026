const App = {
    setup() {
        const state = Vue.reactive({
            loading: true, saving: false, search: '', orders: [], selected: '', orderNumber: '', shipTo: null, lines: [], shipments: [], edit: {}
        });

        const methods = {
            n: (v) => (v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 }),
            day: (d) => (d ? new Date(d).toLocaleDateString(undefined, { day: '2-digit', month: 'short' }) : '-'),
            when: (d) => (d ? new Date(d.endsWith('Z') ? d : d + 'Z').toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '-'),
            pct: (o) => (o.ordered > 0 ? Math.min(100, Math.round(o.packed / o.ordered * 100)) : 0),
            label: (o) => (o.shippedCount > 0 && o.packed + 1e-9 >= o.ordered ? 'Shipped' : o.packed <= 0 ? 'To pack' : o.packed + 1e-9 >= o.ordered ? 'Packed' : 'Partial'),
            badge: (o) => (o.shippedCount > 0 && o.packed + 1e-9 >= o.ordered ? 'bg-success' : o.packed <= 0 ? 'bg-secondary' : o.packed + 1e-9 >= o.ordered ? 'bg-info text-dark' : 'bg-warning text-dark'),
            orders: () => {
                const q = state.search.trim().toLowerCase();
                return state.orders.filter(o => !q || (o.number || '').toLowerCase().includes(q) || (o.customer || '').toLowerCase().includes(q));
            },
            address: (a) => [a.street, a.city, a.state, a.zipCode, a.country].filter(Boolean).join(', ') + (a.phone ? ' | ' + a.phone : ''),
            size: (p) => [p.weightKg ? p.weightKg + ' kg' : '', (p.lengthCm && p.widthCm && p.heightCm) ? p.lengthCm + 'x' + p.widthCm + 'x' + p.heightCm + ' cm' : ''].filter(Boolean).join(', ')
        };

        const err = (title, e) => Swal.fire({ icon: 'error', title, text: e?.response?.data?.message || e.message });

        const handler = {
            fetch: async (id) => {
                const res = await AxiosManager.get('/Shipping/GetPacking?deliveryOrderId=' + encodeURIComponent(id || ''), {});
                const c = res?.data?.content ?? {};
                state.orders = c.orders ?? [];
                state.orderNumber = c.orderNumber || '';
                state.shipTo = c.shipTo || null;
                state.lines = c.lines ?? [];
                state.shipments = c.shipments ?? [];
                const edit = {};
                state.shipments.filter(s => s.status !== 'Shipped').forEach(s => {
                    const keep = state.edit[s.id];
                    edit[s.id] = {
                        carrier: keep?.carrier ?? s.carrier ?? '', tracking: keep?.tracking ?? s.trackingNumber ?? '', notes: keep?.notes ?? s.notes ?? '',
                        packages: s.packages.map(p => ({ packageType: p.packageType || 'Carton', weightKg: p.weightKg, lengthCm: p.lengthCm, widthCm: p.widthCm, heightCm: p.heightCm,
                            qty: Object.fromEntries(p.items.map(i => [i.productId, i.quantity])) }))
                    };
                });
                state.edit = edit;
            },
            select: async (id) => {
                state.selected = id;
                try { await handler.fetch(id); } catch (e) { err('Could not load the order', e); }
            },
            createShipment: async () => {
                state.saving = true;
                try { await AxiosManager.post('/Shipping/CreateShipment', { deliveryOrderId: state.selected }); await handler.fetch(state.selected); }
                catch (e) { err('Could not create the shipment', e); } finally { state.saving = false; }
            },
            addCarton: (sid) => state.edit[sid].packages.push({ packageType: 'Carton', weightKg: null, lengthCm: null, widthCm: null, heightCm: null, qty: {} }),
            removeCarton: (sid, i) => state.edit[sid].packages.splice(i, 1),
            // left to pack = ordered - packed in other shipments - what is already typed into this shipment's cartons
            packRest: (sid) => {
                const e = state.edit[sid];
                const rest = {};
                state.lines.forEach(l => {
                    const mineSaved = (state.shipments.find(s => s.id === sid)?.packages ?? []).reduce((a, p) => a + p.items.filter(i => i.productId === l.productId).reduce((x, i) => x + i.quantity, 0), 0);
                    const typed = e.packages.reduce((a, p) => a + (Number(p.qty[l.productId]) || 0), 0);
                    const left = l.ordered - (l.packed - mineSaved) - typed;
                    if (left > 1e-9) rest[l.productId] = Math.round(left * 1e6) / 1e6;
                });
                if (!Object.keys(rest).length) { Swal.fire({ icon: 'info', title: 'Everything is already packed' }); return; }
                e.packages.push({ packageType: 'Carton', weightKg: null, lengthCm: null, widthCm: null, heightCm: null, qty: rest });
            },
            savePackages: async (s) => {
                const e = state.edit[s.id];
                const packages = e.packages.map(p => ({
                    packageType: p.packageType, weightKg: p.weightKg || null, lengthCm: p.lengthCm || null, widthCm: p.widthCm || null, heightCm: p.heightCm || null,
                    items: Object.keys(p.qty).map(k => ({ productId: k, quantity: Number(p.qty[k]) || 0 })).filter(i => i.quantity > 0)
                }));
                state.saving = true;
                try {
                    await AxiosManager.post('/Shipping/SavePackages', { shipmentId: s.id, packages });
                    await handler.fetch(state.selected);
                    Swal.fire({ icon: 'success', title: 'Packing saved', timer: 1000, showConfirmButton: false });
                } catch (x) { err('Could not save the packing', x); } finally { state.saving = false; }
            },
            ship: async (s) => {
                const e = state.edit[s.id];
                if (!(e.carrier || '').trim()) { Swal.fire({ icon: 'warning', title: 'Enter the carrier first' }); return; }
                const open = state.lines.some(l => l.ordered - l.packed > 1e-9);
                const ask = await Swal.fire({
                    title: 'Mark ' + s.number + ' as shipped?', icon: 'question', showCancelButton: true, confirmButtonText: 'Ship',
                    text: open ? 'Some units of this order are not packed yet. They can go in a later shipment.' : 'This closes the shipment. It can no longer be changed.'
                });
                if (!ask.isConfirmed) return;
                state.saving = true;
                try {
                    const res = await AxiosManager.post('/Shipping/ShipShipment', { shipmentId: s.id, carrier: e.carrier, trackingNumber: e.tracking, notes: e.notes });
                    await handler.fetch(state.selected);
                    const note = res?.data?.content?.note;
                    await Swal.fire(note ? { icon: 'info', title: 'Shipped', text: note } : { icon: 'success', title: 'Shipped', timer: 1200, showConfirmButton: false });
                } catch (x) { err('Could not ship', x); } finally { state.saving = false; }
            },
            remove: async (s) => {
                const ask = await Swal.fire({ title: 'Delete ' + s.number + '?', icon: 'warning', showCancelButton: true, confirmButtonText: 'Delete' });
                if (!ask.isConfirmed) return;
                try { await AxiosManager.post('/Shipping/DeleteShipment', { shipmentId: s.id }); await handler.fetch(state.selected); }
                catch (x) { err('Could not delete', x); }
            },
            slip: (s) => {
                const esc = (v) => String(v ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
                const to = state.shipTo;
                const body = s.packages.map(p => '<h3>' + esc(p.packageType) + ' ' + p.number + ' <small>' + esc(methods.size(p)) + '</small></h3><table><tr><th>Product</th><th>Code</th><th class="n">Qty</th></tr>' +
                    p.items.map(i => '<tr><td>' + esc(i.productName) + '</td><td>' + esc(i.productNumber) + '</td><td class="n">' + methods.n(i.quantity) + '</td></tr>').join('') + '</table>').join('');
                const w = window.open('', '_blank');
                if (!w) { Swal.fire({ icon: 'info', title: 'Allow pop-ups to print the packing slip' }); return; }
                w.document.write('<html><head><title>Packing slip ' + esc(s.number) + '</title><style>body{font-family:Arial,sans-serif;margin:28px}h2{margin:0 0 4px}h3{margin:16px 0 6px;border-bottom:2px solid #000}h3 small{font-weight:normal;color:#555}table{width:100%;border-collapse:collapse}td,th{border:1px solid #999;padding:5px 8px;text-align:left;font-size:13px}.n{text-align:right}.meta{display:flex;gap:40px;margin:10px 0}</style></head><body><h2>Packing slip ' + esc(s.number) + '</h2><div>Order ' +
                    esc(state.orderNumber) + '</div><div class="meta"><div><b>Ship to</b><br>' + esc(to?.name) + '<br>' + esc(to ? methods.address(to) : '') + '</div><div><b>Carrier</b><br>' + esc(s.carrier || '-') + '<br>' + esc(s.trackingNumber || '') +
                    '</div><div><b>Total weight</b><br>' + methods.n(s.totalWeightKg) + ' kg</div></div>' + body + '</body></html>');
                w.document.close(); w.focus(); w.print();
            },
            // one 4 x 6 inch label per carton with the destination and a scannable shipment/carton code
            labels: (s) => {
                const esc = (v) => String(v ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
                const to = state.shipTo;
                const total = s.packages.length;
                const code = (p) => {
                    const value = s.number + '-' + p.number;
                    if (typeof JsBarcode === 'undefined') return '<div style="font:bold 16px monospace">' + esc(value) + '</div>';
                    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                    JsBarcode(svg, value, { format: 'CODE128', width: 2, height: 60, displayValue: true, fontSize: 14, margin: 0 });
                    return svg.outerHTML;
                };
                const pages = s.packages.map(p => '<section><div class="top"><b>' + esc(s.carrier || 'CARRIER') + '</b><span>Carton ' + p.number + ' of ' + total + '</span></div>' +
                    '<div class="to"><small>SHIP TO</small><div class="name">' + esc(to?.name) + '</div><div>' + esc(to ? methods.address(to) : '') + '</div><div>' + esc(to?.phone || '') + '</div></div>' +
                    '<div class="row"><div>Order<br><b>' + esc(state.orderNumber) + '</b></div><div>Shipment<br><b>' + esc(s.number) + '</b></div><div>Weight<br><b>' + (p.weightKg ? methods.n(p.weightKg) + ' kg' : '-') + '</b></div></div>' +
                    (s.trackingNumber ? '<div class="trk">Tracking: ' + esc(s.trackingNumber) + '</div>' : '') +
                    '<div class="bc">' + code(p) + '</div></section>').join('');
                const w = window.open('', '_blank');
                if (!w) { Swal.fire({ icon: 'info', title: 'Allow pop-ups to print the labels' }); return; }
                w.document.write('<html><head><title>Labels ' + esc(s.number) + '</title><style>@page{size:4in 6in;margin:0}body{margin:0;font-family:Arial,sans-serif}section{width:4in;height:6in;box-sizing:border-box;padding:.25in;page-break-after:always;border:1px dashed #bbb;display:flex;flex-direction:column;gap:.15in}' +
                    '.top{display:flex;justify-content:space-between;font-size:18px;border-bottom:3px solid #000;padding-bottom:6px}.to small{color:#555}.name{font-size:22px;font-weight:bold;margin:4px 0}.to{font-size:16px;flex:1}' +
                    '.row{display:flex;justify-content:space-between;font-size:13px;border-top:1px solid #000;padding-top:6px}.trk{font-size:13px}.bc{text-align:center}.bc svg{max-width:100%}@media print{section{border:none}}</style></head><body>' + pages + '</body></html>');
                w.document.close(); w.focus(); setTimeout(() => w.print(), 300);
            },
            // delivery note with signature boxes, handed to the driver
            deliveryNote: (s) => {
                const esc = (v) => String(v ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
                const to = state.shipTo;
                const totals = {};
                s.packages.forEach(p => p.items.forEach(i => { const k = i.productNumber + '|' + i.productName; totals[k] = (totals[k] || 0) + i.quantity; }));
                const rows = Object.keys(totals).map(k => { const [num, name] = k.split('|'); return '<tr><td>' + esc(num) + '</td><td>' + esc(name) + '</td><td class="n">' + methods.n(totals[k]) + '</td></tr>'; }).join('');
                const w = window.open('', '_blank');
                if (!w) { Swal.fire({ icon: 'info', title: 'Allow pop-ups to print the delivery note' }); return; }
                w.document.write('<html><head><title>Delivery note ' + esc(s.number) + '</title><style>body{font-family:Arial,sans-serif;margin:28px}h2{margin:0 0 4px}table{width:100%;border-collapse:collapse;margin-top:12px}td,th{border:1px solid #999;padding:6px 8px;text-align:left;font-size:13px}.n{text-align:right}.meta{display:flex;gap:40px;margin:12px 0}.sign{display:flex;gap:40px;margin-top:50px}.sign div{flex:1;border-top:1px solid #000;padding-top:4px;font-size:12px}</style></head><body>' +
                    '<h2>Delivery note ' + esc(s.number) + '</h2><div>Order ' + esc(state.orderNumber) + '</div><div class="meta"><div><b>Deliver to</b><br>' + esc(to?.name) + '<br>' + esc(to ? methods.address(to) : '') + '</div><div><b>Carrier</b><br>' + esc(s.carrier || '-') + '<br>' + esc(s.trackingNumber || '') +
                    '</div><div><b>Cartons</b><br>' + s.packages.length + '</div><div><b>Total weight</b><br>' + methods.n(s.totalWeightKg) + ' kg</div></div><table><tr><th>Code</th><th>Item</th><th class="n">Quantity</th></tr>' + rows + '</table>' +
                    '<div class="sign"><div>Released by (name, signature, date)</div><div>Driver (name, signature, date)</div><div>Received by customer (name, signature, date)</div></div></body></html>');
                w.document.close(); w.focus(); w.print();
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Warehouses']);
                await SecurityManager.validateToken();
                await handler.fetch('');
            } catch (e) { console.error('page init error:', e); } finally { state.loading = false; }
        });

        return { state, methods, handler };
    }
};

Vue.createApp(App).mount('#app');
