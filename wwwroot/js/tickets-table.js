function initTicketsColumnResize(tableId, storageKey) {
    const table = document.getElementById(tableId);
    if (!table) {
        return;
    }

    const colgroup = table.querySelector('colgroup');
    if (!colgroup) {
        return;
    }

    const cols = Array.from(colgroup.querySelectorAll('col'));
    const minWidth = 56;

    function applyWidths(widths) {
        cols.forEach(function (col, index) {
            const key = col.dataset.col;
            const width = widths[key];
            if (width) {
                col.style.width = width;
            }
        });
    }

    function readWidths() {
        try {
            const raw = localStorage.getItem(storageKey);
            return raw ? JSON.parse(raw) : null;
        } catch {
            return null;
        }
    }

    function saveWidths() {
        const widths = {};
        cols.forEach(function (col) {
            if (col.dataset.col) {
                widths[col.dataset.col] = col.style.width || getComputedStyle(col).width;
            }
        });
        localStorage.setItem(storageKey, JSON.stringify(widths));
    }

    const saved = readWidths();
    if (saved) {
        applyWidths(saved);
    }

    table.querySelectorAll('.tickets-col-resizer').forEach(function (resizer) {
        const th = resizer.closest('th');
        if (!th) {
            return;
        }

        const colIndex = th.cellIndex;
        const col = cols[colIndex];
        if (!col) {
            return;
        }

        let startX = 0;
        let startWidth = 0;

        function onPointerMove(event) {
            const clientX = event.clientX ?? event.pageX;
            const nextWidth = Math.max(minWidth, startWidth + (clientX - startX));
            col.style.width = nextWidth + 'px';
        }

        function onPointerUp() {
            document.removeEventListener('pointermove', onPointerMove);
            document.removeEventListener('pointerup', onPointerUp);
            document.body.classList.remove('tickets-col-resizing');
            resizer.classList.remove('is-active');
            saveWidths();
        }

        function onPointerDown(event) {
            if (event.button !== undefined && event.button !== 0) {
                return;
            }

            event.preventDefault();
            startX = event.clientX ?? event.pageX;
            startWidth = th.getBoundingClientRect().width;
            document.body.classList.add('tickets-col-resizing');
            resizer.classList.add('is-active');
            document.addEventListener('pointermove', onPointerMove);
            document.addEventListener('pointerup', onPointerUp);
        }

        resizer.addEventListener('pointerdown', onPointerDown);

        resizer.addEventListener('keydown', function (event) {
            if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
                return;
            }

            event.preventDefault();
            const current = th.getBoundingClientRect().width;
            const delta = event.key === 'ArrowRight' ? 12 : -12;
            col.style.width = Math.max(minWidth, current + delta) + 'px';
            saveWidths();
        });
    });
}
