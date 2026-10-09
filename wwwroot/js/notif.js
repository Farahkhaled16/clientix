(function () {
    var btn = document.getElementById('notifBtn');
    var pop = document.getElementById('notifPop');
    if (!btn || !pop) return;

    var list = document.getElementById('notifList');
    var badge = document.getElementById('notifBadge');
    var toasts = document.getElementById('toasts');
    var lang = document.documentElement.lang || 'ar';
    var icons = {
        visit: '👀', register: '🆕', login: '🔑', meeting: '📅', agency: '🏢', chat: '💬',
        approved: '✅', rejected: '❌', cancelled: '🚫', reminder: '⏰'
    };

    function getBadge() { return parseInt(badge.textContent, 10) || 0; }
    function setBadge(n) { badge.textContent = n > 99 ? '99+' : String(n); badge.hidden = n <= 0; }

    function ago(iso) {
        var s = Math.round((Date.now() - new Date(iso).getTime()) / 1000);
        var rtf = new Intl.RelativeTimeFormat(lang, { numeric: 'auto' });
        if (s < 60) return rtf.format(0, 'second');
        if (s < 3600) return rtf.format(-Math.floor(s / 60), 'minute');
        if (s < 86400) return rtf.format(-Math.floor(s / 3600), 'hour');
        if (s < 604800) return rtf.format(-Math.floor(s / 86400), 'day');
        return new Date(iso).toLocaleDateString(lang, { day: 'numeric', month: 'short' });
    }

    function row(n) {
        var a = document.createElement('a');
        a.className = 'np-item' + (n.read ? '' : ' unread');
        a.href = n.link || '#';
        var ico = document.createElement('div');
        ico.className = 'np-ico';
        ico.textContent = icons[n.type] || '🔔';
        var box = document.createElement('div');
        box.className = 'np-text';
        var t = document.createElement('b'); t.textContent = n.title || '';
        var s = document.createElement('span'); s.textContent = n.body || '';
        var tm = document.createElement('time'); tm.textContent = ago(n.at);
        box.append(t, s, tm);
        a.append(ico, box);
        return a;
    }

    function render(items) {
        list.textContent = '';
        if (!items.length) {
            var p = document.createElement('p');
            p.className = 'np-empty';
            p.textContent = pop.dataset.empty;
            list.append(p);
            return;
        }
        items.forEach(function (n) { list.append(row(n)); });
    }

    function markSeen() {
        fetch('/notifications/read-all', { method: 'POST' }).catch(function () { });
        setBadge(0);
    }

    async function load() {
        try {
            var r = await fetch('/notifications/list', { headers: { Accept: 'application/json' } });
            var data = await r.json();
            render(data.items || []);
            markSeen();
        } catch (e) { }
    }

    function open() { pop.hidden = false; btn.setAttribute('aria-expanded', 'true'); load(); }
    function close() { pop.hidden = true; btn.setAttribute('aria-expanded', 'false'); }

    btn.addEventListener('click', function (e) { e.stopPropagation(); pop.hidden ? open() : close(); });
    document.addEventListener('click', function (e) {
        if (!pop.hidden && !pop.contains(e.target) && !btn.contains(e.target)) close();
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') close(); });

    // إشعار جديد وصل لحظيًا
    document.addEventListener('clientix-notify', function (e) {
        var n = e.detail;
        if (!pop.hidden) {
            var empty = list.querySelector('.np-empty');
            if (empty) empty.remove();
            list.prepend(row({
                type: n.type, title: (toasts && toasts.dataset[n.type]) || '', body: n.body,
                link: n.link, read: false, at: new Date().toISOString()
            }));
            markSeen();
        } else {
            setBadge(getBadge() + 1);
        }
    });
})();