window.scrollToId = function (id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

window.ballersCharts = {

    renderWinLossChart: function (canvasId, wins, draws, losses) {

        const ctx = document.getElementById(canvasId);

        if (!ctx) return;

        if (ctx.chart)
            ctx.chart.destroy();

        const data = [wins, draws, losses];

        ctx.chart = new Chart(ctx, {
            type: 'doughnut',

            data: {
                labels: ['Wins', 'Draws', 'Losses'],
                datasets: [{
                    data: data,
                    backgroundColor: [
                        '#16A34A',
                        '#FACC15',
                        '#EF4444'
                    ],
                    borderWidth: 0
                }]
            },

            options: {
                responsive: true,
                maintainAspectRatio: false,

                cutout: '70%',

                animation: {
                    animateRotate: true,
                    duration: 1200,
                    easing: 'easeOutQuart'
                },

                plugins: {
                    legend: {
                        display: false
                    },

                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.label}: ${context.raw}`;
                            }
                        }
                    }
                }
            },

            plugins: [{
                id: 'valueLabels',
                afterDraw(chart) {
                    const { ctx } = chart;
                    ctx.save();

                    chart.getDatasetMeta(0).data.forEach((arc, i) => {
                        const value = data[i];
                        if (!value) return;

                        const pos = arc.tooltipPosition();

                        ctx.fillStyle = "#0F172A";
                        ctx.font = "600 14px Inter";
                        ctx.textAlign = "center";
                        ctx.textBaseline = "middle";

                        ctx.fillText(value, pos.x, pos.y);
                    });

                    ctx.restore();
                }
            }]
        });
    }
};