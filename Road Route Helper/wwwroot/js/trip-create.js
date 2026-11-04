// Map, stops and saving for the trip create and edit pages.
(function () {
    const state = window._tripCreate = window._tripCreate || {};

    const map = L.map('map').setView([34.0522, -118.2437], 8);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    const initialData = document.getElementById('trip-initial-data');
    state.map = map;
    state.trip = initialData ? JSON.parse(initialData.textContent) : {};
    state.stops = (state.trip.stops || []).map(s => ({ ...s }));
    state.markers = [];
    state.routingControl = null;
    state.routeWaiters = [];
    state.savedRouteLine = null;
    state.route = state.trip.id ? {
        distanceKm: state.trip.totalDistanceKm || 0,
        duration: state.trip.estimatedDuration || null,
        geometry: state.trip.routeGeometry || []
    } : null;

    function addMarker(stop) {
        const m = L.marker([stop.latitude, stop.longitude]).addTo(map).bindPopup(textPopup(stop.name));
        state.markers.push({ id: stop.id, marker: m });
    }

    function renderStops() {
        const items = state.stops.map((stop, idx) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex justify-content-between align-items-center';
            li.dataset.id = stop.id;
            const label = document.createElement('div');
            label.textContent = `${idx + 1}. ${stop.name}`;
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn btn-sm btn-danger';
            remove.textContent = 'Del';
            remove.addEventListener('click', () => removeStop(stop.id));
            li.append(label, remove);
            return li;
        });
        document.getElementById('stopsList').replaceChildren(...items);
    }

    function reorderStops() {
        const byId = new Map(state.stops.map(s => [s.id, s]));
        const items = Array.from(document.getElementById('stopsList').children);
        state.stops = items.map((li, idx) => ({ ...byId.get(li.dataset.id), order: idx }));
        stopsChanged();
    }

    function removeStop(id) {
        state.stops = state.stops.filter(s => s.id !== id);
        const m = state.markers.find(x => x.id === id);
        if (m) { map.removeLayer(m.marker); state.markers = state.markers.filter(x => x.id !== id); }
        stopsChanged();
    }

    // TimeSpan format accepted by the API: hh:mm:ss, with a day part from 24h on.
    function formatDuration(totalSeconds) {
        const pad = n => String(n).padStart(2, '0');
        const days = Math.floor(totalSeconds / 86400);
        const h = Math.floor((totalSeconds % 86400) / 3600);
        const m = Math.floor((totalSeconds % 3600) / 60);
        const s = Math.floor(totalSeconds % 60);
        return `${days > 0 ? days + '.' : ''}${pad(h)}:${pad(m)}:${pad(s)}`;
    }

    function setRouteStatus(message) {
        const status = document.getElementById('routeStatus');
        status.textContent = message;
        status.classList.toggle('d-none', !message);
    }

    function clearSavedRouteLine() {
        if (state.savedRouteLine) map.removeLayer(state.savedRouteLine);
        state.savedRouteLine = null;
    }

    function finishRouting(found) {
        const waiting = state.routeWaiters;
        state.routeWaiters = [];
        waiting.forEach(resolve => resolve(found));
    }

    function updateRouting() {
        if (state.routingControl) { map.removeControl(state.routingControl); state.routingControl = null; }
        setRouteStatus('');
        if (state.stops.length < 2) {
            finishRouting(false);
            return;
        }
        const control = L.Routing.control({
            waypoints: state.stops.map(s => L.latLng(s.latitude, s.longitude)),
            router: L.Routing.osrmv1({ serviceUrl: state.routingServiceUrl }),
            lineOptions: { styles: [{ color: 'crimson', weight: 6 }] },
            showAlternatives: false,
            fitSelectedRoute: true,
            createMarker: (i, wp) => L.marker(wp.latLng).bindPopup(textPopup(state.stops[i].name))
        }).addTo(map);
        state.routingControl = control;

        // A replaced control may still answer: only the current one counts.
        control.on('routesfound', e => {
            if (control !== state.routingControl) return;
            const route = e.routes[0];
            state.route = {
                distanceKm: parseFloat((route.summary.totalDistance / 1000).toFixed(2)),
                duration: formatDuration(route.summary.totalTime),
                geometry: route.coordinates.map(p => ({ latitude: p.lat, longitude: p.lng }))
            };
            finishRouting(true);
        });
        control.on('routingerror', () => {
            if (control !== state.routingControl) return;
            setRouteStatus('Não foi possível calcular a rota. A viagem pode ser salva sem o traçado.');
            finishRouting(false);
        });
    }

    function stopsChanged() {
        state.route = null;
        clearSavedRouteLine();
        renderStops();
        updateRouting();
    }

    function addStopFromInputs() {
        const latlng = map.getCenter();
        state.stops.push({
            id: crypto.randomUUID(),
            name: document.getElementById('stopName').value || 'Stop',
            category: 8,
            notes: '',
            latitude: latlng.lat,
            longitude: latlng.lng,
            order: state.stops.length
        });
        stopsChanged();
    }

    async function calculateRoute() {
        const btn = document.getElementById('calcRouteBtn');
        const originalText = btn.innerText;
        btn.disabled = true;
        btn.innerText = 'Calculando...';
        const finished = new Promise(resolve => state.routeWaiters.push(resolve));
        clearSavedRouteLine();
        updateRouting();
        const found = await finished;
        btn.disabled = false;
        btn.innerText = originalText;
        return found;
    }

    function showSaveError(messages) {
        const box = document.getElementById('saveError');
        box.textContent = messages.join(' ');
        box.classList.toggle('d-none', messages.length === 0);
    }

    async function readErrors(response) {
        try {
            const problem = await response.json();
            if (problem.errors) return Object.values(problem.errors).flat();
        } catch (err) {
            console.error(err);
        }
        return ['Não foi possível salvar a viagem.'];
    }

    async function saveTrip() {
        const value = id => document.getElementById(id).value;
        const route = state.route;
        const dto = {
            ...state.trip,
            name: value('name'),
            description: value('description'),
            startDate: value('startDate') || null,
            endDate: value('endDate') || null,
            stops: state.stops.map((s, i) => ({ ...s, order: i })),
            totalDistanceKm: route ? route.distanceKm : 0,
            estimatedDuration: route ? route.duration : null,
            routeGeometry: route ? route.geometry : []
        };
        const url = state.trip.id ? `/api/trips/${state.trip.id}` : '/api/trips';
        const method = state.trip.id ? 'PUT' : 'POST';

        showSaveError([]);
        try {
            const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto) });
            if (res.ok) {
                window.location.href = '/';
            } else {
                showSaveError(await readErrors(res));
            }
        } catch (err) {
            console.error(err);
            showSaveError(['Não foi possível salvar a viagem.']);
        }
    }

    state.calculateRoute = calculateRoute;
    document.getElementById('addStopBtn').addEventListener('click', addStopFromInputs);
    document.getElementById('calcRouteBtn').addEventListener('click', calculateRoute);
    document.getElementById('saveTripBtn').addEventListener('click', saveTrip);

    Sortable.create(document.getElementById('stopsList'), { onEnd: reorderStops });
    state.stops.forEach(addMarker);
    renderStops();
    if (state.route && state.route.geometry.length > 0) {
        state.savedRouteLine = L.polyline(state.route.geometry.map(p => [p.latitude, p.longitude]), { color: 'crimson', weight: 6 }).addTo(map);
        map.fitBounds(state.savedRouteLine.getBounds(), { padding: [40, 40] });
    } else if (state.stops.length >= 2) {
        updateRouting();
    }
})();
