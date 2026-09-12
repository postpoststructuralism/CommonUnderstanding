document.querySelectorAll('[data-chart]').forEach((element) => {
    const points = JSON.parse(element.dataset.chart || '[]');
    if (!points.length) {
        element.innerHTML = '<div class="empty-chart">NO DATA IN RANGE</div>';
        return;
    }

    const width = 800;
    const height = 190;
    const maximum = Math.max(...points.map((point) => point.value), 1);
    const coordinates = points.map((point, index) => {
        const x = points.length === 1 ? width / 2 : index * width / (points.length - 1);
        const y = height - (point.value / maximum * (height - 24)) - 12;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
    }).join(' ');

    element.innerHTML = `<svg viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" role="img" aria-label="Request volume chart">
        <defs><linearGradient id="chart-fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#18c59a" stop-opacity=".32"/><stop offset="1" stop-color="#18c59a" stop-opacity="0"/></linearGradient></defs>
        <polygon points="0,${height} ${coordinates} ${width},${height}" fill="url(#chart-fill)"/>
        <polyline points="${coordinates}" fill="none" stroke="#18c59a" stroke-width="3" vector-effect="non-scaling-stroke"/>
    </svg>`;
});