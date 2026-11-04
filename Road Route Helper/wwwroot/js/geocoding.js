// Address search. Nominatim's usage policy allows at most one request per second and no autocomplete,
// so a search only runs when the form is submitted.
(function () {
    const MIN_INTERVAL_MS = 1000;
    const state = window._tripCreate;
    const form = document.getElementById('addressSearch');
    const button = form.querySelector('button[type=submit]');
    const results = document.getElementById('addressResults');
    const status = document.getElementById('addressStatus');

    function buildResult(result) {
        const item = document.createElement('button');
        item.type = 'button';
        item.className = 'list-group-item list-group-item-action small';
        item.textContent = result.display_name;
        item.addEventListener('click', () => {
            const latlng = L.latLng(Number(result.lat), Number(result.lon));
            state.map.setView(latlng, 14, { animate: false });
            state.openStopForm(latlng, null, result.display_name.split(',')[0]);
        });
        return item;
    }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        const query = document.getElementById('addressQuery').value.trim();
        if (!query || button.disabled) return;

        button.disabled = true;
        const startedAt = Date.now();
        status.textContent = '';
        results.replaceChildren();
        try {
            const response = await fetch(`${state.geocodingServiceUrl}?format=jsonv2&limit=5&q=${encodeURIComponent(query)}`);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const found = await response.json();
            results.replaceChildren(...found.map(buildResult));
            if (found.length === 0) status.textContent = 'Nenhum endereço encontrado.';
        } catch (err) {
            console.error(err);
            status.textContent = 'Não foi possível buscar o endereço. Tente novamente.';
        }
        setTimeout(() => { button.disabled = false; }, Math.max(0, MIN_INTERVAL_MS - (Date.now() - startedAt)));
    });
})();
