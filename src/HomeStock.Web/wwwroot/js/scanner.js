// Thin wrapper around the bundled html5-qrcode library, exposed to Blazor via IJSRuntime.
// Works across Android/iOS/desktop browsers with camera access; degrades gracefully when
// no camera is available (the page offers manual entry).
window.homestockScanner = (function () {
    let instance = null;
    let running = false;

    async function start(regionId, dotNetRef) {
        if (running) return true;
        if (typeof Html5Qrcode === 'undefined') return false;
        try {
            instance = new Html5Qrcode(regionId, { verbose: false });
            const config = { fps: 10, qrbox: { width: 250, height: 250 }, aspectRatio: 1.0 };
            await instance.start(
                { facingMode: 'environment' },
                config,
                (decodedText) => {
                    // Notify Blazor; the component decides whether to stop.
                    dotNetRef.invokeMethodAsync('OnScan', decodedText);
                },
                () => { /* per-frame decode failure — ignore */ }
            );
            running = true;
            return true;
        } catch (e) {
            running = false;
            return false;
        }
    }

    async function stop() {
        if (instance && running) {
            try { await instance.stop(); } catch (e) { /* ignore */ }
            try { await instance.clear(); } catch (e) { /* ignore */ }
        }
        running = false;
        instance = null;
    }

    return { start, stop };
})();
