(function () {
    if (typeof signalR === 'undefined') return;

    var role = document.body.dataset.role;
    var url = role === 'broker' ? '/hubs/notifications'
        : (role === 'business' || role === 'agency') ? '/hubs/user' : null;
    if (!url) return;

    var toasts = document.getElementById('toasts');
    var icons = {
        visit: '👀', register: '🆕', login: '🔑', meeting: '📅', agency: '🏢', chat: '💬',
        approved: '✅', rejected: '❌', cancelled: '🚫', reminder: '⏰'
    };

    function toast(n) {
        if (!toasts) return;
        var el = document.createElement('a');
        el.className = 'toast';
        el.href = n.link || '/';
        el.innerHTML = '<b></b><span></span>';
        el.querySelector('b').textContent = (icons[n.type] || '🔔') + ' ' + (toasts.dataset[n.type] || '');
        el.querySelector('span').textContent = n.body || '';
        toasts.appendChild(el);
        setTimeout(function () { el.remove(); }, 7000);
    }

    var conn = new signalR.HubConnectionBuilder().withUrl(url).withAutomaticReconnect().build();

    conn.on('notify', function (n) {
        toast(n);
        document.dispatchEvent(new CustomEvent('clientix-notify', { detail: n }));
        if (role === 'broker') document.dispatchEvent(new CustomEvent('broker-notify', { detail: n }));
    });

    conn.start().catch(function () { });
})();