const App = {
    setup() {
        const SPECS = {
            products: {
                columns: ['Name', 'Description', 'UnitPrice', 'ReorderPoint', 'ReorderQuantity', 'ProductGroup', 'UnitMeasure', 'Physical'],
                aliases: { name: 'Name', productname: 'Name', description: 'Description', unitprice: 'UnitPrice', price: 'UnitPrice', reorderpoint: 'ReorderPoint', reorderquantity: 'ReorderQuantity', reorderqty: 'ReorderQuantity',
                           productgroup: 'ProductGroup', group: 'ProductGroup', unitmeasure: 'UnitMeasure', unit: 'UnitMeasure', uom: 'UnitMeasure', physical: 'Physical' },
                sample: [['Name', 'Description', 'UnitPrice', 'ReorderPoint', 'ReorderQuantity', 'ProductGroup', 'UnitMeasure', 'Physical'], ['Sample widget', 'Blue, 10 cm', '12.50', '20', '100', 'Widgets', 'Pcs', 'yes']],
                required: ['Name'], url: '/Import/ImportProducts', file: 'products-template.csv'
            },
            stock: {
                columns: ['Product', 'Quantity'],
                aliases: { product: 'Product', productnumber: 'Product', number: 'Product', sku: 'Product', name: 'Product', quantity: 'Quantity', qty: 'Quantity', stock: 'Quantity' },
                sample: [['Product', 'Quantity'], ['ART0001', '250'], ['Sample widget', '40']],
                required: ['Product', 'Quantity'], url: '/Import/ImportOpeningStock', file: 'opening-stock-template.csv'
            }
        };

        const PARTY_COLUMNS = ['Name', 'Description', 'Street', 'City', 'State', 'ZipCode', 'Country', 'PhoneNumber', 'EmailAddress', 'Website', 'Group', 'Category'];
        const PARTY_ALIASES = { name: 'Name', vendor: 'Name', vendorname: 'Name', customer: 'Name', customername: 'Name', description: 'Description', street: 'Street', address: 'Street',
            city: 'City', state: 'State', zipcode: 'ZipCode', zip: 'ZipCode', postalcode: 'ZipCode', postcode: 'ZipCode', country: 'Country', phonenumber: 'PhoneNumber', phone: 'PhoneNumber',
            emailaddress: 'EmailAddress', email: 'EmailAddress', website: 'Website', web: 'Website', group: 'Group', vendorgroup: 'Group', customergroup: 'Group', category: 'Category', vendorcategory: 'Category', customercategory: 'Category' };
        const PARTY_SAMPLE = [PARTY_COLUMNS, ['Sample Trading Co', 'Main supplier', '12 Market Road', 'Kolkata', 'West Bengal', '700001', 'India', '+91 90000 00000', 'orders@sample.example', 'https://sample.example', 'General', 'General']];
        SPECS.vendors = { columns: PARTY_COLUMNS, aliases: PARTY_ALIASES, sample: PARTY_SAMPLE, required: ['Name'], url: '/Import/ImportVendors', file: 'vendors-template.csv' };
        SPECS.customers = { columns: PARTY_COLUMNS, aliases: PARTY_ALIASES, sample: PARTY_SAMPLE, required: ['Name'], url: '/Import/ImportCustomers', file: 'customers-template.csv' };

        const state = Vue.reactive({
            tab: 'products', tabs: [{ key: 'products', label: 'Products' }, { key: 'stock', label: 'Opening stock' }, { key: 'vendors', label: 'Vendors' }, { key: 'customers', label: 'Customers' }],
            warehouses: [], warehouseId: '', date: new Date().toISOString().substring(0, 10),
            rows: [], fileName: '', error: '', result: null, checked: false, busy: false, done: ''
        });
        const fileRef = Vue.ref(null);

        // small CSV reader: quoted fields, doubled quotes, commas or semicolons, CRLF
        const parseCsv = (text) => {
            text = text.replace(/^﻿/, '');
            const first = text.split(/\r?\n/)[0] || '';
            const delim = (first.match(/;/g) || []).length > (first.match(/,/g) || []).length ? ';' : ',';
            const out = []; let row = [], cur = '', q = false;
            for (let i = 0; i < text.length; i++) {
                const c = text[i];
                if (q) { if (c === '"') { if (text[i + 1] === '"') { cur += '"'; i++; } else q = false; } else cur += c; }
                else if (c === '"') q = true;
                else if (c === delim) { row.push(cur); cur = ''; }
                else if (c === '\n' || c === '\r') { if (c === '\r' && text[i + 1] === '\n') i++; row.push(cur); out.push(row); row = []; cur = ''; }
                else cur += c;
            }
            if (cur.length || row.length) { row.push(cur); out.push(row); }
            return out.filter(r => r.some(v => String(v).trim() !== ''));
        };

        const methods = {
            columns: () => SPECS[state.tab].columns.filter(c => state.rows.some(r => r[c] != null && r[c] !== '')),
            res: (i) => (state.result && state.result.rows ? state.result.rows[i] : null),
            badge: (s) => ({ Ok: 'bg-success', Skipped: 'bg-warning text-dark', Error: 'bg-danger' }[s] || 'bg-secondary')
        };

        const handler = {
            setTab: (k) => {
                state.tab = k; state.rows = []; state.result = null; state.checked = false; state.error = ''; state.done = ''; state.fileName = '';
                if (fileRef.value) fileRef.value.value = '';
            },
            template: () => {
                const spec = SPECS[state.tab];
                const esc = (v) => '"' + String(v).replace(/"/g, '""') + '"';
                const blob = new Blob(['﻿' + spec.sample.map(r => r.map(esc).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = spec.file; a.click();
                setTimeout(() => URL.revokeObjectURL(a.href), 1000);
            },
            pick: async (ev) => {
                state.error = ''; state.result = null; state.checked = false; state.done = ''; state.rows = [];
                const f = ev.target.files && ev.target.files[0];
                if (!f) return;
                if (f.size > 5 * 1024 * 1024) { state.error = 'The file is larger than 5 MB.'; return; }
                try {
                    const table = parseCsv(await f.text());
                    if (table.length < 2) { state.error = 'The file needs a header row and at least one data row.'; return; }
                    const spec = SPECS[state.tab];
                    const map = {};
                    table[0].forEach((h, idx) => { const k = spec.aliases[String(h).trim().toLowerCase().replace(/[\s_-]+/g, '')]; if (k && map[k] == null) map[k] = idx; });
                    const missing = spec.required.filter(k => map[k] == null);
                    if (missing.length) { state.error = 'Missing column(s): ' + missing.join(', ') + '. Download the template to see the expected headers.'; return; }
                    state.rows = table.slice(1).map(r => { const o = {}; spec.columns.forEach(c => { o[c] = map[c] != null ? String(r[map[c]] ?? '').trim() : ''; }); return o; });
                    state.fileName = f.name;
                } catch (e) { state.error = 'Could not read the file: ' + e.message; }
            },
            run: async (dry) => {
                if (state.tab === 'stock' && !state.warehouseId) { Swal.fire({ icon: 'warning', title: 'Choose a warehouse first' }); return; }
                if (!dry) {
                    const ask = await Swal.fire({
                        title: 'Import ' + state.result.created + ' row(s)?', icon: 'question', showCancelButton: true, confirmButtonText: 'Import',
                        text: state.tab === 'stock' ? 'This creates a Draft positive adjustment. Stock moves when you confirm it.' : 'Records are created straight away.'
                    });
                    if (!ask.isConfirmed) return;
                }
                state.busy = true; state.done = '';
                try {
                    const spec = SPECS[state.tab];
                    const partyRow = (r) => ({ name: r.Name, description: r.Description, street: r.Street, city: r.City, state: r.State, zipCode: r.ZipCode, country: r.Country, phoneNumber: r.PhoneNumber, emailAddress: r.EmailAddress, website: r.Website, group: r.Group, category: r.Category });
                    const isParty = state.tab === 'vendors' || state.tab === 'customers';
                    const body = isParty ? { rows: state.rows.map(partyRow), dryRun: dry } : state.tab === 'products'
                        ? { rows: state.rows.map(r => ({ name: r.Name, description: r.Description, unitPrice: r.UnitPrice, reorderPoint: r.ReorderPoint, reorderQuantity: r.ReorderQuantity, productGroup: r.ProductGroup, unitMeasure: r.UnitMeasure, physical: r.Physical })), dryRun: dry }
                        : { warehouseId: state.warehouseId, date: state.date, rows: state.rows.map(r => ({ product: r.Product, quantity: r.Quantity })), dryRun: dry };
                    const res = await AxiosManager.post(spec.url, body);
                    state.result = res?.data?.content ?? null;
                    state.checked = dry;
                    if (!dry && state.result) {
                        state.done = state.tab !== 'stock'
                            ? state.result.created + ' ' + ({ products: 'product(s)', vendors: 'vendor(s)', customers: 'customer(s)' }[state.tab]) + ' created, ' + state.result.skipped + ' skipped, ' + state.result.errors + ' with errors.'
                            : 'Draft adjustment ' + (state.result.reference || '') + ' created with ' + state.result.created + ' line(s). Open Positive Adjustment to review and confirm it.';
                        state.checked = false;
                    }
                } catch (e) {
                    Swal.fire({ icon: 'error', title: dry ? 'Could not check the file' : 'Import failed', text: e?.response?.data?.message || e.message });
                } finally { state.busy = false; }
            }
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.authorizePage(['Products']);
                await SecurityManager.validateToken();
                const wh = await AxiosManager.get('/Warehouse/GetWarehouseList', {});
                state.warehouses = (wh?.data?.content?.data ?? []).filter(w => !w.systemWarehouse);
            } catch (e) { console.error('page init error:', e); }
        });

        return { state, methods, handler, fileRef };
    }
};

Vue.createApp(App).mount('#app');
