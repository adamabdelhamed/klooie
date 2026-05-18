window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef, hostElement) {
        const id = this.nextId++;
        let pending = false;
        this.pumps[id] = false;

        const frame = () => {
            if (this.pumps[id]) return;

            if (!pending) {
                pending = true;
                const size = measure(hostElement);
                dotNetRef.invokeMethodAsync("Tick", size.width, size.height)
                    .catch(error => console.error("klooie frame pump tick failed", error))
                    .finally(() => pending = false);
            }

            requestAnimationFrame(frame);
        };

        requestAnimationFrame(frame);
        return id;
    },
    stop(id) {
        this.pumps[id] = true;
    }
};

function measure(hostElement) {
    const cell = hostElement.querySelector(".browser-console-cell");
    const rect = cell?.getBoundingClientRect();
    const viewport = window.visualViewport;
    const viewportWidth = viewport?.width || window.innerWidth || document.documentElement.clientWidth;
    const viewportHeight = viewport?.height || window.innerHeight || document.documentElement.clientHeight;
    const cellWidth = Math.max(1, rect?.width || 8);
    const cellHeight = Math.max(1, rect?.height || 16);

    return {
        width: Math.max(1, Math.floor(viewportWidth / cellWidth)),
        height: Math.max(1, Math.floor(viewportHeight / cellHeight))
    };
}
