// FamilySplit — Web Push helpers called via IJSRuntime from Blazor.
// All functions return null / false on failure so callers can handle gracefully.

// ── SW update notification ────────────────────────────────────────────────────
// Called from MainLayout on first render. When the 'sw-updated' event fires
// (dispatched by the controllerchange listener in index.html), invokes the
// .NET callback so Blazor can show a snackbar rather than a hard auto-reload.
window.FamilySplitSw = {
    registerUpdateListener: function (dotNetObj) {
        window.addEventListener('sw-updated', function () {
            dotNetObj.invokeMethodAsync('OnSwUpdated');
        }, { once: true });
    },
    reload: function () {
        window.location.reload();
    }
};

window.FamilySplitPush = (function () {

    // ── Utilities ─────────────────────────────────────────────────────────────

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64  = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw     = window.atob(base64);
        const array   = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; ++i) array[i] = raw.charCodeAt(i);
        return array;
    }

    function arrayBufferToBase64Url(buffer) {
        return btoa(String.fromCharCode(...new Uint8Array(buffer)))
            .replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    }

    // ── Feature detection ─────────────────────────────────────────────────────

    function isSupported() {
        return 'serviceWorker' in navigator && 'PushManager' in window;
    }

    // ── Permission ────────────────────────────────────────────────────────────

    async function getPermissionState() {
        if (!isSupported()) return 'unsupported';
        return Notification.permission; // 'default' | 'granted' | 'denied'
    }

    async function requestPermission() {
        if (!isSupported()) return false;
        const result = await Notification.requestPermission();
        return result === 'granted';
    }

    // ── Subscribe ─────────────────────────────────────────────────────────────

    /**
     * Subscribe the current browser to push notifications.
     * Returns { endpoint, p256dh, auth } on success, null on failure.
     */
    async function subscribe(vapidPublicKey) {
        if (!isSupported()) return null;
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.subscribe({
                userVisibleOnly:      true,
                applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
            });
            return {
                endpoint: sub.endpoint,
                p256dh:   arrayBufferToBase64Url(sub.getKey('p256dh')),
                auth:     arrayBufferToBase64Url(sub.getKey('auth')),
            };
        } catch (err) {
            console.warn('[FamilySplitPush] subscribe failed:', err);
            return null;
        }
    }

    // ── Unsubscribe ───────────────────────────────────────────────────────────

    /**
     * Unsubscribe the current browser from push notifications.
     * Returns the endpoint string (so the server can remove it), or null.
     */
    async function unsubscribe() {
        if (!isSupported()) return null;
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            if (!sub) return null;
            const endpoint = sub.endpoint;
            await sub.unsubscribe();
            return endpoint;
        } catch (err) {
            console.warn('[FamilySplitPush] unsubscribe failed:', err);
            return null;
        }
    }

    // ── Current subscription check ────────────────────────────────────────────

    /**
     * Returns the current subscription's endpoint, or null if not subscribed.
     */
    async function getCurrentEndpoint() {
        if (!isSupported()) return null;
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            return sub ? sub.endpoint : null;
        } catch {
            return null;
        }
    }

    return { isSupported, getPermissionState, requestPermission, subscribe, unsubscribe, getCurrentEndpoint };
})();
