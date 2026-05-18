window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef, hostElement) {
        const id = this.nextId++;
        this.pumps[id] = false;

        const frame = () => {
            if (this.pumps[id]) return;

            const size = measure(hostElement);
            dotNetRef.invokeMethodAsync("Tick", size.width, size.height)
                .catch(error => console.error("klooie frame pump tick failed", error));

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
    const rect = measureCell(hostElement);
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

function measureCell(hostElement) {
    let probe = hostElement.querySelector(".browser-console-measure");
    if (!probe) {
        probe = document.createElement("span");
        probe.className = "browser-console-measure";
        probe.textContent = "M";
        hostElement.appendChild(probe);
    }

    return probe.getBoundingClientRect();
}
