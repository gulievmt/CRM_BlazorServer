let idleTimer = null;
let dotnetRef = null;
let timeoutMs = null;

const activityEvents = ["mousemove", "mousedown", "keydown", "scroll", "touchstart"];

export function start(dotnetHelper, timeoutInMs) {
    dotnetRef = dotnetHelper;
    timeoutMs = timeoutInMs;

    resetTimer();
    activityEvents.forEach(evt => document.addEventListener(evt, resetTimer, true));
}

function resetTimer() {
    clearTimeout(idleTimer);
    idleTimer = setTimeout(() => {
        console.log("Idle timeout reached. Invoking .NET method.");
        dotnetRef?.invokeMethodAsync("OnIdleTimeout");
    }, timeoutMs);
}

export function stop() {
    clearTimeout(idleTimer);
    activityEvents.forEach(evt => document.removeEventListener(evt, resetTimer, true));
    dotnetRef = null;
}