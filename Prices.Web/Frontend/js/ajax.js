const getAntiForgeryToken = () => {
    return document
        .querySelector('meta[name="antiforgery-token"]')
        ?.getAttribute('content');
};

$.ajaxSetup({
    beforeSend: function (xhr, settings) {
        const method = (settings.type || 'GET').toUpperCase();

        // Only send antiforgery token for requests that can modify data
        if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS') {
            return;
        }

        const token = getAntiForgeryToken();

        if (token) {
            xhr.setRequestHeader('RequestVerificationToken', token);
        }
    }
});