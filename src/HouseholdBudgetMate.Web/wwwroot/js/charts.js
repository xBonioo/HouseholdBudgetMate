window.HBM = window.HBM || {};
window.HBM.charts = (function () {
    'use strict';

    // chartId -> { chart: Chart, type: string }
    const _instances = {};

    const _palette = [
        '#4F46E5', // Indigo  (Primary)
        '#0EA5E9', // Sky     (Secondary)
        '#10B981', // Emerald (Success)
        '#F59E0B', // Amber   (Warning)
        '#EF4444', // Red     (Error)
        '#8B5CF6', // Violet
        '#EC4899', // Pink
        '#14B8A6', // Teal
        '#F97316', // Orange
        '#6366F1', // Indigo-light
    ];

    function _theme(isDark) {
        return isDark
            ? { text: '#E2E8F0', grid: 'rgba(255,255,255,0.08)', tip: '#1E293B' }
            : { text: '#0F172A', grid: 'rgba(0,0,0,0.08)',       tip: '#FFFFFF' };
    }

    function _buildDatasets(rawDs, chartType, isDark) {
        const isPie = chartType === 'pie' || chartType === 'doughnut';
        return rawDs.map(function (ds, i) {
            const fill   = ds.backgroundColor || _palette[i % _palette.length];
            const border = ds.borderColor     || fill;
            const dsType = ds.type            || chartType;

            if (isPie) {
                var sliceColors;
                if (ds.backgroundColors && ds.backgroundColors.length > 0) {
                    sliceColors = ds.backgroundColors;
                } else if (rawDs.length === 1) {
                    sliceColors = ds.data.map(function (_, j) { return _palette[j % _palette.length]; });
                } else {
                    sliceColors = fill;
                }
                return {
                    label: ds.label,
                    data: ds.data,
                    backgroundColor: sliceColors,
                    borderColor: isDark ? '#1E293B' : '#FFFFFF',
                    borderWidth: 2
                };
            }
            if (dsType === 'line') {
                return {
                    type: 'line',
                    label: ds.label,
                    data: ds.data,
                    borderColor: border,
                    backgroundColor: border + '33',
                    borderWidth: 2,
                    fill: false,
                    tension: 0.35,
                    pointRadius: 3,
                    pointHoverRadius: 5
                };
            }
            return {
                type: 'bar',
                label: ds.label,
                data: ds.data,
                backgroundColor: fill + 'CC',
                borderColor: fill,
                borderWidth: 1,
                borderRadius: 4
            };
        });
    }

    function _buildOptions(chartType, theme) {
        const isPie = chartType === 'pie' || chartType === 'doughnut';
        const opts = {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: isPie ? 'right' : 'bottom',
                    labels: {
                        color: theme.text,
                        boxWidth: 12,
                        padding: 12,
                        font: { size: 11 }
                    }
                },
                tooltip: {
                    backgroundColor: theme.tip,
                    titleColor: theme.text,
                    bodyColor: theme.text,
                    borderColor: theme.grid,
                    borderWidth: 1,
                    callbacks: {
                        label: function (ctx) {
                            var val = ctx.parsed.y !== undefined ? ctx.parsed.y : ctx.parsed;
                            if (isPie) {
                                var total = ctx.dataset.data.reduce(function (sum, item, index) {
                                    return ctx.chart.getDataVisibility(index)
                                        ? sum + Number(item || 0)
                                        : sum;
                                }, 0);
                                var percent = total > 0 ? (Number(val || 0) / total) * 100 : 0;
                                return ' ' + ctx.label + ': ' + val.toFixed(2) + ' zl (' + percent.toFixed(1) + '%)';
                            }
                            return ' ' + ctx.dataset.label + ': ' + val.toFixed(2) + ' zł';
                        }
                    }
                }
            }
        };

        if (!isPie) {
            opts.scales = {
                x: {
                    ticks: { color: theme.text, font: { size: 11 } },
                    grid:  { color: theme.grid }
                },
                y: {
                    ticks: {
                        color: theme.text,
                        font: { size: 11 },
                        callback: function (val) { return val.toFixed(0) + ' zł'; }
                    },
                    grid:        { color: theme.grid },
                    beginAtZero: true
                }
            };
        }
        return opts;
    }

    function create(canvasEl, chartId, chartType, labels, rawDs, isDark) {
        if (_instances[chartId]) {
            _instances[chartId].chart.destroy();
        }
        const theme    = _theme(isDark);
        const isMixed  = chartType === 'mixed';
        const baseType = isMixed ? 'bar' : chartType;

        const chart = new Chart(canvasEl, {
            type: baseType,
            data: {
                labels:   labels,
                datasets: _buildDatasets(rawDs, chartType, isDark)
            },
            options: _buildOptions(chartType, theme)
        });
        _instances[chartId] = { chart: chart, type: chartType };
    }

    function update(canvasEl, chartId, labels, rawDs, isDark) {
        const entry = _instances[chartId];
        if (!entry) {
            // Instance lost (e.g. after reconnect) — nothing we can do; next create() will fix it.
            return;
        }
        const theme = _theme(isDark);
        const chart = entry.chart;

        chart.data.labels   = labels;
        chart.data.datasets = _buildDatasets(rawDs, entry.type, isDark);

        chart.options.plugins.legend.labels.color       = theme.text;
        chart.options.plugins.tooltip.backgroundColor   = theme.tip;
        chart.options.plugins.tooltip.titleColor        = theme.text;
        chart.options.plugins.tooltip.bodyColor         = theme.text;
        if (chart.options.scales) {
            if (chart.options.scales.x) chart.options.scales.x.ticks.color = theme.text;
            if (chart.options.scales.y) chart.options.scales.y.ticks.color = theme.text;
        }
        chart.update('none');
    }

    function destroy(chartId) {
        const entry = _instances[chartId];
        if (entry) {
            entry.chart.destroy();
            delete _instances[chartId];
        }
    }

    return { create: create, update: update, destroy: destroy };
})();
