// Mobile : fermer le menu latéral en cliquant en dehors.
document.addEventListener('click', function (e) {
    const sb = document.getElementById('sidebar');
    if (sb && sb.classList.contains('open') && !sb.contains(e.target) && !e.target.closest('[onclick*="sidebar"]')) {
        sb.classList.remove('open');
    }
});

// Helpers graphiques (Chart.js) — style sobre : une série, barres fines arrondies, grille discrète.
(function () {
    const SERIES = '#2a78d6';
    const MUTED = '#898781';
    const GRID = '#e1e0d9';
    const fmt = (v, unit) => unit === 'DH'
        ? new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v) + ' DH'
        : new Intl.NumberFormat('fr-FR').format(v) + (unit ? ' ' + unit : '');

    window.appBarChart = function (canvasId, labels, values, options) {
        const el = document.getElementById(canvasId);
        if (!el || !window.Chart) return;
        const o = Object.assign({ horizontal: false, unit: 'DH', label: '' }, options || {});
        const valueAxis = {
            beginAtZero: true,
            grid: { color: GRID, drawTicks: false },
            border: { display: false },
            ticks: { color: MUTED, padding: 6, callback: v => new Intl.NumberFormat('fr-FR', { notation: 'compact' }).format(v) },
        };
        const catAxis = { grid: { display: false }, border: { color: '#c3c2b7' }, ticks: { color: MUTED, autoSkip: true, maxRotation: 0 } };

        return new Chart(el, {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: o.label, data: values, backgroundColor: SERIES, hoverBackgroundColor: '#1c5cab',
                    borderRadius: 4, borderSkipped: 'start', maxBarThickness: o.horizontal ? 18 : 28,
                    categoryPercentage: 0.8, barPercentage: 0.9,
                }],
            },
            options: {
                indexAxis: o.horizontal ? 'y' : 'x',
                responsive: true, maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#0b0b0b', padding: 10, displayColors: false,
                        callbacks: { label: ctx => (o.label ? o.label + ' : ' : '') + fmt(ctx.parsed[o.horizontal ? 'x' : 'y'], o.unit) },
                    },
                },
                scales: o.horizontal ? { x: valueAxis, y: catAxis } : { x: catAxis, y: valueAxis },
            },
        });
    };
})();
