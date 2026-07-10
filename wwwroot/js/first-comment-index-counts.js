(function () {
    const numberLocale = 'es-ES';

    function formatCount(value) {
        return Number(value || 0).toLocaleString(numberLocale);
    }

    function showCountsError(section, message) {
        const status = section?.querySelector('.first-comment-counts-status');
        if (status) {
            status.classList.remove('alert-light', 'border');
            status.classList.add('alert-warning', 'border-0');
            status.innerHTML = `<i class="bi bi-exclamation-circle me-1"></i>${message}`;
        }

        section?.querySelectorAll('[data-count-display]').forEach(function (element) {
            element.textContent = '—';
        });
    }

    function applyCounts(counts) {
        document.querySelectorAll('[data-count-display]').forEach(function (element) {
            const key = element.dataset.countDisplay;
            if (!key || counts[key] === undefined) {
                return;
            }

            element.textContent = formatCount(counts[key]);
        });

        document.querySelectorAll('[data-first-comment-counts]').forEach(function (section) {
            section.classList.remove('first-comment-counts-loading');
            section.removeAttribute('aria-busy');

            const status = section.querySelector('.first-comment-counts-status');
            status?.classList.add('d-none');

            section.querySelectorAll('.first-comment-counts-pending').forEach(function (card) {
                card.classList.remove('first-comment-counts-pending');
            });
        });

        document.querySelectorAll('[data-bulk-index-estimate]').forEach(function (root) {
            let corpus = {};
            try {
                corpus = JSON.parse(root.dataset.corpus || '{}');
            } catch {
                corpus = {};
            }

            corpus.totalTickets = counts.totalTicketsWithFirstComment;
            corpus.pendingTickets = counts.pendingTickets;
            corpus.knowledgeBaseTotalTickets = counts.knowledgeBaseTotalTicketsWithFirstComment;
            corpus.knowledgeBasePendingTickets = counts.knowledgeBasePendingTickets;
            root.dataset.corpus = JSON.stringify(corpus);
        });

        document.dispatchEvent(new CustomEvent('firstCommentCountsUpdated', { detail: counts }));
    }

    async function loadCounts() {
        const section = document.querySelector('[data-first-comment-counts]');
        const countsUrl = section?.dataset.countsUrl;
        if (!countsUrl) {
            return;
        }

        try {
            const response = await fetch(countsUrl, { headers: { Accept: 'application/json' } });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const counts = await response.json();
            applyCounts(counts);
        } catch {
            if (section) {
                showCountsError(section, 'No se pudieron calcular los contadores. Recarga la página para reintentar.');
            }
        }
    }

    document.addEventListener('DOMContentLoaded', loadCounts);

    window.MoneyPennyFirstCommentIndex = window.MoneyPennyFirstCommentIndex || {};
    window.MoneyPennyFirstCommentIndex.reloadCounts = loadCounts;
})();
