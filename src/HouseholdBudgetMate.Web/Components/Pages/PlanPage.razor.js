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
