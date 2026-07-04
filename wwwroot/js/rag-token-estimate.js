(function () {
    function estimateTokensFromText(text, charsPerToken) {
        if (!text || !text.trim()) {
            return 0;
        }
        return Math.max(1, Math.ceil(text.length / Math.max(1, charsPerToken)));
    }

    function estimateChunkCount(textLength, chunkSize, overlap) {
        if (textLength <= 0) {
            return 0;
        }
        const size = Math.max(100, chunkSize);
        const step = Math.max(1, size - Math.min(overlap, Math.floor(size / 2)));
        return Math.ceil(textLength / step);
    }

    function costFromTokens(tokens, pricePerMillion) {
        return (tokens / 1_000_000) * pricePerMillion;
    }

    function formatUsd(value) {
        return `$${value.toFixed(4)}`;
    }

    function renderEstimate(container, lines, totals) {
        if (!container) {
            return;
        }

        const linesHtml = lines.map(line => `<li>${line}</li>`).join('');
        const visionPart = totals.visionTokens > 0 ? ` · Vision: ~${totals.visionTokens.toLocaleString()} tok` : '';
        const chatPart = totals.chatTokens > 0 ? ` · Chat: ~${totals.chatTokens.toLocaleString()} tok` : '';

        container.innerHTML = `
            <div class="fw-semibold mb-1"><i class="bi bi-coin me-1"></i>${container.dataset.title || 'Estimación de consumo OpenAI'}</div>
            <ul class="mb-1 small ps-3">${linesHtml}</ul>
            <div class="small text-muted mb-0">
                Coste orientativo: <strong>~${formatUsd(totals.cost)} USD</strong>
                · Embeddings: ~${totals.embeddingTokens.toLocaleString()} tok${visionPart}${chatPart}
            </div>
            <div class="small text-muted fst-italic mt-1 mb-0">Aproximación; el consumo real puede variar.</div>`;
    }

    function readBulkFormOptions() {
        return {
            rebuildAll: document.getElementById('rebuildAll')?.checked ?? false,
            skipIndexed: document.getElementById('skipAlreadyIndexed')?.checked ?? true,
            kbOnly: document.getElementById('onlyKnowledgeBaseTickets')?.checked ?? false,
            maxTickets: document.querySelector('[data-bulk-max-tickets]')?.value,
            ticketCreatedFrom: document.getElementById('TicketCreatedFrom')?.value || '',
            ticketCreatedTo: document.getElementById('TicketCreatedTo')?.value || ''
        };
    }

    function computeBulkTicketsToProcess(corpus, options) {
        if (options.exactCount != null && Number.isFinite(options.exactCount)) {
            return Math.max(0, options.exactCount);
        }

        const rebuildAll = options.rebuildAll ?? false;
        const skipIndexed = options.skipIndexed ?? true;
        const kbOnly = options.kbOnly ?? false;
        const limit = Number(options.maxTickets || 0);

        const totalPool = kbOnly
            ? Number(corpus.knowledgeBaseTotalTickets || 0)
            : Number(corpus.totalTickets || 0);
        const pendingPool = kbOnly
            ? Number(corpus.knowledgeBasePendingTickets || 0)
            : Number(corpus.pendingTickets || 0);

        let tickets = rebuildAll ? totalPool : (skipIndexed ? pendingPool : totalPool);
        if (limit > 0) {
            tickets = Math.min(tickets, limit);
        }

        return Math.max(0, tickets);
    }

    const bulkEstimateState = new WeakMap();

    function getBulkEstimateRoot() {
        return document.querySelector('[data-bulk-index-estimate]');
    }

    function getBulkState() {
        const root = getBulkEstimateRoot();
        if (!root) {
            return null;
        }

        let state = bulkEstimateState.get(root);
        if (!state) {
            state = {
                corpus: {},
                exactCount: null,
                countLoading: false,
                countError: null,
                countRequestId: 0
            };
            bulkEstimateState.set(root, state);
        }

        return { root, state };
    }

    function getBulkCorpus() {
        const ctx = getBulkState();
        if (!ctx) {
            return {};
        }

        if (ctx.state.corpus && Object.keys(ctx.state.corpus).length > 0) {
            return ctx.state.corpus;
        }

        try {
            return JSON.parse(ctx.root.dataset.corpus || '{}');
        } catch {
            return {};
        }
    }

    function buildBulkCountUrl(baseUrl, options) {
        if (!baseUrl) {
            return '';
        }

        const params = new URLSearchParams();
        params.set('onlyKnowledgeBaseTickets', options.kbOnly ? 'true' : 'false');
        params.set('rebuildAll', options.rebuildAll ? 'true' : 'false');
        params.set('skipAlreadyIndexed', options.skipIndexed ? 'true' : 'false');

        if (options.ticketCreatedFrom) {
            params.set('ticketCreatedFrom', options.ticketCreatedFrom);
        }

        if (options.ticketCreatedTo) {
            params.set('ticketCreatedTo', options.ticketCreatedTo);
        }

        const limit = Number(options.maxTickets || 0);
        if (limit > 0) {
            params.set('maxTickets', String(limit));
        }

        const separator = baseUrl.includes('?') ? '&' : '?';
        return `${baseUrl}${separator}${params.toString()}`;
    }

    async function fetchBulkTicketsToIndexCount() {
        const ctx = getBulkState();
        if (!ctx) {
            return computeBulkTicketsToProcess({}, readBulkFormOptions());
        }

        const { root, state } = ctx;
        const countUrl = root.dataset.bulkCountUrl;
        const options = readBulkFormOptions();

        if (!countUrl) {
            return computeBulkTicketsToProcess(state.corpus, options);
        }

        const requestId = ++state.countRequestId;
        state.countLoading = true;
        state.countError = null;

        const response = await fetch(buildBulkCountUrl(countUrl, options), {
            headers: { Accept: 'application/json' }
        });

        if (!response.ok) {
            state.countLoading = false;
            throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        if (requestId !== state.countRequestId) {
            return state.exactCount ?? computeBulkTicketsToProcess(state.corpus, options);
        }

        state.exactCount = Number(data.ticketsToProcess ?? 0);
        state.countLoading = false;
        return state.exactCount;
    }

    function getBulkTicketsToIndex() {
        const ctx = getBulkState();
        const options = readBulkFormOptions();
        const corpus = ctx?.state?.corpus ?? getBulkCorpus();

        if (ctx?.state?.exactCount != null && Number.isFinite(ctx.state.exactCount)) {
            return computeBulkTicketsToProcess(corpus, { ...options, exactCount: ctx.state.exactCount });
        }

        return computeBulkTicketsToProcess(corpus, options);
    }

    function getSingleTicketsToIndex() {
        const skip = document.getElementById('skipAlreadyIndexedSingle')?.checked ?? true;
        const rebuild = document.getElementById('rebuildAllSingle')?.checked ?? false;
        return {
            count: 1,
            maySkip: skip && !rebuild
        };
    }

    function formatTicketCount(count) {
        return Number(count || 0).toLocaleString('es-ES');
    }

    window.MoneyPennyFirstCommentIndex = {
        getBulkTicketsToIndex,
        fetchBulkTicketsToIndexCount,
        getSingleTicketsToIndex,
        formatTicketCount
    };

    function initFirstCommentBulkEstimate(root) {
        const config = JSON.parse(root.dataset.pricing || '{}');
        const corpus = JSON.parse(root.dataset.corpus || '{}');
        const state = {
            corpus,
            exactCount: null,
            countLoading: false,
            countError: null,
            countRequestId: 0
        };
        bulkEstimateState.set(root, state);
        const corpusUrl = root.dataset.corpusUrl;
        const countUrl = root.dataset.bulkCountUrl;
        const output = root.querySelector('[data-bulk-estimate-output]');
        const corpusInfo = document.querySelector('[data-corpus-info]');
        const rebuild = root.querySelector('[data-bulk-rebuild]');
        const skipIndexed = root.querySelector('[data-bulk-skip-indexed]');
        const processImages = root.querySelector('[data-bulk-process-images]');
        const onlyKnowledgeBase = root.querySelector('[data-bulk-only-knowledge-base]');
        const maxTickets = root.querySelector('[data-bulk-max-tickets]');
        const dateFrom = document.getElementById('TicketCreatedFrom');
        const dateTo = document.getElementById('TicketCreatedTo');
        if (!output) {
            return;
        }

        let countDebounceTimer = null;

        const scheduleBulkCountRefresh = () => {
            if (!countUrl) {
                return;
            }

            clearTimeout(countDebounceTimer);
            countDebounceTimer = setTimeout(() => {
                fetchBulkTicketsToIndexCount()
                    .then(() => update())
                    .catch(() => {
                        state.countError = true;
                        state.exactCount = null;
                        update();
                    });
            }, 300);
        };

        const updateCorpusInfo = () => {
            if (!corpusInfo || !corpus.corpusSampleSize) {
                return;
            }

            const avgChars = Number(corpus.averageCommentCharCount || 0).toLocaleString();
            const avgImages = Number(corpus.averageImagesPerTicket || 0).toFixed(2).replace(/\.?0+$/, '');
            corpusInfo.innerHTML =
                `Muestra de ${corpus.corpusSampleSize} ticket(s): media ~${avgChars} caracteres/comentario, ~${avgImages} imagen(es)/ticket.`;
        };

        const update = () => {
            const avgChars = Number(corpus.averageCommentCharCount || 0);
            const avgImages = Number(corpus.averageImagesPerTicket || 0);
            const formOptions = readBulkFormOptions();
            const rebuildAll = formOptions.rebuildAll;
            const skip = formOptions.skipIndexed;
            const withImages = processImages?.checked;
            const limit = Number(formOptions.maxTickets || 0);
            const kbOnly = formOptions.kbOnly;

            if (state.countLoading && countUrl) {
                renderEstimate(output, ['Calculando tickets a procesar…'], {
                    embeddingTokens: 0,
                    visionTokens: 0,
                    chatTokens: 0,
                    cost: 0
                });
                return;
            }

            const tickets = computeBulkTicketsToProcess(corpus, {
                rebuildAll,
                skipIndexed: skip,
                kbOnly,
                maxTickets: limit,
                exactCount: state.exactCount
            });

            if (tickets <= 0) {
                renderEstimate(output, ['No hay tickets que procesar.'], {
                    embeddingTokens: 0,
                    visionTokens: 0,
                    chatTokens: 0,
                    cost: 0
                });
                return;
            }

            if (!corpus.corpusSampleSize) {
                renderEstimate(output, ['Cargando estadísticas del corpus…'], {
                    embeddingTokens: 0,
                    visionTokens: 0,
                    chatTokens: 0,
                    cost: 0
                });
                return;
            }

            const docChars = avgChars + 120;
            const chunksPerTicket = Math.max(1, estimateChunkCount(docChars, config.chunkSize, config.chunkOverlap));
            const tokensPerTicket = estimateTokensFromText('x'.repeat(docChars), config.charsPerToken);
            const embeddingCalls = tickets * chunksPerTicket;
            const embeddingTokens = tickets * tokensPerTicket;
            let visionTokens = 0;
            let visionCalls = 0;
            const lines = [
                `Embeddings (${config.embeddingModel}): ~${embeddingCalls.toLocaleString()} llamada(s), ~${embeddingTokens.toLocaleString()} tokens de entrada.`,
                `Tickets a procesar: ${tickets.toLocaleString()} (media ~${docChars.toLocaleString()} caracteres/documento, muestra ${corpus.corpusSampleSize || 0} tickets).`
            ];

            if (state.countError && countUrl) {
                lines.push('No se pudo recalcular el total exacto; se muestra una aproximación.');
            }

            let cost = costFromTokens(embeddingTokens, config.embeddingPricePerMillion);
            if (withImages && avgImages > 0) {
                visionCalls = Math.ceil(tickets * avgImages);
                const visionInput = visionCalls * config.visionInputTokensPerImage;
                const visionOutput = visionCalls * config.visionOutputTokensPerImage;
                visionTokens = visionInput + visionOutput;
                cost += costFromTokens(visionInput, config.visionInputPricePerMillion);
                cost += costFromTokens(visionOutput, config.visionOutputPricePerMillion);
                lines.push(`Vision (${config.visionModel}): ~${visionCalls.toLocaleString()} llamada(s), ~${visionInput.toLocaleString()} tokens entrada + ~${visionOutput.toLocaleString()} salida.`);
            } else if (withImages) {
                lines.push('Vision: depende de cuántos tickets tengan imágenes.');
            } else {
                lines.push('Sin llamadas Vision.');
            }

            renderEstimate(output, lines, {
                embeddingTokens,
                visionTokens,
                chatTokens: 0,
                cost
            });
        };

        const onFormOptionChange = () => {
            state.countError = null;
            scheduleBulkCountRefresh();
            update();
        };

        [rebuild, skipIndexed, processImages, onlyKnowledgeBase, maxTickets, dateFrom, dateTo].forEach(el => {
            if (el) {
                el.addEventListener('change', onFormOptionChange);
                el.addEventListener('input', onFormOptionChange);
            }
        });

        if (onlyKnowledgeBase) {
            onlyKnowledgeBase.addEventListener('change', () => {
                if (!corpus.corpusSampleSize) {
                    return;
                }

                corpus.corpusSampleSize = 0;
                renderEstimate(output, ['Cargando estadísticas del corpus…'], {
                    embeddingTokens: 0,
                    visionTokens: 0,
                    chatTokens: 0,
                    cost: 0
                });

                fetch(buildCorpusStatsUrl(corpusUrl, onlyKnowledgeBase.checked), { headers: { Accept: 'application/json' } })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error(`HTTP ${response.status}`);
                        }
                        return response.json();
                    })
                    .then(data => {
                        corpus.averageCommentCharCount = data.averageCommentCharCount;
                        corpus.averageImagesPerTicket = data.averageImagesPerTicket;
                        corpus.corpusSampleSize = data.corpusSampleSize;
                        updateCorpusInfo();
                        scheduleBulkCountRefresh();
                        update();
                    })
                    .catch(() => {
                        if (corpusInfo) {
                            corpusInfo.textContent = 'No se pudieron cargar las estadísticas del corpus.';
                        }
                    });
            });
        }

        if (corpusUrl && !corpus.corpusSampleSize) {
            renderEstimate(output, ['Cargando estadísticas del corpus…'], {
                embeddingTokens: 0,
                visionTokens: 0,
                chatTokens: 0,
                cost: 0
            });

            fetch(buildCorpusStatsUrl(corpusUrl, onlyKnowledgeBase?.checked ?? false), { headers: { Accept: 'application/json' } })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}`);
                    }
                    return response.json();
                })
                .then(data => {
                    corpus.averageCommentCharCount = data.averageCommentCharCount;
                    corpus.averageImagesPerTicket = data.averageImagesPerTicket;
                    corpus.corpusSampleSize = data.corpusSampleSize;
                    updateCorpusInfo();
                    scheduleBulkCountRefresh();
                    update();
                })
                .catch(() => {
                    if (corpusInfo) {
                        corpusInfo.textContent = 'No se pudieron cargar las estadísticas del corpus.';
                    }
                    renderEstimate(output, ['No se pudieron cargar las estadísticas del corpus.'], {
                        embeddingTokens: 0,
                        visionTokens: 0,
                        chatTokens: 0,
                        cost: 0
                    });
                });
        } else {
            updateCorpusInfo();
            scheduleBulkCountRefresh();
            update();
        }
    }

    function buildCorpusStatsUrl(baseUrl, onlyKnowledgeBaseScope) {
        if (!baseUrl) {
            return '';
        }

        const separator = baseUrl.includes('?') ? '&' : '?';
        return `${baseUrl}${separator}onlyKnowledgeBaseScope=${onlyKnowledgeBaseScope ? 'true' : 'false'}`;
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-bulk-index-estimate]').forEach(initFirstCommentBulkEstimate);
    });
})();
