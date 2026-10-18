// Summary strip shown above report grids: headline numbers that follow the grid's filter and search.
// Usage: ReportSummary.attach(document.getElementById('reportKpis'), grid, rows, [{label, note, tone, calc: rows => value, kind: 'money'|'int'|'signed'}])
window.ReportSummary = (function () {
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }
    function fmt(v, kind) {
        if (v == null || isNaN(v)) return '0';
        if (kind === 'signed') return (v > 0 ? '+' : '') + Number(v).toLocaleString(undefined, { maximumFractionDigits: 2 });
        if (kind === 'int') return Math.round(v).toLocaleString();
        return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function distinct(rows, f) { const s = new Set(); rows.forEach(r => { if (r[f] != null && r[f] !== '') s.add(r[f]); }); return s.size; }
    function sum(rows, f) { return rows.reduce((a, r) => a + (Number(r[f]) || 0), 0); }
    function attach(el, grid, rows, defs) {
        if (!el) return;
        function current() {
            try { if (grid && grid.getFilteredRecords) { const f = grid.getFilteredRecords(); if (f) return f; } } catch (e) { }
            return rows || [];
        }
        function draw() {
            const data = current();
            const filtered = data.length !== (rows || []).length;
            el.innerHTML = defs.map(d => {
                const v = d.calc(data);
                const tone = d.tone ? d.tone(v) : '';
                return '<div class="rk-kpi ' + (tone ? 'tone-' + esc(tone) : '') + '"><span class="rk-label">' + esc(d.label) + '</span><span class="rk-value">' + esc(fmt(v, d.kind)) + '</span><span class="rk-note">' + esc(filtered ? 'Matching your filter' : (d.note || '')) + '</span></div>';
            }).join('');
        }
        draw();
        if (grid && grid.addEventListener) {
            grid.addEventListener('actionComplete', a => { if (a && (a.requestType === 'filtering' || a.requestType === 'searching' || a.requestType === 'refresh' || a.requestType === 'sorting' || a.requestType === 'clearFilter')) draw(); });
            grid.addEventListener('dataBound', draw);
        }
        return draw;
    }
    return { attach, distinct, sum };
})();
