const App = {
    setup() {
        const hasRole = (r) => { try { return (StorageManager.getUserRoles() || []).some(x => String(x).toLowerCase() === r.toLowerCase()); } catch (e) { return false; } };
        const state = Vue.reactive({
            canFull: hasRole('Dashboards'),
            work: [],
            workLoaded: false,
            cardsData: {},
            salesData: {},
            purchaseData: {},
            inventoryData: {},
            insights: null,
            batchSummary: null,
            analytics: null,
            analyticsDays: 30,
            analyticsLoading: false,
            hoverIdx: -1
        });

        const cardSalesQtyRef = Vue.ref(null);
        const cardSalesReturnQtyRef = Vue.ref(null);
        const cardPurchaseQtyRef = Vue.ref(null);
        const cardPurchaseReturnQtyRef = Vue.ref(null);
        const cardDeliveryOrderQtyRef = Vue.ref(null);
        const cardGoodsReceiveQtyRef = Vue.ref(null);
        const cardTransferOutQtyRef = Vue.ref(null);
        const cardTransferInQtyRef = Vue.ref(null);

        const salesOrderGridRef = Vue.ref(null);
        const inventoryTransactionGridRef = Vue.ref(null);
        const purchaseOrderGridRef = Vue.ref(null);
        const customerGroupChartRef = Vue.ref(null);
        const vendorGroupChartRef = Vue.ref(null);
        const customerCategoryChartRef = Vue.ref(null);
        const vendorCategoryChartRef = Vue.ref(null);
        const stockChartRef = Vue.ref(null);

        const services = {
            getCardsData: async () => {
                try {
                    const response = await AxiosManager.get('/Dashboard/GetCardsDashboard', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getSalesData: async () => {
                try {
                    const response = await AxiosManager.get('/Dashboard/GetSalesDashboard', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getPurchaseData: async () => {
                try {
                    const response = await AxiosManager.get('/Dashboard/GetPurchaseDashboard', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
            getInventoryData: async () => {
                try {
                    const response = await AxiosManager.get('/Dashboard/GetInventoryDashboard', {});
                    return response;
                } catch (error) {
                    throw error;
                }
            },
        };

        // Helpers for the analytics section (chart geometry, formatting, colours).
        const analytics = (() => {
            const L = 44, R = 636, T = 12, B = 232;
            const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });
            const pts = () => state.analytics?.daily ?? [];
            const niceMax = () => {
                const m = Math.max(1, ...pts().map(d => Math.max(d.sales, d.purchases)));
                const p = Math.pow(10, Math.floor(Math.log10(m)));
                const f = m / p;
                return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * p;
            };
            const step = () => (R - L) / Math.max(pts().length - 1, 1);
            const x = (i) => (pts().length <= 1 ? (L + R) / 2 : L + i * step());
            const y = (v) => B - ((v || 0) / niceMax()) * (B - T);
            const compact = (v) => v >= 1e6 ? (v / 1e6).toFixed(1).replace(/\.0$/, '') + 'M' : v >= 1e3 ? (v / 1e3).toFixed(1).replace(/\.0$/, '') + 'k' : String(Math.round(v));
            return {
                money: (v) => nf.format(v || 0),
                pct: (v, max) => (max > 0 ? Math.max(3, Math.round((v / max) * 100)) : 0),
                net: () => (state.analytics ? state.analytics.revenue.current - state.analytics.spend.current : 0),
                deltaClass: (c) => (c == null ? 'flat' : c > 0.05 ? 'up' : c < -0.05 ? 'down' : 'flat'),
                deltaText: (c) => (c == null ? 'new' : (c > 0 ? '+' : '') + c + '%'),
                x, step,
                grid: () => [0, 0.25, 0.5, 0.75, 1].map(f => ({ y: B - f * (B - T), label: compact(niceMax() * f) })),
                ticks: () => {
                    const d = pts(); if (!d.length) return [];
                    const every = Math.max(1, Math.ceil(d.length / 7));
                    return d.map((p, i) => ({ x: x(i), label: p.date, i })).filter(t => t.i % every === 0);
                },
                line: (k) => pts().map((d, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(d[k]).toFixed(1)).join(' '),
                area: (k) => {
                    const d = pts(); if (!d.length) return '';
                    return pts().map((p, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(p[k]).toFixed(1)).join(' ') +
                        ' L' + x(d.length - 1).toFixed(1) + ' ' + B + ' L' + x(0).toFixed(1) + ' ' + B + ' Z';
                },
                healthColor: () => { const s = state.analytics?.healthScore ?? 0; return s >= 80 ? 'var(--good)' : s >= 55 ? 'var(--warn)' : 'var(--bad)'; },
                healthLabel: () => { const s = state.analytics?.healthScore ?? 0; return s >= 80 ? 'Healthy' : s >= 55 ? 'Needs care' : 'At risk'; },
                insightIcon: (l) => ({ good: 'fa-check-circle', info: 'fa-info-circle', warning: 'fa-exclamation-triangle', danger: 'fa-exclamation-circle' }[l] || 'fa-info-circle'),
                abcColor: (c) => ({ A: '#0b7f72', B: '#f5a524', C: '#8da1b4' }[c] || '#8da1b4'),
                donut: () => {
                    const abc = state.analytics?.abc ?? [];
                    const total = abc.reduce((s, b) => s + b.sharePercent, 0);
                    if (!total) return 'var(--surface-2)';
                    let acc = 0;
                    const colors = { A: '#0b7f72', B: '#f5a524', C: '#8da1b4' };
                    return 'conic-gradient(' + abc.map(b => { const a = acc; acc += b.sharePercent / total * 100; return colors[b.class] + ' ' + a + '% ' + acc + '%'; }).join(',') + ')';
                }
            };
        })();

        // Formatting + scaling helpers for the insights section.
        const nf = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });
        const insights = {
            num: (v) => nf.format(v || 0),
            money: (v) => new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(v || 0),
            barHeight: (v) => {
                const rows = state.insights?.dailyMovement ?? [];
                const max = Math.max(1, ...rows.map(r => Math.max(r.unitsIn, r.unitsOut)));
                return Math.round(((v || 0) / max) * 150);
            },
            valuePct: (v) => {
                const rows = state.insights?.topValueItems ?? [];
                const max = Math.max(1, ...rows.map(r => r.value));
                return Math.round(((v || 0) / max) * 100);
            }
        };

        const methods = {
            populateMyWork: async () => {
                const response = await AxiosManager.get('/Dashboard/GetMyWork', {});
                state.work = response?.data?.content?.data?.widgets ?? response?.data?.content?.widgets ?? [];
                state.workLoaded = true;
            },
            populateInsightsData: async () => {
                const response = await AxiosManager.get('/Dashboard/GetInsightsDashboard', {});
                state.insights = response?.data?.content?.data ?? null;
            },
            populateAnalytics: async () => {
                state.analyticsLoading = true;
                try {
                    const response = await AxiosManager.get('/Dashboard/GetAnalyticsDashboard?days=' + state.analyticsDays, {});
                    state.analytics = response?.data?.content?.data ?? null;
                    state.hoverIdx = -1;
                } finally {
                    state.analyticsLoading = false;
                }
            },
            setAnalyticsDays: async (d) => {
                if (state.analyticsDays === d) return;
                state.analyticsDays = d;
                try { await methods.populateAnalytics(); } catch (e) { console.error('analytics load error:', e); }
            },
            populateBatchSummary: async () => {
                const response = await AxiosManager.get('/ProductBatch/GetProductBatchList?expiringDays=30', {});
                state.batchSummary = response?.data?.content?.summary ?? null;
            },
            populateCardsData: async () => {
                const response = await services.getCardsData();
                state.cardsData = response?.data?.content?.data;
                methods.populateCards();
            },
            populateSalesData: async () => {
                const response = await services.getSalesData();
                state.salesData = response?.data?.content?.data;
                methods.populateSalesOrderGrid();
                methods.populateSalesByCustomerGroupChart();
                methods.populateSalesByCustomerCategoryChart();
            },
            populatePurchaseData: async () => {
                const response = await services.getPurchaseData();
                state.purchaseData = response?.data?.content?.data;
                methods.populatePurchaseOrderGrid();
                methods.populatePurchaseByVendorGroupChart();
                methods.populatePurchaseByVendorCategoryChart();
            },
            populateInventoryData: async () => {
                const response = await services.getInventoryData();
                state.inventoryData = response?.data?.content?.data;
                methods.populateInventoryTransactionGrid();
                methods.populateInventoryStockChart();
            },
            populateCards: () => {
                const cardsDashboard = state.cardsData?.cardsDashboard;

                if (cardsDashboard) {
                    cardSalesQtyRef.value.textContent = cardsDashboard.salesTotal || 0;
                    cardSalesReturnQtyRef.value.textContent = cardsDashboard.salesReturnTotal || 0;
                    cardPurchaseQtyRef.value.textContent = cardsDashboard.purchaseTotal || 0;
                    cardPurchaseReturnQtyRef.value.textContent = cardsDashboard.purchaseReturnTotal || 0;
                    cardDeliveryOrderQtyRef.value.textContent = cardsDashboard.deliveryOrderTotal || 0;
                    cardGoodsReceiveQtyRef.value.textContent = cardsDashboard.goodsReceiveTotal || 0;
                    cardTransferOutQtyRef.value.textContent = cardsDashboard.transferOutTotal || 0;
                    cardTransferInQtyRef.value.textContent = cardsDashboard.transferInTotal || 0;
                } else {
                    console.error('CardsDashboard data is not available.');
                }
            },
            populateSalesOrderGrid: () => {
                const salesOrderDashboard = state.salesData?.salesOrderDashboard ?? [];
                new ej.grids.Grid({
                    dataSource: salesOrderDashboard,
                    allowFiltering: false,
                    allowSorting: true,
                    allowSelection: false,
                    allowGrouping: false,
                    allowTextWrap: false,
                    allowResizing: false,
                    allowPaging: true,
                    allowExcelExport: false,
                    sortSettings: { columns: [{ field: 'orderDate', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 10 },
                    autoFit: false,
                    showColumnMenu: false,
                    gridLines: 'Horizontal',
                    columns: [
                        {
                            field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false
                        },
                        { field: 'salesOrder.orderDate', headerText: 'Order Date', width: 70, type: 'dateTime', format: 'yyyy-MM-dd', textAlign: 'Left' },
                        { field: 'salesOrder.number', headerText: '#Number', width: 90 },
                        { field: 'product.name', headerText: 'Product', width: 150 },
                        { field: 'total', headerText: 'Total', width: 70, type: 'number', format: 'N2', textAlign: 'Right' },
                    ],
                }, salesOrderGridRef.value);
            },
            populateInventoryTransactionGrid: () => {
                const inventoryTransactionDashboard = state.inventoryData?.inventoryTransactionDashboard ?? [];
                new ej.grids.Grid({
                    dataSource: inventoryTransactionDashboard,
                    allowFiltering: false,
                    allowSorting: true,
                    allowSelection: false,
                    allowGrouping: false,
                    allowTextWrap: false,
                    allowResizing: false,
                    allowPaging: true,
                    allowExcelExport: false,
                    sortSettings: { columns: [{ field: 'movementDate', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 10 },
                    autoFit: false,
                    showColumnMenu: false,
                    gridLines: 'Horizontal',
                    columns: [
                        {
                            field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false
                        },
                        { field: 'movementDate', headerText: 'Date', width: 150, format: 'yyyy-MM-dd', textAlign: 'Left', type: 'dateTime' },
                        { field: 'warehouse.name', headerText: 'Warehouse', width: 150 },
                        { field: 'product.name', headerText: 'Product', width: 150 },
                        { field: 'number', headerText: 'Number', width: 150 },
                        { field: 'stock', headerText: 'Movement', width: 100, type: 'number', format: '+0.00;-0.00;0.00', textAlign: 'Right' },
                        { field: 'moduleName', headerText: 'Module Name', width: 150 },
                        { field: 'moduleCode', headerText: 'Module Code', width: 150 },
                        { field: 'moduleNumber', headerText: 'Module Number', width: 150 },
                        { field: 'warehouseFrom.name', headerText: 'Warehouse From', width: 150 },
                        { field: 'warehouseTo.name', headerText: 'Warehouse To', width: 150 },
                    ],
                }, inventoryTransactionGridRef.value);
            },
            populatePurchaseOrderGrid: () => {
                const purchaseOrderDashboard = state.purchaseData?.purchaseOrderDashboard ?? [];
                new ej.grids.Grid({
                    dataSource: purchaseOrderDashboard,
                    allowFiltering: false,
                    allowSorting: true,
                    allowSelection: false,
                    allowGrouping: false,
                    allowTextWrap: false,
                    allowResizing: false,
                    allowPaging: true,
                    allowExcelExport: false,
                    sortSettings: { columns: [{ field: 'orderDate', direction: 'Descending' }] },
                    pageSettings: { currentPage: 1, pageSize: 10 },
                    autoFit: false,
                    showColumnMenu: false,
                    gridLines: 'Horizontal',
                    columns: [
                        {
                            field: 'id', isPrimaryKey: true, headerText: 'Id', visible: false
                        },
                        { field: 'purchaseOrder.orderDate', headerText: 'Order Date', width: 70, type: 'dateTime', format: 'yyyy-MM-dd', textAlign: 'Left' },
                        { field: 'purchaseOrder.number', headerText: '#Number', width: 90 },
                        { field: 'product.name', headerText: 'Product', width: 150 },
                        { field: 'total', headerText: 'Total', width: 70, type: 'number', format: 'N2', textAlign: 'Right' },
                    ],
                }, purchaseOrderGridRef.value);
            },
            populateSalesByCustomerGroupChart: () => {
                const salesByCustomerGroupDashboard = state.salesData?.salesByCustomerGroupDashboard ?? [];
                WhmsCharts.render(customerGroupChartRef.value, salesByCustomerGroupDashboard, { unit: 'Quantity' });
            },
            populatePurchaseByVendorGroupChart: () => {
                const purchaseByVendorGroupDashboard = state.purchaseData?.purchaseByVendorGroupDashboard ?? [];
                WhmsCharts.render(vendorGroupChartRef.value, purchaseByVendorGroupDashboard, { unit: 'Quantity' });
            },
            populateSalesByCustomerCategoryChart: () => {
                const salesByCustomerCategoryDashboard = state.salesData?.salesByCustomerCategoryDashboard ?? [];
                WhmsCharts.render(customerCategoryChartRef.value, salesByCustomerCategoryDashboard, { unit: 'Quantity' });
            },
            populatePurchaseByVendorCategoryChart: () => {
                const purchaseByVendorCategoryDashboard = state.purchaseData?.purchaseByVendorCategoryDashboard ?? [];
                WhmsCharts.render(vendorCategoryChartRef.value, purchaseByVendorCategoryDashboard, { unit: 'Quantity' });
            },
            populateInventoryStockChart: () => {
                const inventoryStockDashboard = state.inventoryData?.inventoryStockDashboard ?? [];
                WhmsCharts.render(stockChartRef.value, inventoryStockDashboard, { unit: 'Quantity' });
            },
        };

        Vue.onMounted(async () => {
            try {
                await SecurityManager.validateToken();

                if (!state.canFull) return;

                try {
                    await methods.populateInsightsData();
                } catch (insightsError) {
                    console.error('insights load error:', insightsError);
                }

                try {
                    await methods.populateAnalytics();
                } catch (analyticsError) {
                    console.error('analytics load error:', analyticsError);
                }

                await methods.populateCardsData();
                await methods.populateSalesData();
                await methods.populatePurchaseData();
                await methods.populateInventoryData();

            } catch (e) {
                console.error('page init error:', e);
            }
        });

        return {
            cardSalesQtyRef,
            cardSalesReturnQtyRef,
            cardPurchaseQtyRef,
            cardPurchaseReturnQtyRef,
            cardDeliveryOrderQtyRef,
            cardGoodsReceiveQtyRef,
            cardTransferOutQtyRef,
            cardTransferInQtyRef,
            salesOrderGridRef,
            inventoryTransactionGridRef,
            purchaseOrderGridRef,
            customerGroupChartRef,
            vendorGroupChartRef,
            customerCategoryChartRef,
            vendorCategoryChartRef,
            stockChartRef,
            state,
            insights,
            analytics,
            methods
        };
    }
};

Vue.createApp(App).mount('#app');