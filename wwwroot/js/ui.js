(function () {
    // ---------- الوضع الداكن ----------
    var themeBtn = document.getElementById('themeBtn');
    if (themeBtn) themeBtn.addEventListener('click', function () {
        var next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
        document.documentElement.dataset.theme = next;
        try { localStorage.setItem('theme', next); } catch (e) { }
    });

    // ---------- الصور: تظهر بعد التحميل مع shimmer ----------
    document.querySelectorAll('img.fade').forEach(function (img) {
        function done() { img.classList.add('loaded'); }
        if (img.complete) done();
        else { img.addEventListener('load', done); img.addEventListener('error', done); }
    });

    // ---------- شريط التقدم أعلى الصفحة ----------
    var bar = document.getElementById('topbar');
    function run() { if (bar) bar.className = 'topbar run'; }

    document.addEventListener('click', function (e) {
        var a = e.target.closest && e.target.closest('a');
        if (!a || e.defaultPrevented || e.metaKey || e.ctrlKey || e.shiftKey) return;
        if (a.target === '_blank' || a.hasAttribute('download')) return;
        var h = a.getAttribute('href');
        if (!h || h.charAt(0) === '#' || a.origin !== location.origin) return;
        if (a.pathname === location.pathname && a.search === location.search) return;
        run();
    });
    document.addEventListener('submit', function (e) { if (!e.defaultPrevented) run(); });
    window.addEventListener('pageshow', function () { if (bar) bar.className = 'topbar'; });
})();