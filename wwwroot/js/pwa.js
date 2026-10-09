(function () {
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('/sw.js').catch(function () { });

    var standalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone;
    var ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
    var hint = document.getElementById('iosHint');
    if (ios && !standalone && hint) hint.hidden = false;

    var deferred = null, btn = document.getElementById('installBtn');
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferred = e;
        if (btn) btn.hidden = false;
    });
    if (btn) btn.addEventListener('click', async function () {
        if (!deferred) return;
        deferred.prompt();
        await deferred.userChoice;
        deferred = null;
        btn.hidden = true;
    });
    window.addEventListener('appinstalled', function () { if (btn) btn.hidden = true; });
})();