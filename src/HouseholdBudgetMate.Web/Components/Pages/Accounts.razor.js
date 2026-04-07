const normalizeDecimalInput = (value) => {
    let normalized = String(value ?? "");
    normalized = normalized.replace(/,/g, ".");
    normalized = normalized.replace(/[^0-9.\-]/g, "");

    const negative = normalized.startsWith("-") ? "-" : "";
    normalized = normalized.replace(/-/g, "");

    const parts = normalized.split(".");
    if (parts.length > 2) {
        normalized = `${parts[0]}.${parts.slice(1).join("")}`;
    }

    return `${negative}${normalized}`;
};

window.parseLocalizedDecimal = (value) => {
    const normalized = normalizeDecimalInput(value).trim();
    if (!normalized || normalized === "-" || normalized === "." || normalized === "-.") {
        return null;
    }

    const parsed = parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};

document.addEventListener("input", (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement) || input.dataset.decimalMask !== "true") {
        return;
    }

    input.value = normalizeDecimalInput(input.value);
});
