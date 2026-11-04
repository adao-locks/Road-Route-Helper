// Map, stops and saving for the trip create and edit pages.
(function () {
    const CUSTOM_CATEGORY = 8;
    const ROUTING_DEBOUNCE_MS = 500;

    const state = window._tripCreate = window._tripCreate || {};

    const map = L.map('map').setView([34.0522, -118.2437], 8);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    const initialData = document.getElementById('trip-initial-data');
    state.map = map;
    state.trip = initialData ? JSON.parse(initialData.textContent) : {};
    state.stops = (state.trip.stops || []).map(s => ({ ...s }));
    state.markers = new Map();
    state.routingControl = null;
    state.routingTimer = null;
    state.routingInFlight = false;
    state.routeWaiters = [];
    state.savedRouteLine = null;
    state.route = state.trip.id ? {
        distanceKm: state.trip.totalDistanceKm || 0,
        duration: state.trip.estimatedDuration || null,
        geometry: state.trip.routeGeometry || []
    } : null;

    function renderMarkers() {
        state.markers.forEach(marker => map.removeLayer(marker));
        state.markers.clear();
        state.stops.forEach(stop => {
            const marker = L.marker([stop.latitude, stop.longitude], { draggable: true, icon: categoryIcon(stop.category) })
                .addTo(map)
                .bindPopup(textPopup(stop.name));
            marker.on('dragend', () => {
                const position = marker.getLatLng();
                stop.latitude = position.lat;
                stop.longitude = position.lng;
                stopsChanged();
            });
            state.markers.set(stop.id, marker);
        });
    }

    function renderStops() {
        const categories = stopCategories();
        const items = state.stops.map((stop, idx) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex justify-content-between align-items-center gap-2';
            li.dataset.id = stop.id;

            const label = document.createElement('div');
            label.textContent = `${idx + 1}. ${stop.name}`;
            const category = document.createElement('small');
            category.className = 'd-block text-muted';
            category.textContent = categories.find(c => c.category === stop.category).label;
            label.append(category);

            const actions = document.createElement('div');
            actions.className = 'd-flex gap-1';
            const edit = document.createElement('button');
            edit.type = 'button';
            edit.className = 'btn btn-sm btn-outline-secondary';
            edit.dataset.action = 'edit';
            edit.textContent = 'Editar';
            edit.addEventListener('click', () => openStopForm(L.latLng(stop.latitude, stop.longitude), stop));
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn btn-sm btn-danger';
            remove.textContent = 'Del';
            remove.addEventListener('click', () => removeStop(stop.id));
            actions.append(edit, remove);

            li.append(label, actions);
            return li;
        });
        document.getElementById('stopsList').replaceChildren(...items);
    }

    function buildField(labelText, control) {
        const wrapper = document.createElement('div');
        wrapper.className = 'mb-2';
        const label = document.createElement('label');
        label.className = 'form-label mb-0';
        label.htmlFor = control.id;
        label.textContent = labelText;
        wrapper.append(label, control);
        return wrapper;
    }

    function openStopForm(latlng, stop) {
        const name = document.createElement('input');
        name.id = 'stopFormName';
        name.className = 'form-control form-control-sm';
        name.value = stop ? stop.name : '';

        const category = document.createElement('select');
        category.id = 'stopFormCategory';
        category.className = 'form-select form-select-sm';
        stopCategories().forEach(c => {
            const option = document.createElement('option');
            option.value = c.category;
            option.textContent = c.label;
            category.append(option);
        });
        category.value = stop ? stop.category : CUSTOM_CATEGORY;

        const notes = document.createElement('textarea');
        notes.id = 'stopFormNotes';
        notes.className = 'form-control form-control-sm';
        notes.value = stop ? stop.notes : '';

        const error = document.createElement('div');
        error.id = 'stopFormError';
        error.className = 'text-danger small mb-2';
        error.setAttribute('role', 'alert');

        const save = document.createElement('button');
        save.id = 'stopFormSave';
        save.type = 'submit';
        save.className = 'btn btn-sm btn-primary';
        save.textContent = 'Salvar parada';

        const form = document.createElement('form');
        form.append(buildField('Nome', name), buildField('Categoria', category), buildField('Notas', notes), error, save);
        form.addEventListener('submit', event => {
            event.preventDefault();
            const values = { name: name.value.trim(), category: Number(category.value), notes: notes.value };
            if (!values.name) {
                error.textContent = 'Informe o nome da parada.';
                return;
            }
            map.closePopup();
            if (stop) {
                Object.assign(stop, values);
                renderStops();
                renderMarkers();
            } else {
                state.stops.push({
                    ...values,
                    id: crypto.randomUUID(),
                    latitude: latlng.lat,
                    longitude: latlng.lng,
                    order: state.stops.length
                });
                stopsChanged();
            }
        });

        L.popup({ minWidth: 240 }).setLatLng(latlng).setContent(form).openOn(map);
        name.focus();
    }
    state.openStopForm = openStopForm;

    function reorderStops() {
        const byId = new Map(state.stops.map(s => [s.id, s]));
        const items = Array.from(document.getElementById('stopsList').children);
        state.stops = items.map((li, idx) => Object.assign(byId.get(li.dataset.id), { order: idx }));
        stopsChanged();
    }

    function removeStop(id) {
        state.stops = state.stops.filter(s => s.id !== id);
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
        state.routingInFlight = false;
        const waiting = state.routeWaiters;
        state.routeWaiters = [];
        waiting.forEach(resolve => resolve(found));
    }

    function updateRouting() {
        clearTimeout(state.routingTimer);
        state.routingTimer = null;
        if (state.routingControl) { map.removeControl(state.routingControl); state.routingControl = null; }
        setRouteStatus('');
        if (state.stops.length < 2) {
            finishRouting(false);
            return;
        }
        state.routingInFlight = true;
        const control = L.Routing.control({
            waypoints: state.stops.map(s => L.latLng(s.latitude, s.longitude)),
            router: L.Routing.osrmv1({ serviceUrl: state.routingServiceUrl }),
            lineOptions: { styles: [{ color: 'crimson', weight: 6 }], addWaypoints: false },
            showAlternatives: false,
            fitSelectedRoute: true,
            createMarker: () => null
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

    function scheduleRouting() {
        clearTimeout(state.routingTimer);
        state.routingTimer = setTimeout(updateRouting, ROUTING_DEBOUNCE_MS);
    }

    function stopsChanged() {
        state.route = null;
        clearSavedRouteLine();
        setRouteStatus('');
        renderStops();
        renderMarkers();
        scheduleRouting();
    }

    function waitForRouting() {
        return new Promise(resolve => state.routeWaiters.push(resolve));
    }

    async function calculateRoute() {
        const btn = document.getElementById('calcRouteBtn');
        const originalText = btn.innerText;
        btn.disabled = true;
        btn.innerText = 'Calculando...';
        const finished = waitForRouting();
        clearSavedRouteLine();
        updateRouting();
        const found = await finished;
        if (state.stops.length < 2) setRouteStatus('Adicione pelo menos duas paradas para calcular a rota.');
        btn.disabled = false;
        btn.innerText = originalText;
        return found;
    }
    state.calculateRoute = calculateRoute;

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
        const saveButton = document.getElementById('saveTripBtn');
        saveButton.disabled = true;
        showSaveError([]);
        try {
            // A route still being calculated belongs to this save.
            if (state.routingTimer !== null || state.routingInFlight) await waitForRouting();

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
            const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto) });
            if (res.ok) {
                window.location.href = '/';
                return;
            }
            showSaveError(await readErrors(res));
        } catch (err) {
            console.error(err);
            showSaveError(['Não foi possível salvar a viagem.']);
        }
        saveButton.disabled = false;
    }

    map.on('click', event => openStopForm(event.latlng));
    document.getElementById('calcRouteBtn').addEventListener('click', calculateRoute);
    document.getElementById('saveTripBtn').addEventListener('click', saveTrip);
    Sortable.create(document.getElementById('stopsList'), { onEnd: reorderStops });

    renderMarkers();
    renderStops();
    if (state.route && state.route.geometry.length > 0) {
        state.savedRouteLine = L.polyline(state.route.geometry.map(p => [p.latitude, p.longitude]), { color: 'crimson', weight: 6 }).addTo(map);
        map.fitBounds(state.savedRouteLine.getBounds(), { padding: [40, 40] });
    } else if (state.stops.length >= 2) {
        updateRouting();
    } else if (state.stops.length === 1) {
        map.setView([state.stops[0].latitude, state.stops[0].longitude], 12);
    }
})();
