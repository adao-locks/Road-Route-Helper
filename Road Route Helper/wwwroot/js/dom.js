// Shared DOM helpers. User text only ever reaches the page through textContent.
window.textPopup = function (text) {
    const element = document.createElement('div');
    element.textContent = text;
    return element;
};
