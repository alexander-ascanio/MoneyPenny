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

    function computeBulkTicketsToProcess(corpus, options) {
        const rebuildAll = options.rebuildAll ?? true;
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

    function getBulkCorpus() {
        const root = getBulkEstimateRoot();
        if (!root) {
            return {};
        }

        const state = bulkEstimateState.get(root);
        if (state?.corpus) {
            return state.corpus;
        }

        try {
            return JSON.parse(root.dataset.corpus || '{}');
        } catch {
            return {};
        }
    }

    function getBulkTicketsToIndex() {
        return computeBulkTicketsToProcess(getBulkCorpus(), {
            rebuildAll: document.getElementById('rebuildAll')?.checked ?? true,
            skipIndexed: document.getElementById('skipAlreadyIndexed')?.checked ?? true,
            kbOnly: document.getElementById('onlyKnowledgeBaseTickets')?.checked ?? false,
            maxTickets: document.querySelector('[data-bulk-max-tickets]')?.value
        });
    }

    function getSingleTicketsToIndex() {
        const skip = document.getElementById('skipAlreadyIndexedSingle')?.checked ?? false;
        const rebuild = document.getElementById('rebuildAllSingle')?.checked ?? true;
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
        getSingleTicketsToIndex,
        formatTicketCount
    };

    function initFirstCommentBulkEstimate(root) {
        const config = JSON.parse(root.dataset.pricing || '{}');
        const corpus = JSON.parse(root.dataset.corpus || '{}');
        bulkEstimateState.set(root, { corpus });
        const corpusUrl = root.dataset.corpusUrl;
        const output = root.querySelector('[data-bulk-estimate-output]');
        const corpusInfo = document.querySelector('[data-corpus-info]');
        const rebuild = root.querySelector('[data-bulk-rebuild]');
        const skipIndexed = root.querySelector('[data-bulk-skip-indexed]');
        const processImages = root.querySelector('[data-bulk-process-images]');
        const onlyKnowledgeBase = root.querySelector('[data-bulk-only-knowledge-base]');
        const maxTickets = root.querySelector('[data-bulk-max-tickets]');
        if (!output) {
            return;
        }

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
            const rebuildAll = rebuild?.checked ?? true;
            const skip = skipIndexed?.checked;
            const withImages = processImages?.checked;
            const limit = Number(maxTickets?.value || 0);
            const kbOnly = onlyKnowledgeBase?.checked ?? false;
            const tickets = computeBulkTicketsToProcess(corpus, {
                rebuildAll,
                skipIndexed: skip,
                kbOnly,
                maxTickets: limit
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

        [rebuild, skipIndexed, processImages, onlyKnowledgeBase, maxTickets].forEach(el => {
            if (el) {
                el.addEventListener('change', update);
                el.addEventListener('input', update);
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
