(function () {
    if (location.pathname.toLowerCase().startsWith('/chat')) return;
    if (typeof signalR === 'undefined') return;

    var badge = document.getElementById('chatBadge');
    var toasts = document.getElementById('toasts');

    var conn = new signalR.HubConnectionBuilder().withUrl('/hubs/chat').withAutomaticReconnect().build();

    conn.on('msg', function (m) {
        if (m.role !== 'broker') return;
        if (badge) {
            badge.textContent = (parseInt(badge.textContent || '0', 10) || 0) + 1;
            badge.hidden = false;
        }
        if (toasts) {
            var el = document.createElement('a');
            el.className = 'toast';
            el.href = '/Chat';
            el.innerHTML = '<b></b><span></span>';
            el.querySelector('b').textContent = '💬 ' + (toasts.dataset.chat || '');
            el.querySelector('span').textContent = m.preview || m.text || '';
            toasts.appendChild(el);
            setTimeout(function () { el.remove(); }, 7000);
        }
    });

    conn.start().catch(function () { });
})();