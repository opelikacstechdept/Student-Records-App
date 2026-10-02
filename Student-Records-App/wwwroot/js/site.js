// Site-wide behaviour. Inline event handlers (onclick, onsubmit) are blocked
// by the Content-Security-Policy (script-src 'self'), so wire them up here.

// Confirmation prompt for any form marked with data-confirm="message"
document.addEventListener('submit', function (e) {
    var form = e.target;
    var message = form.getAttribute && form.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        e.preventDefault();
    }
});
