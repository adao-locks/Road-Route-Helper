// Shared DOM helpers. User text only ever reaches the page through textContent.
window.textPopup = function (text) {
    const element = document.createElement('div');
    element.textContent = text;
    return element;
};

window.stopCategories = function () {
    return JSON.parse(document.getElementById('stop-categories').textContent);
};

// Map marker with the same letter and color the poster uses for this category.
window.categoryIcon = function (category) {
    const style = window.stopCategories().find(c => c.category === category);
    const element = document.createElement('div');
    element.className = 'stop-icon-badge';
    element.style.backgroundColor = style.color;
    element.textContent = style.letter;
    return L.divIcon({ className: 'stop-icon', html: element, iconSize: [26, 26], iconAnchor: [13, 13], popupAnchor: [0, -13] });
};

// Dates come from the API with an offset: the written calendar day is the trip's day, whatever the browser's time zone.
window.formatDate = function (isoDate) {
    const [year, month, day] = isoDate.slice(0, 10).split('-');
    return `${day}/${month}/${year}`;
};

// Routing Machine options that keep its directions panel in Portuguese.
window.routingLocale = function (serviceUrl) {
    return {
        router: L.Routing.osrmv1({ serviceUrl, language: 'pt-BR' }),
        formatter: new L.Routing.Formatter({ language: 'pt', units: 'metric' })
    };
};
