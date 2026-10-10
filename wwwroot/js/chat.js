(function () {
    var root = document.getElementById('chatRoot');
    var list = document.getElementById('threadList');
    if (!root && !list) return;

    var status = document.getElementById('chatStatus');
    if (typeof signalR === 'undefined') {
        if (status) status.textContent = 'SignalR not loaded';
        return;
    }

    var myRole = document.body.dataset.role;                   // business | broker
    var T = root ? root.dataset : {};
    var thread = root ? (root.dataset.thread || '') : '';
    var log = document.getElementById('chatLog');
    var form = document.getElementById('chatForm');
    var input = document.getElementById('chatInput');
    var typingBar = document.getElementById('typingBar');
    var fileInput = document.getElementById('chatFile');
    var attachBtn = document.getElementById('attachBtn');
    var agencyBtn = document.getElementById('agencyBtn');
    var picker = document.getElementById('agencyPicker');
    var MAX = 25 * 1024 * 1024;
    var noop = function () { };

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }
    function down() { if (log) log.scrollTop = log.scrollHeight; }
    function fmtTime(iso) { return new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }); }
    function autosize() {
        if (!input) return;
        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 120) + 'px';
    }

    // ---------- رسم الرسائل ----------
    function agencyCard(a) {
        if (!a) return el('p', null, '🏢');
        var c = el('a', 'agency-msg');
        c.href = '/Agencies/Details?id=' + encodeURIComponent(a.id);

        var top = el('div', 'am-top');
        var av;
        if (a.logo) { av = el('img', 'am-logo'); av.src = a.logo; av.alt = ''; }
        else { av = el('div', 'am-logo am-init', (a.name || '?').charAt(0)); }
        var info = el('div', 'am-info');
        info.append(el('b', null, a.name));
        if (a.city) info.append(el('span', null, '📍 ' + a.city));
        top.append(av, info);

        var tags = el('div', 'am-tags');
        (a.services || '').split(',').map(function (s) { return s.trim(); }).filter(Boolean).slice(0, 3)
            .forEach(function (s) { tags.append(el('i', null, s)); });

        c.append(top, tags, el('span', 'am-go', (T.tView || '') + ' ›'));
        return c;
    }

    function content(m) {
        if (m.kind === 'image') {
            var a = el('a'); a.href = m.mediaUrl; a.target = '_blank'; a.rel = 'noopener';
            var im = el('img', 'chat-img'); im.src = m.mediaUrl; im.alt = m.fileName || ''; im.loading = 'lazy';
            im.addEventListener('load', down);
            a.append(im);
            return a;
        }
        if (m.kind === 'video') {
            var v = el('video', 'chat-video'); v.src = m.mediaUrl; v.controls = true; v.preload = 'metadata';
            return v;
        }
        if (m.kind === 'file') {
            var f = el('a', 'file-chip'); f.href = m.mediaUrl; f.target = '_blank'; f.rel = 'noopener';
            f.textContent = '📎 ' + (m.fileName || T.tFile || '');
            return f;
        }
        if (m.kind === 'agency') return agencyCard(m.agency);
        return el('p', null, m.text);
    }

    function bubble(m) {
        var mine = m.role === myRole;
        var cls = 'bubble ' + (mine ? 'me' : 'them');
        if (m.kind === 'agency') cls += ' bub-agency';
        if (m.kind === 'image' || m.kind === 'video') cls += ' bub-media';
        var d = el('div', cls);
        d.append(content(m));
        var meta = el('div', 'meta');
        meta.append(el('time', null, fmtTime(m.at)));
        if (mine) meta.append(el('span', 'ticks' + (m.read ? ' read' : ''), m.read ? '✓✓' : '✓'));
        d.append(meta);
        return d;
    }

    function removeEmpty() { var e = document.getElementById('chatEmpty'); if (e) e.remove(); }

    if (log) {
        var items = [];
        try { items = JSON.parse(document.getElementById('chatInit').textContent) || []; } catch (e) { }
        if (!items.length) {
            var emp = el('p', 'chat-empty', T.tEmpty || '');
            emp.id = 'chatEmpty';
            log.append(emp);
        } else {
            items.forEach(function (m) { log.append(bubble(m)); });
        }
        down();
    }

    // ---------- الاتصال ----------
    var conn = new signalR.HubConnectionBuilder().withUrl('/hubs/chat').withAutomaticReconnect().build();

    function setStatus(on, err) {
        if (!status) return;
        status.classList.toggle('on', on);
        status.textContent = err ? err : (on ? status.dataset.on : status.dataset.off);
    }

    var typingTimer;
    function hideTyping() { if (typingBar) typingBar.hidden = true; }
    function showTyping() {
        if (!typingBar) return;
        typingBar.hidden = false;
        down();
        clearTimeout(typingTimer);
        typingTimer = setTimeout(hideTyping, 3500);
    }

    conn.on('msg', function (m) {
        if (!log) return;
        if (thread && m.threadId !== thread) return;
        removeEmpty();
        log.appendChild(bubble(m));
        down();
        if (m.role !== myRole) {
            hideTyping();
            conn.invoke('MarkRead', thread || null).catch(noop);
        }
    });

    conn.on('read', function (r) {
        if (!log || r.by === myRole) return;
        if (thread && r.threadId !== thread) return;
        log.querySelectorAll('.bubble.me .ticks').forEach(function (t) {
            t.classList.add('read');
            t.textContent = '✓✓';
        });
    });

    conn.on('typing', function (t) {
        if (t.role === myRole) return;
        if (thread && t.threadId !== thread) return;
        showTyping();
    });

    // قايمة المحادثات عند الـ Broker
    conn.on('threadUpdate', function (u) {
        if (!list) return;
        var li = list.querySelector('[data-id="' + u.threadId + '"]');
        if (!li) { location.reload(); return; }
        li.querySelector('.last').textContent = u.last;
        list.prepend(li);
        if (u.threadId !== thread && u.role === 'business') {
            var b = li.querySelector('.unread-badge');
            if (!b) { b = el('span', 'unread-badge', '0'); li.querySelector('a').append(b); }
            b.textContent = (parseInt(b.textContent, 10) || 0) + 1;
        }
    });

    async function start() {
        try {
            await conn.start();
            if (myRole === 'broker' && thread) await conn.invoke('JoinThread', thread);
            setStatus(true);
            if (root) conn.invoke('MarkRead', myRole === 'broker' ? thread : null).catch(noop);
        } catch (e) {
            setStatus(false, 'Connection failed');
            setTimeout(start, 4000);
        }
    }
    conn.onreconnecting(function () { setStatus(false); });
    conn.onreconnected(async function () {
        if (myRole === 'broker' && thread) await conn.invoke('JoinThread', thread);
        setStatus(true);
    });
    conn.onclose(function () { setStatus(false); });

    // ---------- الإرسال ----------
    function connected() { return conn.state === signalR.HubConnectionState.Connected; }

    if (form && input) {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            var text = input.value.trim();
            if (!text || !connected()) return;
            input.value = '';
            autosize();
            try { await conn.invoke('Send', myRole === 'broker' ? thread : null, text); }
            catch (err) { input.value = text; }
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); }
        });

        var lastTyping = 0;
        input.addEventListener('input', function () {
            autosize();
            if (connected() && Date.now() - lastTyping > 2500) {
                lastTyping = Date.now();
                conn.invoke('Typing', myRole === 'broker' ? thread : null).catch(noop);
            }
        });
        autosize();
    }

    // ---------- رفع ملفات: زرار / لصق / سحب وإفلات ----------
    async function upload(f) {
        if (f.size > MAX) { alert(T.tToobig || 'Too large'); return; }
        var fd = new FormData();
        fd.append('file', f);
        var q = myRole === 'broker' ? '?thread=' + encodeURIComponent(thread) : '';

        var note = el('div', 'bubble me uploading');
        note.append(el('p', null, (T.tUploading || '') + ' ' + f.name));
        log.append(note);
        down();
        try {
            var r = await fetch('/chat/upload' + q, { method: 'POST', body: fd });
            if (!r.ok) throw new Error(String(r.status));
        } catch (err) {
            alert(T.tFail || 'Upload failed');
        } finally {
            note.remove();
        }
    }

    if (attachBtn && fileInput) {
        attachBtn.addEventListener('click', function () { fileInput.click(); });
        fileInput.addEventListener('change', function () {
            var f = fileInput.files[0];
            fileInput.value = '';
            if (f) upload(f);
        });
    }
    if (log) {
        log.addEventListener('dragover', function (e) { e.preventDefault(); });
        log.addEventListener('drop', function (e) {
            e.preventDefault();
            var f = e.dataTransfer && e.dataTransfer.files[0];
            if (f) upload(f);
        });
    }
    if (input) {
        input.addEventListener('paste', function (e) {
            var f = e.clipboardData && e.clipboardData.files[0];
            if (f) { e.preventDefault(); upload(f); }
        });
    }

    // ---------- كارت وكالة (Broker) ----------
    var agencies = [], loaded = false;

    function renderPicker(q) {
        var box = picker.querySelector('.pk-list');
        box.textContent = '';
        q = (q || '').trim().toLowerCase();
        agencies
            .filter(function (a) { return !q || (a.name + ' ' + (a.city || '')).toLowerCase().indexOf(q) > -1; })
            .forEach(function (a) {
                var b = el('button', 'pk-item');
                b.type = 'button';
                b.append(el('b', null, a.name), el('span', null, a.city || ''));
                b.addEventListener('click', function () {
                    conn.invoke('SendAgency', thread, a.id).catch(noop);
                    picker.hidden = true;
                });
                box.append(b);
            });
    }

    if (agencyBtn && picker) {
        agencyBtn.addEventListener('click', async function () {
            picker.hidden = false;
            if (!loaded) {
                try { agencies = await (await fetch('/chat/agencies')).json(); loaded = true; } catch (e) { }
            }
            var search = picker.querySelector('input');
            search.value = '';
            renderPicker('');
            search.focus();
        });
        picker.addEventListener('click', function (e) {
            if (e.target === picker || e.target.closest('[data-close]')) picker.hidden = true;
        });
        picker.querySelector('input').addEventListener('input', function (e) { renderPicker(e.target.value); });
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape') picker.hidden = true; });
    }

    start();
})();