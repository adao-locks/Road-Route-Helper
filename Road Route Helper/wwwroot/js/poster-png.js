// Converts the trip's SVG poster to a PNG in the browser, with no image library on the server.
(function () {
    const WIDTH = 2400;
    const HEIGHT = 1600;

    function loadImage(url) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = () => reject(new Error('O poster não pôde ser carregado como imagem.'));
            image.src = url;
        });
    }

    function canvasToBlob(canvas) {
        return new Promise((resolve, reject) => {
            canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('O canvas não gerou a imagem.')), 'image/png');
        });
    }

    window.exportPosterPng = async function (svgUrl, fileName) {
        const response = await fetch(svgUrl);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const svgObjectUrl = URL.createObjectURL(new Blob([await response.text()], { type: 'image/svg+xml' }));
        try {
            const image = await loadImage(svgObjectUrl);
            const canvas = document.createElement('canvas');
            canvas.width = WIDTH;
            canvas.height = HEIGHT;
            canvas.getContext('2d').drawImage(image, 0, 0, WIDTH, HEIGHT);

            const pngObjectUrl = URL.createObjectURL(await canvasToBlob(canvas));
            const link = document.createElement('a');
            link.href = pngObjectUrl;
            link.download = fileName;
            link.click();
            setTimeout(() => URL.revokeObjectURL(pngObjectUrl), 1000);
        } finally {
            URL.revokeObjectURL(svgObjectUrl);
        }
    };

    const button = document.getElementById('exportPng');
    const error = document.getElementById('exportError');
    button.addEventListener('click', async () => {
        button.disabled = true;
        error.textContent = '';
        error.classList.add('d-none');
        try {
            await window.exportPosterPng(button.dataset.svgUrl, button.dataset.fileName);
        } catch (err) {
            console.error(err);
            error.textContent = 'Não foi possível gerar o PNG.';
            error.classList.remove('d-none');
        }
        button.disabled = false;
    });
})();
