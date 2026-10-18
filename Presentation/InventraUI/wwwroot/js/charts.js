/* WHMS charts: responsive SVG bar charts with grouped/stacked modes, legend toggles and a hover tooltip.
   Input: series [{ name, type: 'Column'|'Bar', dataSource: [{ x, y }] }] (the shape the dashboard API returns). */
(function () {
    var NAMED = { confirmed: '#0b7f72', draft: '#f5a524', cancelled: '#e5484d', archived: '#8da1b4' };
    var CAT = ['#0b7f72', '#f5a524', '#4f7cff', '#8b5cf6', '#e5484d', '#17a589', '#8da1b4'];
    var NS = 'http://www.w3.org/2000/svg';

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function fmt(v) { return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(v || 0); }
    function compact(v) { return v >= 1e6 ? (v / 1e6).toFixed(1).replace(/\.0$/, '') + 'M' : v >= 1e3 ? (v / 1e3).toFixed(1).replace(/\.0$/, '') + 'k' : String(Math.round(v)); }
    function nice(max, n) {
        if (max <= 0) return { max: 1, step: 1 };
        var raw = max / n, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
        var step = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p;
        return { max: Math.ceil(max / step) * step, step: step };
    }
    // Bar with rounded corners only on the data end. dir: 'up' (vertical) or 'right' (horizontal).
    function barPath(x, y, w, h, r, dir, round) {
        r = round ? Math.max(0, Math.min(r, w / 2, h / 2)) : 0;
        if (dir === 'up') {
            return 'M' + x + ' ' + (y + h) + 'V' + (y + r) + 'Q' + x + ' ' + y + ' ' + (x + r) + ' ' + y + 'H' + (x + w - r) + 'Q' + (x + w) + ' ' + y + ' ' + (x + w) + ' ' + (y + r) + 'V' + (y + h) + 'Z';
        }
        return 'M' + x + ' ' + y + 'H' + (x + w - r) + 'Q' + (x + w) + ' ' + y + ' ' + (x + w) + ' ' + (y + r) + 'V' + (y + h - r) + 'Q' + (x + w) + ' ' + (y + h) + ' ' + (x + w - r) + ' ' + (y + h) + 'H' + x + 'Z';
    }

    function render(el, series, opts) {
        if (!el) return;
        opts = opts || {};
        series = (series || []).filter(function (s) { return s && s.dataSource; });
        if (!series.length || !series[0].dataSource.length) {
            el.innerHTML = '<div class="wc-empty"><i class="fas fa-chart-bar"></i><div>No data yet</div></div>';
            return;
        }
        var horizontal = String(series[0].type || '').toLowerCase() === 'bar';
        var cats = series[0].dataSource.map(function (d) { return d.x; });
        series.forEach(function (s, i) {
            s._color = NAMED[String(s.name || '').toLowerCase()] || CAT[i % CAT.length];
            s._map = {}; s.dataSource.forEach(function (d) { s._map[d.x] = d.y; });
            cats.forEach(function (c) { if (!(c in s._map)) s._map[c] = 0; });
        });
        var st = { mode: opts.mode || 'grouped', hidden: {}, hover: -1 };

        el.classList.add('wc');
        el.innerHTML = '<div class="wc-top"><div class="wc-legend"></div><div class="wc-seg"><button type="button" data-m="grouped">Grouped</button><button type="button" data-m="stacked">Stacked</button></div></div>' +
            '<div class="wc-plot"><svg></svg><div class="wc-tip" hidden></div></div>';
        var legend = el.querySelector('.wc-legend'), plot = el.querySelector('.wc-plot'), svg = el.querySelector('svg'), tip = el.querySelector('.wc-tip');

        function drawLegend() {
            legend.innerHTML = series.map(function (s, i) {
                var total = cats.reduce(function (a, c) { return a + s._map[c]; }, 0);
                return '<button type="button" class="wc-chip' + (st.hidden[i] ? ' off' : '') + '" data-i="' + i + '"><i style="background:' + s._color + '"></i>' + esc(s.name) + '<b>' + fmt(total) + '</b></button>';
            }).join('');
            el.querySelectorAll('.wc-seg button').forEach(function (b) { b.classList.toggle('on', b.getAttribute('data-m') === st.mode); });
        }

        function draw() {
            var vis = series.filter(function (s, i) { return !st.hidden[i]; });
            var W = Math.max(280, plot.clientWidth || 600);
            var n = cats.length;
            var labelW = horizontal ? Math.min(140, Math.max.apply(null, cats.map(function (c) { return String(c).length * 6.6 + 12; }))) : 46;
            var M = horizontal ? { l: labelW, r: 28, t: 8, b: 30 } : { l: labelW, r: 8, t: 18, b: 30 };
            var H = horizontal ? Math.max(280, n * (st.mode === 'grouped' ? 28 * Math.max(vis.length, 1) + 18 : 46) + M.t + M.b) : 320;
            var pw = W - M.l - M.r, ph = H - M.t - M.b;
            var totals = cats.map(function (c) { return vis.reduce(function (a, s) { return a + s._map[c]; }, 0); });
            var maxV = st.mode === 'stacked' ? Math.max.apply(null, totals.concat([0])) : Math.max.apply(null, [0].concat(vis.map(function (s) { return Math.max.apply(null, cats.map(function (c) { return s._map[c]; })); })));
            var sc = nice(maxV * (st.mode === 'stacked' ? 1.08 : 1.04), horizontal ? 5 : 5);
            var ticks = []; for (var t = 0; t <= sc.max + 1e-9; t += sc.step) ticks.push(t);
            var band = (horizontal ? ph : pw) / n;
            var out = '';

            ticks.forEach(function (t) {
                if (horizontal) {
                    var gx = M.l + t / sc.max * pw;
                    out += '<line class="wc-grid" x1="' + gx + '" x2="' + gx + '" y1="' + M.t + '" y2="' + (M.t + ph) + '"/><text class="wc-ax" x="' + gx + '" y="' + (H - 10) + '" text-anchor="middle">' + compact(t) + '</text>';
                } else {
                    var gy = M.t + ph - t / sc.max * ph;
                    out += '<line class="wc-grid" x1="' + M.l + '" x2="' + (W - M.r) + '" y1="' + gy + '" y2="' + gy + '"/><text class="wc-ax" x="' + (M.l - 8) + '" y="' + (gy + 3.5) + '" text-anchor="end">' + compact(t) + '</text>';
                }
            });

            cats.forEach(function (c, ci) {
                var label = String(c); var max = horizontal ? Math.floor(labelW / 6.6) : Math.max(4, Math.floor(band / 6.4));
                if (label.length > max + 1) label = label.slice(0, max) + '…';
                if (horizontal) out += '<text class="wc-ax wc-cat" x="' + (M.l - 8) + '" y="' + (M.t + ci * band + band / 2 + 4) + '" text-anchor="end">' + esc(label) + '</text>';
                else out += '<text class="wc-ax wc-cat" x="' + (M.l + ci * band + band / 2) + '" y="' + (H - 10) + '" text-anchor="middle">' + esc(label) + '</text>';
                out += '<rect class="wc-hl' + (st.hover === ci ? ' on' : '') + '" data-ci="' + ci + '" ' + (horizontal ? 'x="' + M.l + '" y="' + (M.t + ci * band) + '" width="' + pw + '" height="' + band : 'x="' + (M.l + ci * band) + '" y="' + M.t + '" width="' + band + '" height="' + ph) + '" rx="8"/>';
            });

            cats.forEach(function (c, ci) {
                var gw = Math.min(band * 0.72, st.mode === 'stacked' ? 46 : 80);
                var start = (horizontal ? M.t : M.l) + ci * band + (band - gw) / 2;
                if (st.mode === 'grouped') {
                    var k = Math.max(vis.length, 1), gap = 3, bw = Math.min(26, (gw - gap * (k - 1)) / k);
                    var used = bw * k + gap * (k - 1), off = start + (gw - used) / 2;
                    vis.forEach(function (s, si) {
                        var v = s._map[c]; if (v <= 0) return;
                        if (horizontal) { var w = v / sc.max * pw; out += '<path class="wc-bar h" d="' + barPath(M.l, off + si * (bw + gap), w, bw, 4, 'right', true) + '" fill="' + s._color + '"/>'; }
                        else { var h = v / sc.max * ph; out += '<path class="wc-bar v" d="' + barPath(off + si * (bw + gap), M.t + ph - h, bw, h, 4, 'up', true) + '" fill="' + s._color + '"/>'; }
                    });
                } else {
                    var acc = 0, lastIdx = -1;
                    vis.forEach(function (s, si) { if (s._map[c] > 0) lastIdx = si; });
                    vis.forEach(function (s, si) {
                        var v = s._map[c]; if (v <= 0) return;
                        if (horizontal) { var x0 = M.l + acc / sc.max * pw, w2 = v / sc.max * pw; out += '<path class="wc-bar h seg" d="' + barPath(x0, start, w2, gw, 5, 'right', si === lastIdx) + '" fill="' + s._color + '"/>'; }
                        else { var h2 = v / sc.max * ph, y0 = M.t + ph - acc / sc.max * ph - h2; out += '<path class="wc-bar v seg" d="' + barPath(start, y0, gw, h2, 5, 'up', si === lastIdx) + '" fill="' + s._color + '"/>'; }
                        acc += v;
                    });
                    if (totals[ci] > 0) {
                        if (horizontal) out += '<text class="wc-total" x="' + (M.l + totals[ci] / sc.max * pw + 6) + '" y="' + (start + gw / 2 + 4) + '">' + fmt(totals[ci]) + '</text>';
                        else out += '<text class="wc-total" x="' + (start + gw / 2) + '" y="' + (M.t + ph - totals[ci] / sc.max * ph - 6) + '" text-anchor="middle">' + fmt(totals[ci]) + '</text>';
                    }
                }
            });

            if (opts.unit) out += '<text class="wc-ax wc-unit" x="' + (horizontal ? M.l - 8 : 4) + '" y="' + (horizontal ? H - 10 : 10) + '" text-anchor="' + (horizontal ? 'end' : 'start') + '">' + esc(opts.unit) + '</text>';
            svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
            svg.setAttribute('width', W); svg.setAttribute('height', H);
            svg.innerHTML = out;
            svg._geo = { M: M, band: band, horizontal: horizontal, W: W, H: H, vis: vis, totals: totals };
        }

        function showTip(ci, evt) {
            var g = svg._geo; if (!g) return;
            var c = cats[ci];
            var rows = g.vis.map(function (s) { return '<div><i style="background:' + s._color + '"></i>' + esc(s.name) + '<b>' + fmt(s._map[c]) + '</b></div>'; }).join('');
            tip.innerHTML = '<div class="wc-tt">' + esc(c) + '</div>' + rows + (g.vis.length > 1 ? '<div class="wc-sum">Total<b>' + fmt(g.totals[ci]) + '</b></div>' : '');
            tip.hidden = false;
            var pr = plot.getBoundingClientRect();
            var x = evt.clientX - pr.left + 14, y = evt.clientY - pr.top + 12;
            if (x + tip.offsetWidth > pr.width) x = evt.clientX - pr.left - tip.offsetWidth - 14;
            if (y + tip.offsetHeight > pr.height) y = Math.max(0, pr.height - tip.offsetHeight - 4);
            tip.style.left = Math.max(0, x) + 'px'; tip.style.top = y + 'px';
        }

        legend.addEventListener('click', function (e) {
            var b = e.target.closest('.wc-chip'); if (!b) return;
            var i = +b.getAttribute('data-i');
            var visibleCount = series.length - Object.keys(st.hidden).filter(function (k) { return st.hidden[k]; }).length;
            if (st.hidden[i]) st.hidden[i] = false; else if (visibleCount > 1) st.hidden[i] = true;
            drawLegend(); draw();
        });
        el.querySelector('.wc-seg').addEventListener('click', function (e) {
            var b = e.target.closest('button'); if (!b) return;
            st.mode = b.getAttribute('data-m'); drawLegend(); draw();
        });
        svg.addEventListener('mousemove', function (e) {
            var r = e.target.closest && e.target.closest('[data-ci]');
            var g = svg._geo; if (!g) return;
            var box = svg.getBoundingClientRect();
            var pos = g.horizontal ? e.clientY - box.top - g.M.t : e.clientX - box.left - g.M.l;
            var ci = Math.floor(pos / g.band);
            if (ci < 0 || ci >= cats.length) { tip.hidden = true; return; }
            if (ci !== st.hover) {
                st.hover = ci;
                svg.querySelectorAll('.wc-hl').forEach(function (h) { h.classList.toggle('on', +h.getAttribute('data-ci') === ci); });
            }
            showTip(ci, e);
        });
        svg.addEventListener('mouseleave', function () {
            st.hover = -1; tip.hidden = true;
            svg.querySelectorAll('.wc-hl.on').forEach(function (h) { h.classList.remove('on'); });
        });

        drawLegend(); draw();
        if (window.ResizeObserver) {
            var lastW = plot.clientWidth;
            new ResizeObserver(function () { if (Math.abs(plot.clientWidth - lastW) > 4) { lastW = plot.clientWidth; draw(); } }).observe(plot);
        }
    }

    window.WhmsCharts = { render: render };
})();
