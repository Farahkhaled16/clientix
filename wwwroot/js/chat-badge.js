(function () {
    if (location.pathname.toLowerCase().startsWith('/chat')) return;

    const badge = document.getElementById('chatBadge');
    const toasts = document.getElementById('toasts');

    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat')
        .withAutomaticReconnect()
        .build();

    conn.on('msg', function (m) {
        if (m.role !== 'broker') return;
        if (badge) {
            badge.textContent = (parseInt(badge.textContent || '0', 10) || 0) + 1;
            badge.hidden = false;
        }
        if (toasts) {
            const el = document.createElement('a');
            el.className = 'toast';
            el.href = '/Chat';
            el.innerHTML = '<b></b><span></span>';
            el.querySelector('b').textContent = '💬 ' + (toasts.dataset.chat || '');
            el.querySelector('span').textContent = m.text;
            toasts.appendChild(el);
            setTimeout(function () { el.remove(); }, 7000);
        }
    });

    conn.start().catch(function () { });
})();