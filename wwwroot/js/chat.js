(function () {
    const log = document.getElementById('chatLog');
    const list = document.getElementById('threadList');
    if (!log && !list) return;

    const myRole = log ? log.dataset.role : 'broker';
    const thread = log ? (log.dataset.thread || '') : '';
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    const status = document.getElementById('chatStatus');

    if (typeof signalR === 'undefined') {
        if (status) status.textContent = 'SignalR library not loaded';
        console.error('signalR is not defined');
        return;
    }

    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat')
        .withAutomaticReconnect()
        .build();

    function setStatus(on, err) {
        if (!status) return;
        status.classList.toggle('on', on);
        status.textContent = err ? err : (on ? status.dataset.on : status.dataset.off);
    }
    function down() { if (log) log.scrollTop = log.scrollHeight; }
    function autosize() {
        if (!input) return;
        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 120) + 'px';
    }

    function bubble(m) {
        const d = document.createElement('div');
        d.className = 'bubble ' + (m.role === myRole ? 'me' : 'them');
        const p = document.createElement('p');
        p.textContent = m.text;
        const t = document.createElement('time');
        t.textContent = new Date(m.at).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
        d.append(p, t);
        return d;
    }

    conn.on('msg', function (m) {
        if (!log) return;
        if (thread && m.threadId !== thread) return;
        const empty = document.getElementById('chatEmpty');
        if (empty) empty.remove();
        log.appendChild(bubble(m));
        down();
        if (m.role !== myRole) conn.invoke('MarkRead', thread || null).catch(function () { });
    });

    conn.on('threadUpdate', function (u) {
        if (!list) return;
        const li = list.querySelector('[data-id="' + u.threadId + '"]');
        if (!li) { location.reload(); return; }
        li.querySelector('.last').textContent = u.last;
        list.prepend(li);
        if (u.threadId !== thread && u.role === 'business') {
            let b = li.querySelector('.unread-badge');
            if (!b) {
                b = document.createElement('span');
                b.className = 'unread-badge';
                b.textContent = '0';
                li.querySelector('a').append(b);
            }
            b.textContent = (parseInt(b.textContent, 10) || 0) + 1;
        }
    });

    async function start() {
        try {
            await conn.start();
            if (myRole === 'broker' && thread) await conn.invoke('JoinThread', thread);
            setStatus(true);
        } catch (e) {
            console.error('Chat connection failed:', e);
            setStatus(false, 'Connection failed: ' + (e && e.message ? e.message : e));
            setTimeout(start, 4000);
        }
    }
    conn.onreconnecting(function () { setStatus(false); });
    conn.onreconnected(async function () {
        if (myRole === 'broker' && thread) await conn.invoke('JoinThread', thread);
        setStatus(true);
    });
    conn.onclose(function () { setStatus(false); });

    if (form && input) {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const text = input.value.trim();
            if (!text) return;
            if (conn.state !== signalR.HubConnectionState.Connected) {
                setStatus(false, 'Not connected yet, please wait...');
                return;
            }
            input.value = '';
            autosize();
            try {
                await conn.invoke('Send', myRole === 'broker' ? thread : null, text);
            } catch (err) {
                console.error('Send failed:', err);
                input.value = text;
                setStatus(true, 'Send failed: ' + (err && err.message ? err.message : err));
            }
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); }
        });
        input.addEventListener('input', autosize);
        autosize();
    }

    down();
    start();
})();