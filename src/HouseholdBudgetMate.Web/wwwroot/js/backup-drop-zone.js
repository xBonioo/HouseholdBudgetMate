export function registerBackupDropZone(dropZone) {
    if (!dropZone) {
        return { dispose: () => { } };
    }

    const input = dropZone.querySelector('input[type="file"]');
    if (!input) {
        return { dispose: () => { } };
    }

    let dragDepth = 0;

    const stopEvent = event => {
        event.preventDefault();
        event.stopPropagation();
    };

    const activate = event => {
        stopEvent(event);
        dragDepth++;
        dropZone.classList.add('backup-drop-zone-active');
    };

    const keepActive = event => {
        stopEvent(event);
        dropZone.classList.add('backup-drop-zone-active');
    };

    const deactivate = event => {
        stopEvent(event);
        dragDepth = Math.max(0, dragDepth - 1);
        if (dragDepth === 0) {
            dropZone.classList.remove('backup-drop-zone-active');
        }
    };

    const drop = event => {
        stopEvent(event);
        dragDepth = 0;
        dropZone.classList.remove('backup-drop-zone-active');

        const file = event.dataTransfer?.files?.[0];
        if (!file) {
            return;
        }

        const transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    dropZone.addEventListener('dragenter', activate);
    dropZone.addEventListener('dragover', keepActive);
    dropZone.addEventListener('dragleave', deactivate);
    dropZone.addEventListener('drop', drop);

    return {
        dispose: () => {
            dropZone.removeEventListener('dragenter', activate);
            dropZone.removeEventListener('dragover', keepActive);
            dropZone.removeEventListener('dragleave', deactivate);
            dropZone.removeEventListener('drop', drop);
        }
    };
}
