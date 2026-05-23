window.scrollToCreateExpenseForm = () => {
    const anchor = document.getElementById("create-expense-anchor");
    if (!anchor) return;
    anchor.scrollIntoView({ behavior: "smooth", block: "start" });
};

window.scrollExpenseRowIntoView = (anchorId) => {
    if (!anchorId) {
        return;
    }

    const anchor = document.getElementById(anchorId);
    if (!anchor) {
        return;
    }

    const row = anchor.closest("tr") ?? anchor;
    row.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
};

window.measureIncomePanelOverlayGeometry = (wrapperElement) => {
    if (!wrapperElement) {
        return [0, 0, 0];
    }

    const wrapperRect = wrapperElement.getBoundingClientRect();
    const grid = wrapperElement.closest(".plan-dashboard-grid");
    if (!grid) {
        return [Math.ceil(wrapperRect.left), Math.ceil(wrapperRect.top), Math.ceil(wrapperRect.width)];
    }

    const gridRect = grid.getBoundingClientRect();
    const viewportPadding = 24;
    const maxRight = Math.min(gridRect.right, window.innerWidth - viewportPadding);
    const measured = maxRight - wrapperRect.left;

    return [
        Math.ceil(wrapperRect.left),
        Math.ceil(wrapperRect.top),
        Math.ceil(Math.max(wrapperRect.width, measured))
    ];
};

window.startIncomeToggleViewportWatcher = (dotNetRef, breakpoint) => {
    if (!dotNetRef) {
        return;
    }

    if (window.__hbIncomeToggleHandler) {
        window.removeEventListener("resize", window.__hbIncomeToggleHandler);
    }

    const handler = () => {
        const isVisible = window.innerWidth > breakpoint;
        dotNetRef.invokeMethodAsync("SetIncomePanelToggleVisibilityAsync", isVisible);
    };

    window.__hbIncomeToggleHandler = handler;
    window.addEventListener("resize", handler);
    handler();
};

window.stopIncomeToggleViewportWatcher = () => {
    if (window.__hbIncomeToggleHandler) {
        window.removeEventListener("resize", window.__hbIncomeToggleHandler);
        window.__hbIncomeToggleHandler = null;
    }
};

