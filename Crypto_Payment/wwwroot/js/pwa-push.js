/**
 * Polling bildirim akışı.
 * Uygulama açıkken yerel bildirim ile bekleyen talepleri gösterir.
 */
(function () {
    'use strict';
    var POLL_INTERVAL = 45000;
    var STORAGE_KEY = 'notifyLocalEnabled';
    var LAST_STATE_KEY = 'notifyLastState';
    var PRIMED_KEY = 'notifyPrimed';
    var _pollTimer = null;

    function getLastState() {
        try {
            var s = localStorage.getItem(LAST_STATE_KEY);
            return s ? JSON.parse(s) : { approvals: 0, withdrawals: 0, expenses: 0, myRequests: 0 };
        } catch (_) { return { approvals: 0, withdrawals: 0, expenses: 0, myRequests: 0 }; }
    }
    function setLastState(s) {
        try { localStorage.setItem(LAST_STATE_KEY, JSON.stringify(s)); } catch (_) {}
    }
    function showLocalNotification(title, body, tag) {
        if (!('Notification' in window) || Notification.permission !== 'granted') return;
        try {
            var n = new Notification(title, { body: body, tag: tag || 'notify', icon: '/admin/velzon-dist/assets/images/favicon.ico' });
            n.onclick = function () { n.close(); window.focus(); window.location.href = '/approvals'; };
            setTimeout(function () { n.close(); }, 8000);
        } catch (e) { console.warn('[Notify]', e); }
    }
    function poll() {
        fetch('/api/notifications/summary', { credentials: 'include' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                var last = getLastState();
                var primed = localStorage.getItem(PRIMED_KEY) === '1';
                if (!primed) {
                    setLastState(data);
                    localStorage.setItem(PRIMED_KEY, '1');
                    return;
                }
                if ((data.approvals || 0) > last.approvals)
                    showLocalNotification('Yeni Onay Talebi', (data.approvals || 0) + ' onay bekliyor', 'approvals');
                if ((data.withdrawals || 0) > last.withdrawals)
                    showLocalNotification('Çekim Talebi', (data.withdrawals || 0) + ' çekim talebi', 'withdrawals');
                if ((data.expenses || 0) > last.expenses)
                    showLocalNotification('Gider Talebi', (data.expenses || 0) + ' gider talebi', 'expenses');
                if ((data.myRequests || 0) > last.myRequests)
                    showLocalNotification('Talebiniz', 'Talep durumu güncellendi', 'myreq');
                setLastState(data);
            })
            .catch(function () {});
    }
    function startPolling() {
        stopPolling();
        poll();
        _pollTimer = setInterval(poll, POLL_INTERVAL);
    }
    function stopPolling() {
        if (_pollTimer) { clearInterval(_pollTimer); _pollTimer = null; }
    }
    function isEnabled() {
        return localStorage.getItem(STORAGE_KEY) === '1' && 'Notification' in window && Notification.permission === 'granted';
    }
    function finishEnable() {
        localStorage.setItem(STORAGE_KEY, '1');
        startPolling();
        showLocalNotification('Bildirimler Açıldı', 'Onay, fatura ve taleplerden anında haberdar olacaksınız.', 'welcome');
        document.dispatchEvent(new CustomEvent('pushEnabled'));
        fetch('/api/push/test', { method: 'POST', credentials: 'include' }).catch(function () {});
        if (typeof Swal !== 'undefined') Swal.fire({ icon: 'success', title: 'Aktif', text: 'Bildirimler açıldı!' });
    }

    window.enablePushNotifications = function () {
        if (!('Notification' in window)) {
            if (typeof Swal !== 'undefined') Swal.fire({ icon: 'error', title: 'Desteklenmiyor', text: 'Bu tarayıcı bildirimleri desteklemiyor.' });
            else alert('Bu tarayıcı bildirimleri desteklemiyor.');
            return Promise.resolve(false);
        }
        if (Notification.permission === 'denied') {
            if (typeof Swal !== 'undefined') Swal.fire({ icon: 'warning', title: 'Engellenmiş', text: 'Bildirimler engelli. Site ayarlarından izin verin.' });
            else alert('Bildirimler engellenmiş.');
            return Promise.resolve(false);
        }
        if (Notification.permission === 'granted') {
            finishEnable();
            return Promise.resolve(true);
        }
        return Notification.requestPermission().then(function (p) {
            if (p !== 'granted') {
                if (typeof Swal !== 'undefined') Swal.fire({ icon: 'info', title: 'İptal', text: 'İzin verilmedi.' });
                return false;
            }
            finishEnable();
            return true;
        });
    };
    window.isPushEnabled = function () { return isEnabled(); };
    window.disablePushNotifications = function () {
        localStorage.removeItem(STORAGE_KEY);
        stopPolling();
        document.dispatchEvent(new CustomEvent('pushDisabled'));
    };

    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible' && isEnabled()) poll();
    });
    document.addEventListener('DOMContentLoaded', function () {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function () {});
        }
        if (isEnabled()) startPolling();
        document.dispatchEvent(new CustomEvent('pwaReady'));
    });
})();
