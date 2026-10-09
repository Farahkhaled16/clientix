namespace BrokerHub.Services;

public static class SwJs
{
    public const string Template = """
importScripts('https://www.gstatic.com/firebasejs/10.12.2/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/10.12.2/firebase-messaging-compat.js');

const OFFLINE = 'clientix-offline-v1';

self.addEventListener('install', function (e) {
  e.waitUntil(caches.open(OFFLINE).then(function (c) { return c.add('/offline.html'); }));
  self.skipWaiting();
});

self.addEventListener('activate', function (e) { e.waitUntil(self.clients.claim()); });

self.addEventListener('fetch', function (e) {
  if (e.request.mode === 'navigate') {
    e.respondWith(fetch(e.request).catch(function () { return caches.match('/offline.html'); }));
  }
});

try {
  firebase.initializeApp(__CONFIG__);
  const messaging = firebase.messaging();

  // رسائل data فقط: بنعرض الإشعار بنفسنا (الصفحة المفتوحة بتاخد التوست من SignalR)
  messaging.onBackgroundMessage(function (payload) {
    const d = payload.data || {};
    self.registration.showNotification(d.title || 'Clientix', {
      body: d.body || '',
      icon: '/img/icon-192.png',
      badge: '/img/badge-96.png',
      data: { link: d.link || '/' }
    });
  });
} catch (err) { /* إعدادات Firebase ناقصة: الـ PWA يفضل شغال من غير Push */ }

self.addEventListener('notificationclick', function (event) {
  event.notification.close();
  const link = (event.notification.data && event.notification.data.link) || '/';
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
      for (const c of list) {
        if ('focus' in c) {
          return c.focus().then(function (f) { return f.navigate(link).catch(function () { return self.clients.openWindow(link); }); });
        }
      }
      return self.clients.openWindow(link);
    })
  );
});
""";
}