(function () {
    function formatTicketCount(count) {
        return Number(count || 0).toLocaleString('es-ES');
    }

    function setBulkSubmitLoading(form, loading) {
        const button = form.querySelector('[data-bulk-submit-button]');
        const label = button?.querySelector('.bulk-submit-label');
        const spinner = button?.querySelector('.bulk-submit-loading');
        if (button) {
            button.disabled = loading;
        }
        label?.classList.toggle('d-none', loading);
        spinner?.classList.toggle('d-none', !loading);
    }

    function setFormDisabled(form, disabled) {
        form.querySelectorAll('input, select, textarea, button').forEach(el => {
            el.disabled = disabled;
        });
    }

    function updateProgressUi(panel, data) {
        const percent = Math.max(0, Math.min(100, Number(data.percentComplete || 0)));
        const bar = panel.querySelector('[data-bulk-progress-bar]');
        const progress = panel.querySelector('.rag-index-progress');
        const subtitle = panel.querySelector('[data-bulk-progress-subtitle]');
        const stats = panel.querySelector('[data-bulk-progress-stats]');
        const title = panel.querySelector('[data-bulk-progress-title]');

        if (bar) {
            bar.style.width = `${percent}%`;
            bar.textContent = `${percent}%`;
        }

        if (progress) {
            progress.setAttribute('aria-valuenow', String(percent));
        }

        if (title) {
            title.textContent = data.phase === 'Completado'
                ? 'Indexación completada'
                : 'Indexando comentarios #1…';
        }

        if (subtitle) {
            const currentTicket = data.currentTicketNumber
                ? ` Ticket actual: #${data.currentTicketNumber}.`
                : '';
            subtitle.textContent = `${data.phase || 'Procesando'}.${currentTicket} No cierres esta pestaña hasta que finalice.`;
        }

        if (stats) {
            const total = Number(data.totalTickets || 0);
            const processed = Number(data.processed || 0);
            stats.textContent =
                `Procesados: ${formatTicketCount(processed)} / ${formatTicketCount(total)}` +
                ` · Indexados: ${formatTicketCount(data.indexed)}` +
                ` · Omitidos: ${formatTicketCount(data.skipped)}` +
                ` · Errores: ${formatTicketCount(data.failed)}`;
        }
    }

    async function pollProgress(panel, jobId) {
        const progressUrl = panel.dataset.progressUrl;
        const resultUrl = panel.dataset.resultUrl;

        while (true) {
            const response = await fetch(`${progressUrl}?jobId=${encodeURIComponent(jobId)}`, {
                headers: { Accept: 'application/json' }
            });

            if (!response.ok) {
                throw new Error(`No se pudo consultar el progreso (HTTP ${response.status}).`);
            }

            const data = await response.json();
            updateProgressUi(panel, data);

            if (data.status === 'Completed') {
                window.location.href = `${resultUrl}?jobId=${encodeURIComponent(jobId)}`;
                return;
            }

            if (data.status === 'Failed') {
                throw new Error(data.errorMessage || 'La indexación falló.');
            }

            await new Promise(resolve => setTimeout(resolve, 1500));
        }
    }

    async function startBulkIndex(form, panel) {
        const startUrl = panel.dataset.startUrl;
        const formData = new FormData(form);
        const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch(startUrl, {
            method: 'POST',
            headers: token ? { RequestVerificationToken: token } : {},
            body: formData
        });

        if (!response.ok) {
            let message = `No se pudo iniciar la indexación (HTTP ${response.status}).`;
            try {
                const error = await response.json();
                if (error?.error) {
                    message = error.error;
                }
            } catch {
                // ignore parse errors
            }
            throw new Error(message);
        }

        const payload = await response.json();
        if (!payload?.jobId) {
            throw new Error('La indexación no devolvió un identificador de trabajo.');
        }

        return payload.jobId;
    }

    function initBulkIndexProgress() {
        const form = document.querySelector('[data-first-comment-bulk-form]');
        const panel = document.getElementById('bulkIndexProcessing');
        if (!form || !panel) {
            return;
        }

        form.addEventListener('submit', async function (event) {
            event.preventDefault();

            const api = window.MoneyPennyFirstCommentIndex;
            let ticketCount = api?.getBulkTicketsToIndex?.() ?? 0;

            try {
                ticketCount = await api.fetchBulkTicketsToIndexCount();
            } catch {
                ticketCount = api?.getBulkTicketsToIndex?.() ?? ticketCount;
            }

            const formattedCount = api?.formatTicketCount?.(ticketCount) ?? String(ticketCount);
            const rebuild = document.getElementById('rebuildAll')?.checked;
            const maxTickets = document.querySelector('[data-bulk-max-tickets]')?.value?.trim();
            const fromDate = document.getElementById('TicketCreatedFrom')?.value;
            const toDate = document.getElementById('TicketCreatedTo')?.value;
            const kbOnly = document.getElementById('onlyKnowledgeBaseTickets')?.checked;
            const scopeLabel = kbOnly ? 'Knowledge Base' : 'listado de Tickets';

            let message = '¿Iniciar la indexación masiva con la configuración actual?';
            if (rebuild) {
                message = `¿Iniciar la indexación masiva? Se borrará el índice #1 del ámbito «${scopeLabel}» antes de indexar (el otro ámbito no se modifica).`;
            }

            message += `\n\nTickets a indexar: ${formattedCount}.`;
            if (maxTickets) {
                message += `\n\nLímite configurado: ${maxTickets} ticket(s).`;
            }
            if (fromDate || toDate) {
                message += `\n\nRango de fechas: ${fromDate || '…'} → ${toDate || '…'}.`;
            }

            if (!window.confirm(message)) {
                return;
            }

            setBulkSubmitLoading(form, true);

            try {
                const jobId = await startBulkIndex(form, panel);
                setFormDisabled(form, true);
                panel.classList.remove('d-none');
                panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                updateProgressUi(panel, {
                    phase: 'Preparando',
                    percentComplete: 0,
                    totalTickets: ticketCount,
                    processed: 0,
                    indexed: 0,
                    skipped: 0,
                    failed: 0
                });
                await pollProgress(panel, jobId);
            } catch (error) {
                setBulkSubmitLoading(form, false);
                setFormDisabled(form, false);
                panel.classList.remove('d-none');
                updateProgressUi(panel, {
                    phase: 'Error',
                    percentComplete: 0,
                    totalTickets: ticketCount,
                    processed: 0,
                    indexed: 0,
                    skipped: 0,
                    failed: 0
                });
                const subtitle = panel.querySelector('[data-bulk-progress-subtitle]');
                if (subtitle) {
                    subtitle.textContent = error instanceof Error ? error.message : 'La indexación no pudo iniciarse.';
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', initBulkIndexProgress);
})();
