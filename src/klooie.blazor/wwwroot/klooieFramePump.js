window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef) {
        const id = this.nextId++;
        let pending = false;
        this.pumps[id] = false;

        const frame = () => {
            if (this.pumps[id]) return;

            if (!pending) {
                pending = true;
                dotNetRef.invokeMethodAsync("Tick")
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
