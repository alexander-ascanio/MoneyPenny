(function () {
    const countFormatter = new Intl.NumberFormat('es-ES');

    function formatCount(count) {
        return countFormatter.format(Number(count || 0));
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function showLoading(modal) {
        modal.querySelector('[data-indexed-summary-loading]')?.classList.remove('d-none');
        modal.querySelector('[data-indexed-summary-error]')?.classList.add('d-none');
        modal.querySelector('[data-indexed-summary-content]')?.classList.add('d-none');
    }

    function showError(modal, message) {
        modal.querySelector('[data-indexed-summary-loading]')?.classList.add('d-none');
        modal.querySelector('[data-indexed-summary-content]')?.classList.add('d-none');
        const error = modal.querySelector('[data-indexed-summary-error]');
        if (error) {
            error.textContent = message;
            error.classList.remove('d-none');
        }
    }

    function showContent(modal, data) {
        modal.querySelector('[data-indexed-summary-loading]')?.classList.add('d-none');
        modal.querySelector('[data-indexed-summary-error]')?.classList.add('d-none');
        modal.querySelector('[data-indexed-summary-content]')?.classList.remove('d-none');

        const scope = modal.querySelector('[data-indexed-summary-scope]');
        if (scope) {
            scope.textContent = 'Resumen por fecha de creación del ticket en TeamSupport.';
        }

        const tableBody = modal.querySelector('[data-indexed-summary-table-body]');
        if (tableBody) {
            const months = Array.isArray(data.months) ? data.months : [];
            if (months.length === 0) {
                tableBody.innerHTML =
                    '<tr><td colspan="3" class="text-muted">No hay tickets indexados en este ámbito.</td></tr>';
            } else {
                tableBody.innerHTML = months.map(item => {
                    const monthName = item.monthName ?? item.label ?? '';
                    const year = item.year ?? '';
                    const quantity = item.ticketCountFormatted ?? formatCount(item.ticketCount);
                    return `
                        <tr>
                            <td>${escapeHtml(monthName)}</td>
                            <td>${escapeHtml(year)}</td>
                            <td class="text-end fw-semibold text-success">${escapeHtml(quantity)}</td>
                        </tr>`;
                }).join('');
            }
        }

        const total = modal.querySelector('[data-indexed-summary-total]');
        if (total) {
            total.textContent = `Total indexados: ${data.totalTicketsFormatted ?? formatCount(data.totalTickets)} ticket(s).`;
        }
    }

    async function openSummary(card) {
        const modalElement = document.getElementById('indexedByMonthModal');
        if (!modalElement || !window.bootstrap?.Modal) {
            return;
        }

        const summaryUrl = card.dataset.summaryUrl;
        const knowledgeBase = card.dataset.knowledgeBase === 'true';
        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);

        showLoading(modalElement);
        modal.show();

        try {
            const url = `${summaryUrl}?knowledgeBaseScope=${knowledgeBase ? 'true' : 'false'}`;
            const response = await fetch(url, { headers: { Accept: 'application/json' } });
            if (!response.ok) {
                throw new Error(`No se pudo cargar el resumen (HTTP ${response.status}).`);
            }

            const data = await response.json();
            showContent(modalElement, data);
        } catch (error) {
            showError(
                modalElement,
                error instanceof Error ? error.message : 'No se pudo cargar el resumen.');
        }
    }

    function initIndexedSummaryCards() {
        document.querySelectorAll('[data-indexed-summary]').forEach(card => {
            const activate = () => openSummary(card);

            card.addEventListener('click', activate);
            card.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    activate();
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', initIndexedSummaryCards);
})();
