// Trip Create page JS moved to external file to avoid accidental rendering in other views
(function () {
    // Basic map setup
    const map = L.map('map').setView([34.0522, -118.2437], 8);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    window._tripCreate = window._tripCreate || {};
    const state = window._tripCreate;
    state.map = map;
    state.markers = [];
    state.stops = [];
    state.routingControl = null;

    // initialize from server-provided data if present
    if (window._tripCreate.initialData) {
        try {
            const init = window._tripCreate.initialData;
            if (init.stops && Array.isArray(init.stops)) {
                state.stops = init.stops.map(s => ({ id: s.id || (crypto ? crypto.randomUUID() : String(Math.random())), name: s.name || '', latitude: s.latitude || s.Latitude || 0, longitude: s.longitude || s.Longitude || 0, order: s.order || 0 }));
                // add markers
                state.stops.forEach(s => addMarker(s));
                renderStops();
            }
            if (init.routeGeometry && Array.isArray(init.routeGeometry)) {
                // expose as lastRoute so UI can use it
                window._lastRoute = window._lastRoute || {};
                window._lastRoute.geometry = init.routeGeometry.map(p => ({ latitude: p.latitude || p.Latitude, longitude: p.longitude || p.Longitude }));
                if (init.totalDistanceKm) window._lastRoute.distanceKm = init.totalDistanceKm;
                if (init.estimatedDuration) window._lastRoute.duration = init.estimatedDuration;
            }
            // prefill form fields if present
            try {
                if (init.name) document.getElementById('name').value = init.name;
                if (init.description) document.getElementById('description').value = init.description;
                if (init.startDate) document.getElementById('startDate').value = init.startDate;
                if (init.endDate) document.getElementById('endDate').value = init.endDate;
            } catch (e) { }
            // update routing if geometry exists
            if (window._lastRoute && window._lastRoute.geometry && window._lastRoute.geometry.length > 0) {
                // draw polyline from geometry
                const latlngs = window._lastRoute.geometry.map(p => [p.latitude, p.longitude]);
                L.polyline(latlngs, { color: 'crimson', weight: 6 }).addTo(map);
                map.fitBounds(L.polyline(latlngs).getBounds(), { padding: [40,40] });
            } else if (state.stops.length >= 2) {
                updateRouting();
            }
        } catch (err) {
            console.error('Error initializing trip-create with initialData', err);
        }
    }

    function addMarker(stop) {
        const m = L.marker([stop.latitude, stop.longitude]).addTo(map).bindPopup(stop.name);
        state.markers.push({ id: stop.id, marker: m });
    }

    function renderStops() {
        const ul = document.getElementById('stopsList');
        if (!ul) return;
        ul.innerHTML = '';
        state.stops.forEach((s, idx) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex justify-content-between align-items-center';
            li.innerHTML = `<div>${idx+1}. ${s.name}</div><div><button class='btn btn-sm btn-danger' onclick="window._tripCreate.removeStop('${s.id}')">Del</button></div>`;
            ul.appendChild(li);
        });
        if (window.Sortable) Sortable.create(ul, { onEnd: (e) => reorderStops(e) });
    }

    function reorderStops(e) {
        const ul = document.getElementById('stopsList');
        const items = Array.from(ul.children);
        state.stops = items.map((li, idx) => ({ ...state.stops.find(s => li.innerText.includes(s.name)), order: idx })).filter(Boolean);
        renderStops();
        updateRouting();
    }

    function removeStop(id) {
        state.stops = state.stops.filter(s => s.id !== id);
        const m = state.markers.find(x => x.id === id);
        if (m) { map.removeLayer(m.marker); state.markers = state.markers.filter(x => x.id !== id); }
        renderStops();
        updateRouting();
    }

    function updateRouting() {
        try { if (state.routingControl) { map.removeControl(state.routingControl); state.routingControl = null; } } catch (e) { }
        if (!state.stops || state.stops.length < 2) {
            return;
        }
        const waypoints = state.stops.map(s => L.latLng(s.latitude, s.longitude));
        const serviceUrl = window._tripCreate.routingServiceUrl || 'https://router.project-osrm.org/route/v1';
        state.routingControl = L.Routing.control({
            waypoints: waypoints,
            router: L.Routing.osrmv1({ serviceUrl: serviceUrl }),
            lineOptions: { styles: [{ color: 'crimson', weight: 6 }] },
            showAlternatives: false,
            fitSelectedRoute: true,
            createMarker: function (i, wp, n) { return L.marker(wp.latLng).bindPopup(state.stops[i].name); }
        }).addTo(map);

        state.routingControl.on('routesfound', function (e) {
            const route = e.routes[0];
            const distKm = route.summary.totalDistance / 1000.0;
            const timeSec = route.summary.totalTime;
            const h = Math.floor(timeSec / 3600);
            const m = Math.floor((timeSec % 3600) / 60);
            const s = Math.floor(timeSec % 60);
            const timestr = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
            let coords = [];
            try { coords = route.coordinates || route.geometry || []; } catch (err) { coords = []; }
            const geom = coords.map(function (p) { return { latitude: p.lat !== undefined ? p.lat : (p[1] || 0), longitude: p.lng !== undefined ? p.lng : (p[0] || 0) }; });
            window._lastRoute = { distanceKm: parseFloat(distKm.toFixed(2)), duration: timestr, geometry: geom };
        });
    }

    function addStopFromInputs() {
        const name = document.getElementById('stopName').value || 'Stop';
        const latlng = map.getCenter();
        const stop = { id: crypto.randomUUID(), name, latitude: latlng.lat, longitude: latlng.lng, order: state.stops.length };
        state.stops.push(stop);
        renderStops();
        updateRouting();
    }

    function calculateRoute() {
        const btn = document.getElementById('calcRouteBtn');
        if (!btn) return Promise.resolve(false);
        btn.disabled = true;
        const originalText = btn.innerText;
        btn.innerText = 'Calculando...';
        updateRouting();
        return new Promise((resolve) => {
            let waited = 0;
            const interval = setInterval(() => {
                if (window._lastRoute && window._lastRoute.geometry && window._lastRoute.geometry.length > 0) {
                    clearInterval(interval);
                    btn.disabled = false;
                    btn.innerText = originalText;
                    resolve(true);
                }
                waited += 200;
                if (waited >= 10000) {
                    clearInterval(interval);
                    btn.disabled = false;
                    btn.innerText = originalText;
                    resolve(false);
                }
            }, 200);
        });
    }

    // expose some helpers
    window._tripCreate.addMarker = addMarker;
    window._tripCreate.renderStops = renderStops;
    window._tripCreate.addStopFromInputs = addStopFromInputs;
    window._tripCreate.calculateRoute = calculateRoute;
    window._tripCreate.removeStop = removeStop;
    window._tripCreate.updateRouting = updateRouting;

    async function saveTrip() {
        const dto = {
            name: (document.getElementById('name') ? document.getElementById('name').value : '') ,
            description: (document.getElementById('description') ? document.getElementById('description').value : ''),
            startDate: (document.getElementById('startDate') ? document.getElementById('startDate').value : null) || null,
            endDate: (document.getElementById('endDate') ? document.getElementById('endDate').value : null) || null,
            stops: (state.stops || []).map((s, i) => ({ id: s.id, name: s.name, latitude: s.latitude, longitude: s.longitude, order: i })),
            totalDistanceKm: window._lastRoute ? window._lastRoute.distanceKm : 0,
            estimatedDuration: window._lastRoute ? window._lastRoute.duration : null,
            routeGeometry: window._lastRoute && window._lastRoute.geometry ? window._lastRoute.geometry : []
        };

        try {
            const res = await fetch('/api/trips', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(dto) });
            if (res.ok) {
                // redirect to dashboard
                window.location.href = '/';
            } else {
                const txt = await res.text();
                alert('Erro ao salvar viagem: ' + (txt || res.status));
            }
        } catch (err) {
            console.error(err);
            alert('Erro ao salvar viagem');
        }
    }

    // expose saveTrip globally for inline buttons
    window._tripCreate.saveTrip = saveTrip;
    window.saveTrip = saveTrip;

    // expose common helpers as globals for inline onclick handlers
    window.addStopFromInputs = addStopFromInputs;
    window.calculateRoute = calculateRoute;
    window.removeStop = removeStop;

})();
