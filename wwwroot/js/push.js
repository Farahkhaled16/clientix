(function () {
    var el = document.getElementById('pushCfg');
    if (!el || typeof firebase === 'undefined') return;
    if (!('serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window)) return;

    var d = el.dataset;
    if (!d.vapid || !d.apiKey || !d.sender || !d.app) return;

    firebase.initializeApp({ apiKey: d.apiKey, projectId: d.project, messagingSenderId: d.sender, appId: d.app });
    var messaging = firebase.messaging();

    var prompt = document.getElementById('pushPrompt');
    var yes = document.getElementById('pushYes');
    var later = document.getElementById('pushLater');

    async function ensureToken() {
        var reg = await navigator.serviceWorker.register('/sw.js');
        await navigator.serviceWorker.ready;
        var token = await messaging.getToken({ vapidKey: d.vapid, serviceWorkerRegistration: reg });
        if (!token) return;

        var same = localStorage.getItem('fcmToken') === token && localStorage.getItem('fcmUser') === d.uid;
        var fresh = Date.now() - parseInt(localStorage.getItem('fcmAt') || '0', 10) < 7 * 864e5;
        if (same && fresh) return;

        var r = await fetch('/push/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token: token })
        });
        if (r.ok) {
            localStorage.setItem('fcmToken', token);
            localStorage.setItem('fcmUser', d.uid);
            localStorage.setItem('fcmAt', String(Date.now()));
        }
    }

    if (Notification.permission === 'granted') {
        ensureToken().catch(function (e) { console.warn('push:', e); });
    } else if (Notification.permission === 'default' && prompt) {
        var snooze = parseInt(localStorage.getItem('pushSnooze') || '0', 10);
        if (Date.now() > snooze) prompt.hidden = false;
    }

    if (yes) yes.addEventListener('click', async function () {
        var p = await Notification.requestPermission();
        if (prompt) prompt.hidden = true;
        if (p === 'granted') { try { await ensureToken(); } catch (e) { console.warn('push:', e); } }
    });

    if (later) later.addEventListener('click', function () {
        localStorage.setItem('pushSnooze', String(Date.now() + 7 * 864e5));
        if (prompt) prompt.hidden = true;
    });

    // عند الخروج: نشيل توكن الجهاز ده من الحساب
    document.querySelectorAll('form[action="/logout"]').forEach(function (f) {
        f.addEventListener('submit', async function (e) {
            var t = localStorage.getItem('fcmToken');
            if (!t) return;
            e.preventDefault();
            try {
                await fetch('/push/unregister', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ token: t })
                });
            } catch (err) { }
            ['fcmToken', 'fcmUser', 'fcmAt'].forEach(function (k) { localStorage.removeItem(k); });
            f.submit();
        });
    });
})();