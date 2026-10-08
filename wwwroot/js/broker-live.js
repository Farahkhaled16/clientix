(function () {
    const badge = document.getElementById('bellCount');
    const toasts = document.getElementById('toasts');
    const icons = { visit: '👀', register: '🆕', login: '🔑', meeting: '📅', agency: '🏢', chat: '💬' };

    function bump() {
        if (!badge) return;
        badge.textContent = (parseInt(badge.textContent || '0', 10) + 1);
        badge.hidden = false;
    }

    function toast(n) {
        if (!toasts) return;
        const el = document.createElement('a');
        el.className = 'toast';
        el.href = n.link || '/Broker';
        el.innerHTML = '<b></b><span></span>';
        el.querySelector('b').textContent = (icons[n.type] || '🔔') + ' ' + (toasts.dataset[n.type] || '');
        el.querySelector('span').textContent = n.body || '';
        toasts.appendChild(el);
        setTimeout(() => el.remove(), 7000);
    }

    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    conn.on('notify', n => {
        bump();
        toast(n);
        document.dispatchEvent(new CustomEvent('broker-notify', { detail: n }));
    });

    conn.start().catch(() => { });
})();