(function () {
    'use strict';

    const PALETTE = ['#0d9488', '#f59e0b', '#6366f1', '#ef4444', '#0ea5e9', '#84cc16', '#ec4899', '#8b5cf6'];
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 });
    const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });
    const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumFractionDigits: 1 });

    function fmt(value, unit, compactNums) {
        if (value === null || value === undefined || isNaN(value)) return '-';
        if (unit === 'money') return money.format(value);
        const n = compactNums && Math.abs(value) >= 10000 ? compact.format(value) : whole.format(value);
        if (unit === '%') return n + '%';
        if (unit === 'days' || unit === 'hours' || unit === 'min') return n + ' ' + unit;
        return n;
    }

    // A "nice" top value so the four gridlines land on tidy numbers (0, 50, 100, 150, 200).
    function niceMax(v) {
        if (v <= 0) return 4;
        const raw = v / 4, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
        const opts = p >= 10 ? [1, 2, 2.5, 5, 10] : [1, 2, 5, 10];
        const step = (opts.find(o => f <= o) || 10) * p;
        return step * 4;
    }

    function axisFmt(v, unit) { return unit === 'money' ? money.format(v) : compact.format(v); }

    function empty(msg) {
        return '<div class="pd-chart-empty">' + esc(msg || 'No data yet') + '</div>';
    }

    // ---------- tiny SVG charts (all text is escaped) ----------
    function sparkline(values, color) {
        if (!values || values.length < 2 || values.every(v => v === 0)) return '';
        const W = 120, H = 34, max = Math.max(...values), min = Math.min(...values), span = max - min || 1;
        const pts = values.map((v, i) => [i * (W / (values.length - 1)), H - 3 - ((v - min) / span) * (H - 8)]);
        const line = pts.map((p, i) => (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1)).join(' ');
        return '<svg class="pd-spark" viewBox="0 0 ' + W + ' ' + H + '" preserveAspectRatio="none" aria-hidden="true">' +
            '<path d="' + line + ' L' + W + ' ' + H + ' L0 ' + H + ' Z" fill="' + color + '" opacity=".12"/>' +
            '<path d="' + line + '" fill="none" stroke="' + color + '" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>';
    }

    function lineChart(c) {
        const W = 560, H = 230, L = 46, R = 12, T = 12, B = 28;
        const n = c.labels.length;
        const all = c.series.flatMap(s => s.values);
        if (!n || all.every(v => !v)) return empty('No activity in this period');
        const max = niceMax(Math.max(...all));
        const x = i => L + (n === 1 ? (W - L - R) / 2 : i * ((W - L - R) / (n - 1)));
        const y = v => T + (H - T - B) * (1 - v / max);
        let g = '';
        for (let k = 0; k <= 4; k++) {
            const v = max * k / 4;
            g += '<line x1="' + L + '" x2="' + (W - R) + '" y1="' + y(v) + '" y2="' + y(v) + '" class="pd-grid"/>' +
                '<text x="' + (L - 6) + '" y="' + (y(v) + 4) + '" text-anchor="end" class="pd-axis">' + esc(axisFmt(v, c.unit)) + '</text>';
        }
        const step = Math.ceil(n / 7);
        c.labels.forEach((l, i) => { if (i % step === 0 && i < n - 1 - Math.floor(step / 2) || i === n - 1) g += '<text x="' + x(i) + '" y="' + (H - 8) + '" text-anchor="' + (i === n - 1 ? 'end' : i === 0 ? 'start' : 'middle') + '" class="pd-axis">' + esc(l) + '</text>'; });
        c.series.forEach((s, si) => {
            const col = PALETTE[si % PALETTE.length];
            const line = s.values.map((v, i) => (i ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(v).toFixed(1)).join(' ');
            if (si === 0) g += '<path d="' + line + ' L' + x(n - 1) + ' ' + y(0) + ' L' + x(0) + ' ' + y(0) + ' Z" fill="' + col + '" opacity=".1"/>';
            g += '<path d="' + line + '" fill="none" stroke="' + col + '" stroke-width="2.2" stroke-linejoin="round" stroke-linecap="round"/>';
            s.values.forEach((v, i) => { g += '<circle cx="' + x(i) + '" cy="' + y(v) + '" r="' + (n > 16 ? 6 : 8) + '" fill="transparent"><title>' + esc(s.name + ' - ' + c.labels[i] + ': ' + fmt(v, c.unit)) + '</title></circle>'; });
        });
        return '<svg viewBox="0 0 ' + W + ' ' + H + '" class="pd-svg" role="img" aria-label="' + esc(c.title) + '">' + g + '</svg>';
    }

    function barChart(c) {
        const W = 560, H = 230, L = 46, R = 12, T = 12, B = 28;
        const n = c.labels.length, m = c.series.length;
        const all = c.series.flatMap(s => s.values);
        if (!n || all.every(v => !v)) return empty('No activity in this period');
        const max = niceMax(Math.max(...all));
        const band = (W - L - R) / n, bw = Math.min(26, (band * 0.72) / m);
        const y = v => T + (H - T - B) * (1 - v / max);
        let g = '';
        for (let k = 0; k <= 4; k++) {
            const v = max * k / 4;
            g += '<line x1="' + L + '" x2="' + (W - R) + '" y1="' + y(v) + '" y2="' + y(v) + '" class="pd-grid"/>' +
                '<text x="' + (L - 6) + '" y="' + (y(v) + 4) + '" text-anchor="end" class="pd-axis">' + esc(axisFmt(v, c.unit)) + '</text>';
        }
        const step = Math.ceil(n / 7);
        c.labels.forEach((l, i) => {
            const cx = L + band * i + band / 2;
            if (i % step === 0 && i < n - 1 - Math.floor(step / 2) || i === n - 1) g += '<text x="' + cx + '" y="' + (H - 8) + '" text-anchor="' + (i === n - 1 ? 'end' : 'middle') + '" class="pd-axis">' + esc(l) + '</text>';
            c.series.forEach((s, si) => {
                const v = s.values[i] || 0, bx = cx - (bw * m) / 2 + bw * si, h = y(0) - y(v);
                g += '<rect x="' + bx.toFixed(1) + '" y="' + y(v).toFixed(1) + '" width="' + (bw - 2).toFixed(1) + '" height="' + Math.max(h, v > 0 ? 1 : 0).toFixed(1) + '" rx="3" fill="' + PALETTE[si % PALETTE.length] + '"><title>' + esc(s.name + ' - ' + l + ': ' + fmt(v, c.unit)) + '</title></rect>';
            });
        });
        return '<svg viewBox="0 0 ' + W + ' ' + H + '" class="pd-svg" role="img" aria-label="' + esc(c.title) + '">' + g + '</svg>';
    }

    function hbarChart(c) {
        const vals = (c.series[0] || { values: [] }).values;
        if (!c.labels.length || vals.every(v => !v)) return empty('Nothing to compare yet');
        const max = Math.max(...vals) || 1;
        return '<div class="pd-hbars">' + c.labels.map((l, i) =>
            '<div class="pd-hbar"><span class="pd-hlabel" title="' + esc(l) + '">' + esc(l) + '</span>' +
            '<span class="pd-htrack"><span class="pd-hfill" style="width:' + Math.max(2, (vals[i] / max) * 100).toFixed(1) + '%;background:' + PALETTE[i % PALETTE.length] + '"></span></span>' +
            '<span class="pd-hval">' + esc(fmt(vals[i], c.unit, true)) + '</span></div>').join('') + '</div>';
    }

    function donutChart(c) {
        const vals = (c.series[0] || { values: [] }).values;
        const total = vals.reduce((a, b) => a + b, 0);
        if (!c.labels.length || total <= 0) return empty('Nothing to show yet');
        const R = 62, r = 40, cx = 80, cy = 80;
        let a0 = -Math.PI / 2, paths = '';
        vals.forEach((v, i) => {
            if (v <= 0) return;
            const a1 = a0 + (v / total) * Math.PI * 2 - (vals.filter(x => x > 0).length > 1 ? 0.02 : 0);
            const big = a1 - a0 > Math.PI ? 1 : 0;
            const p = (rad, a) => (cx + rad * Math.cos(a)).toFixed(2) + ' ' + (cy + rad * Math.sin(a)).toFixed(2);
            const d = (v / total) > 0.9999
                ? 'M' + (cx - R) + ' ' + cy + 'a' + R + ' ' + R + ' 0 1 0 ' + 2 * R + ' 0a' + R + ' ' + R + ' 0 1 0 ' + (-2 * R) + ' 0M' + (cx - r) + ' ' + cy + 'a' + r + ' ' + r + ' 0 1 1 ' + 2 * r + ' 0a' + r + ' ' + r + ' 0 1 1 ' + (-2 * r) + ' 0'
                : 'M' + p(R, a0) + 'A' + R + ' ' + R + ' 0 ' + big + ' 1 ' + p(R, a1) + 'L' + p(r, a1) + 'A' + r + ' ' + r + ' 0 ' + big + ' 0 ' + p(r, a0) + 'Z';
            paths += '<path d="' + d + '" fill="' + PALETTE[i % PALETTE.length] + '" fill-rule="evenodd"><title>' + esc(c.labels[i] + ': ' + fmt(v, c.unit)) + '</title></path>';
            a0 = a1 + 0.02;
        });
        const legend = c.labels.map((l, i) => '<li><i style="background:' + PALETTE[i % PALETTE.length] + '"></i><span>' + esc(l) + '</span><b>' + esc(fmt(vals[i], c.unit)) + '</b></li>').join('');
        return '<div class="pd-donut"><svg viewBox="0 0 160 160" class="pd-donut-svg" role="img" aria-label="' + esc(c.title) + '">' + paths +
            '<text x="80" y="76" text-anchor="middle" class="pd-donut-n">' + esc(whole.format(total)) + '</text><text x="80" y="94" text-anchor="middle" class="pd-axis">total</text></svg><ul class="pd-legend">' + legend + '</ul></div>';
    }

    function chartHtml(c) {
        switch (c.type) {
            case 'line': return lineChart(c);
            case 'bar': return barChart(c);
            case 'hbar': return hbarChart(c);
            case 'donut': return donutChart(c);
            default: return empty();
        }
    }

    function legendFor(c) {
        if ((c.type === 'line' || c.type === 'bar') && c.series.length > 1)
            return c.series.map((s, i) => '<span class="pd-key"><i style="background:' + PALETTE[i % PALETTE.length] + '"></i>' + esc(s.name) + '</span>').join('');
        return '';
    }

    const App = {
        setup() {
            const cfg = window.PERSONA || { tiles: [] };
            const state = Vue.reactive({
                greeting: 'Welcome', loading: true, error: '', kpis: [], charts: [], lists: [], updated: '', people: null
            });
            const today = new Date().toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long' });

            try {
                const h = new Date().getHours();
                const part = h < 12 ? 'Good morning' : (h < 17 ? 'Good afternoon' : 'Good evening');
                const email = StorageManager.getEmail() || '';
                const name = email ? email.split('@')[0].replace(/[._-]+/g, ' ').replace(/\b\w/g, c => c.toUpperCase()) : '';
                state.greeting = name ? part + ', ' + name : part;
            } catch (e) { }

            const methods = {
                load: async () => {
                    state.loading = true; state.error = '';
                    try {
                        const res = await AxiosManager.get('/Dashboard/GetRoleDashboard', {});
                        const c = res?.data?.content ?? {};
                        state.kpis = c.kpis ?? [];
                        state.charts = c.charts ?? [];
                        state.lists = c.lists ?? [];
                        state.updated = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                    } catch (e) {
                        console.error('dashboard load error:', e);
                        state.error = e?.response?.data?.message || 'The dashboard data could not be loaded.';
                    }
                    if (cfg.showUsers) {
                        try {
                            const ur = await AxiosManager.get('/Security/GetUserList', {});
                            const users = (ur?.data?.content?.data ?? []).filter(u => !u.isDeleted);
                            state.people = {
                                active: users.filter(u => !u.isBlocked).length,
                                blocked: users.filter(u => u.isBlocked).length,
                                unconfirmed: users.filter(u => !u.emailConfirmed).length
                            };
                        } catch (e) { console.error('people load error:', e); }
                    }
                    state.loading = false;
                },
                kpiValue: (k) => fmt(k.value, k.unit, true),
                spark: (k) => sparkline(k.spark, k.tone === 'bad' ? '#ef4444' : k.tone === 'warn' ? '#f59e0b' : '#0d9488'),
                chart: (c) => chartHtml(c),
                keys: (c) => legendFor(c)
            };

            Vue.onMounted(async () => {
                try { await SecurityManager.validateToken(); } catch (e) { console.error('token error:', e); }
                await methods.load();
            });

            return { state, cfg, methods, today };
        }
    };

    Vue.createApp(App).mount('#app');
})();
